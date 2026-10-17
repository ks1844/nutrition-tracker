using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NutritionTracker.Api.Config;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Api.Services;

public class DashboardService(
    AppDbContext db,
    ProfileService profiles,
    TargetService targetService,
    MenuService menus,
    NutritionCatalog catalog,
    IOptions<NutritionOptions> options)
{
    private static readonly string[] MainMeals = ["breakfast", "lunch", "dinner"];

    /// <summary>ある日の摂取量。<see cref="Amounts"/> は保存済みスナップショットの合計（丸めない）。</summary>
    private sealed class DayIntake
    {
        public Dictionary<string, decimal> Amounts { get; } = [];

        public HashSet<string> Incomplete { get; } = [];

        /// <summary>食事区分ごとのタンパク質 g（同じ日・同じ区分の記録は 1 食として合算）</summary>
        public Dictionary<string, decimal> MealProtein { get; } = [];

        public decimal Amount(string code) => Amounts.GetValueOrDefault(code);
    }

    /// <summary>API-17 日次ダッシュボード。スコアは「今日の進捗」（指定日 1 日分）。</summary>
    public async Task<DailyDashboardDto> DailyAsync(DateOnly? date)
    {
        if (date is not { } day)
        {
            throw new ValidationException("date", "日付を指定してください");
        }
        var profile = await profiles.RequireAsync();
        var targets = await targetService.ResolveAsync(profile, day);
        var intake = (await LoadAsync(day, day)).GetValueOrDefault(day);

        var amounts = intake?.Amounts ?? [];
        var incomplete = intake?.Incomplete ?? [];
        var nutrients = await catalog.NutrientsAsync();

        var energyIntake = amounts.GetValueOrDefault(NutrientCodes.Energy);
        var pfc = PfcCalculator.Calculate(
            amounts.GetValueOrDefault(NutrientCodes.Protein), amounts.GetValueOrDefault(NutrientCodes.Fat),
            amounts.GetValueOrDefault(NutrientCodes.Carbohydrate));

        var rows = nutrients.Select(n =>
        {
            var view = targets.Get(n.Code);
            var amount = amounts.GetValueOrDefault(n.Code);
            var (rate, status) = Evaluate(amount, view);
            return new DailyNutrientDto(
                n.Code, n.Name, n.Unit, SnapshotCalculator.ForDisplay(amount, n.Unit),
                ToDto(view), rate, status, incomplete.Contains(n.Code) && !n.IsReference, n.IsReference);
        }).ToList();

        var incompleteCodes = rows.Where(r => r.IsIncomplete).Select(r => r.Code).ToList();
        var remaining = (await menus.ListAsync())
            .Where(m => m.Remaining.Ratio > 0m)
            .Select(m => new RemainingMenuDto(m.Id, m.Name, m.Remaining.Ratio, m.Remaining.Servings, m.ServingLabel))
            .ToList();

        return new DailyDashboardDto(
            day, intake is not null,
            new EnergySummaryDto(
                (int)RoundingPolicy.Round(energyIntake, 0), targets.EnergyKcal,
                RoundingPolicy.Round(energyIntake / targets.EnergyKcal, RoundingPolicy.RatioDigits)),
            new PfcSummaryDto(
                SnapshotCalculator.ForDisplay(amounts.GetValueOrDefault(NutrientCodes.Protein), "g"),
                SnapshotCalculator.ForDisplay(amounts.GetValueOrDefault(NutrientCodes.Fat), "g"),
                SnapshotCalculator.ForDisplay(amounts.GetValueOrDefault(NutrientCodes.Carbohydrate), "g"),
                new PfcRatioPctDto(pfc.ProteinDisplay, pfc.FatDisplay, pfc.CarbDisplay)),
            intake is null ? EmptyScores() : ScoresFor(intake.Amounts, intake.Incomplete, MainMealProteins([intake]), targets),
            rows, remaining,
            incompleteCodes.Count == 0 ? [] : [new DashboardNoticeDto("INCOMPLETE_NUTRIENT", incompleteCodes)]);
    }

    // ---- 摂取量の集計 ----

    /// <summary>期間内の日ごとの摂取量。記録のある日だけが入る。保存済みのスナップショットを合計するだけで、栄養計算はしない。</summary>
    private async Task<Dictionary<DateOnly, DayIntake>> LoadAsync(DateOnly from, DateOnly to)
    {
        var codes = (await catalog.NutrientsAsync()).ToDictionary(n => n.Id, n => n.Code);
        var rows = await db.MealRecordItemNutrients.AsNoTracking()
            .Where(n => n.MealRecordItem!.MealRecord!.UserId == User.FixedUserId
                && n.MealRecordItem.MealRecord.EatenOn >= from && n.MealRecordItem.MealRecord.EatenOn <= to)
            .GroupBy(n => new { n.MealRecordItem!.MealRecord!.EatenOn, n.MealRecordItem.MealRecord.MealType, n.NutrientId })
            .Select(g => new { g.Key.EatenOn, g.Key.MealType, g.Key.NutrientId, Sum = g.Sum(x => x.Amount), Incomplete = g.Max(x => x.IsIncomplete ? 1 : 0) })
            .ToListAsync();

        var days = new Dictionary<DateOnly, DayIntake>();
        foreach (var row in rows)
        {
            var code = codes[row.NutrientId];
            if (!days.TryGetValue(row.EatenOn, out var day))
            {
                days[row.EatenOn] = day = new DayIntake();
            }
            day.Amounts[code] = day.Amounts.GetValueOrDefault(code) + row.Sum;
            if (row.Incomplete == 1)
            {
                day.Incomplete.Add(code);
            }
            if (code == NutrientCodes.Protein)
            {
                day.MealProtein[row.MealType] = day.MealProtein.GetValueOrDefault(row.MealType) + row.Sum;
            }
        }
        return days;
    }

    /// <summary>朝・昼・夕の食事ごとのタンパク質 g（間食は含めない）。</summary>
    private static List<decimal> MainMealProteins(IEnumerable<DayIntake> days) =>
        days.SelectMany(d => MainMeals.Where(d.MealProtein.ContainsKey).Select(m => d.MealProtein[m])).ToList();

    // ---- 評価（達成率・状態・スコア） ----

    /// <summary>
    /// 達成率と状態。下限型の目標があれば達成率（100% で頭打ち）、上限（UL、無ければ DG_MAX）を超えていれば OVER_UL。
    /// 上限しか無い栄養素は、超えなければ ACHIEVED（達成率なし）。目標が何も無ければ NO_TARGET。
    /// </summary>
    private static (decimal? Rate, string Status) Evaluate(decimal intake, NutrientTargetView? view)
    {
        if (view is not { HasAny: true })
        {
            return (null, "NO_TARGET");
        }
        decimal? rate = view.Amount is { } target ? AchievementCalculator.LowerRate(intake, target) : null;
        if (view.Limit is { } limit && intake > limit)
        {
            return (rate, "OVER_UL");
        }
        return (rate, rate is null or >= 1m ? "ACHIEVED" : "DEFICIENT");
    }

    private ScoresDto ScoresFor(
        IReadOnlyDictionary<string, decimal> intake, IReadOnlySet<string> incomplete, IReadOnlyCollection<decimal> mainMealProteins, ResolvedTargets targets)
    {
        var settings = options.Value.Scoring.ToSettings();
        var salt = targets.Get(NutrientCodes.SaltEquivalent)?.Limit;

        var beauty = ScoreCalculator.Beauty(intake, targets.Lower, settings);
        var muscle = ScoreCalculator.Muscle(
            intake.GetValueOrDefault(NutrientCodes.Protein), targets.ProteinG, mainMealProteins,
            intake.GetValueOrDefault(NutrientCodes.Energy), targets.EnergyKcal, settings);
        var @base = ScoreCalculator.Base(intake, targets.Lower, intake.GetValueOrDefault(NutrientCodes.SaltEquivalent), salt, settings);

        return new ScoresDto(
            Score(beauty, settings.BeautyWeights.Keys.Where(targets.Lower.ContainsKey).Any(incomplete.Contains), settings),
            Score(muscle, incomplete.Contains(NutrientCodes.Protein) || incomplete.Contains(NutrientCodes.Energy), settings),
            Score(@base, settings.BaseNutrients.Where(targets.Lower.ContainsKey).Any(incomplete.Contains), settings));
    }

    private static ScoreDto Score(int? value, bool hasIncomplete, ScoringSettings settings) => value is { } v
        ? new ScoreDto(v, ScoreCalculator.Band(v, settings) switch { ScoreBand.Good => "GOOD", ScoreBand.Fair => "FAIR", _ => "LOW" }, false, hasIncomplete)
        : new ScoreDto(null, null, false, hasIncomplete);

    private static ScoresDto EmptyScores()
    {
        var none = new ScoreDto(null, null, false, false);
        return new ScoresDto(none, none, none);
    }

    private static NutrientTargetViewDto ToDto(NutrientTargetView? v) =>
        new(v?.Kind, v?.Min, v?.Max, v?.Ul, "PER_DAY", v?.Amount);
}
