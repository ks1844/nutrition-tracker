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

/// <summary>
/// ダッシュボード（日次・直近 7 日）の集計を行うサービスです。
/// 記録時に保存した栄養量を足し合わせ、目標と比べて達成率・状態・スコアを求めます。栄養の計算そのものはここではしません。
/// </summary>
/// <param name="db">データベースへの窓口。</param>
/// <param name="profiles">プロフィール。</param>
/// <param name="targetService">目標の解決。</param>
/// <param name="menus">メニューの残り。</param>
/// <param name="catalog">栄養素の情報。</param>
/// <param name="clock">今日の日付。</param>
/// <param name="options">スコアの設定。</param>
public class DashboardService(
    AppDbContext db,
    ProfileService profiles,
    TargetService targetService,
    MenuService menus,
    NutritionCatalog catalog,
    AppClock clock,
    IOptions<NutritionOptions> options)
{
    /// <summary>
    /// 朝・昼・夕の食事区分（間食は含めない）。筋肉スコアの分配率の対象です。
    /// </summary>
    private static readonly string[] MainMeals = ["breakfast", "lunch", "dinner"];

    /// <summary>
    /// ある日の摂取量です。Amounts は保存済みの栄養量の合計（丸めません）。
    /// </summary>
    private sealed class DayIntake
    {
        /// <summary>
        /// 栄養素コード → その日の合計。
        /// </summary>
        public Dictionary<string, decimal> Amounts { get; } = [];

        /// <summary>
        /// 未測定の食材を含む栄養素のコード。
        /// </summary>
        public HashSet<string> Incomplete { get; } = [];

        /// <summary>
        /// 食事区分ごとのタンパク質（g）。同じ日・同じ区分の記録は 1 食として合算します。
        /// </summary>
        public Dictionary<string, decimal> MealProtein { get; } = [];

        /// <summary>
        /// 栄養素の合計を返します。
        /// </summary>
        /// <param name="code">栄養素コード。</param>
        /// <returns>合計。なければ 0。</returns>
        public decimal Amount(string code) => Amounts.GetValueOrDefault(code);
    }

    /// <summary>
    /// 指定した日の摂取状況を集計します（API-17）。スコアは「その日の進捗」です。
    /// エネルギー・PFC 比、栄養素ごとの達成率と状態、3 スコア、メニューの残り、未測定の注記を返します。
    /// </summary>
    /// <param name="date">集計する日（必須）。</param>
    /// <returns>その日のダッシュボード。プロフィール未登録なら 404。</returns>
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
        var rows = await RowsAsync(amounts, intake?.Incomplete ?? [], targets, null);

        return new DailyDashboardDto(
            day, intake is not null,
            new EnergySummaryDto(
                (int)RoundingPolicy.Round(amounts.GetValueOrDefault(NutrientCodes.Energy), 0), targets.EnergyKcal,
                RoundingPolicy.Round(amounts.GetValueOrDefault(NutrientCodes.Energy) / targets.EnergyKcal, RoundingPolicy.RatioDigits)),
            PfcOf(amounts),
            intake is null ? EmptyScores() : ScoresFor(intake.Amounts, intake.Incomplete, MainMealProteins([intake]), targets),
            rows, await RemainingMenusAsync(), NoticesOf(rows));
    }

    /// <summary>
    /// 終了日までの直近 7 日の摂取状況を集計します（API-18）。
    /// 摂取量は記録のある日だけで平均し（7 で割らない）、記録が 3 日未満ならスコアを出さずデータ不足にします。日ごとの推移と、期間内に上限を超えた日数も返します。
    /// </summary>
    /// <param name="endDate">7 日間の最後の日（必須。未来日は 400）。</param>
    /// <returns>直近 7 日のダッシュボード。プロフィール未登録なら 404。</returns>
    public async Task<WeeklyDashboardDto> WeeklyAsync(DateOnly? endDate)
    {
        if (endDate is not { } end)
        {
            throw new ValidationException("endDate", "終了日を指定してください");
        }
        if (end > clock.Today)
        {
            throw new ValidationException("endDate", "未来の日付は指定できません");
        }
        var profile = await profiles.RequireAsync();
        var targets = await targetService.ResolveAsync(profile, end);
        var settings = options.Value.Scoring.ToSettings();
        var start = end.AddDays(-6);
        var days = await LoadAsync(start, end);
        var recordDays = days.Count;
        var insufficient = ScoreCalculator.IsInsufficientData(recordDays, settings);

        var series = Enumerable.Range(0, 7).Select(i => start.AddDays(i)).Select(date =>
        {
            if (!days.TryGetValue(date, out var d))
            {
                return new SeriesPointDto(date, false, null, null);
            }
            var s = ScoresFor(d.Amounts, d.Incomplete, MainMealProteins([d]), targets);
            return new SeriesPointDto(
                date, true, (int)RoundingPolicy.Round(d.Amount(NutrientCodes.Energy), 0),
                new SeriesScoresDto(s.Beauty.Value, s.Muscle.Value, s.Base.Value));
        }).ToList();

        var scores = EmptyScores(insufficient);
        WeeklyAveragesDto averages = new(null, null, []);
        IReadOnlyList<NutrientStatusDto> rows = [];
        if (recordDays > 0)
        {
            var average = days.Values.SelectMany(d => d.Amounts).GroupBy(p => p.Key)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Value) / recordDays);
            var incomplete = days.Values.SelectMany(d => d.Incomplete).ToHashSet();
            var ulDays = (await catalog.NutrientsAsync()).ToDictionary(
                n => n.Code,
                n => targets.Get(n.Code)?.Limit is { } limit ? days.Values.Count(d => d.Amount(n.Code) > limit) : 0);
            rows = await RowsAsync(average, incomplete, targets, ulDays);
            averages = new WeeklyAveragesDto(
                new AverageEnergyDto((int)RoundingPolicy.Round(average.GetValueOrDefault(NutrientCodes.Energy), 0), targets.EnergyKcal),
                PfcOf(average), rows);
            if (!insufficient)
            {
                scores = ScoresFor(average, incomplete, MainMealProteins(days.Values), targets);
            }
        }

        return new WeeklyDashboardDto(
            end, start, recordDays, insufficient, averages, scores, series, await RemainingMenusAsync(), NoticesOf(rows));
    }

    /// <summary>
    /// 栄養素ごとの摂取量・目標・達成率・状態を一覧にします。
    /// </summary>
    /// <param name="amounts">栄養素コード → 摂取量。</param>
    /// <param name="incomplete">未測定を含む栄養素のコード。</param>
    /// <param name="targets">目標。</param>
    /// <param name="ulDays">栄養素コード → 期間内に上限を超えた日数（7 日平均のときだけ）。</param>
    /// <returns>栄養素ごとの状態（表示順）。</returns>
    private async Task<List<NutrientStatusDto>> RowsAsync(
        IReadOnlyDictionary<string, decimal> amounts, IReadOnlySet<string> incomplete, ResolvedTargets targets, IReadOnlyDictionary<string, int>? ulDays) =>
        (await catalog.NutrientsAsync()).Select(n =>
        {
            var view = targets.Get(n.Code);
            var amount = amounts.GetValueOrDefault(n.Code);
            var (rate, status) = Evaluate(amount, view);
            return new NutrientStatusDto(
                n.Code, n.Name, n.Unit, SnapshotCalculator.ForDisplay(amount, n.Unit),
                ToDto(view), rate, status, incomplete.Contains(n.Code) && !n.IsReference, n.IsReference,
                ulDays?.GetValueOrDefault(n.Code));
        }).ToList();

    /// <summary>
    /// タンパク質・脂質・炭水化物の量（g）と、PFC 比（整数の表示用）をまとめます。
    /// </summary>
    /// <param name="amounts">栄養素コード → 摂取量。</param>
    /// <returns>PFC のまとめ。</returns>
    private static PfcSummaryDto PfcOf(IReadOnlyDictionary<string, decimal> amounts)
    {
        decimal Get(string code) => amounts.GetValueOrDefault(code);
        var pfc = PfcCalculator.Calculate(Get(NutrientCodes.Protein), Get(NutrientCodes.Fat), Get(NutrientCodes.Carbohydrate));
        return new PfcSummaryDto(
            SnapshotCalculator.ForDisplay(Get(NutrientCodes.Protein), "g"),
            SnapshotCalculator.ForDisplay(Get(NutrientCodes.Fat), "g"),
            SnapshotCalculator.ForDisplay(Get(NutrientCodes.Carbohydrate), "g"),
            new PfcRatioPctDto(pfc.ProteinDisplay, pfc.FatDisplay, pfc.CarbDisplay));
    }

    /// <summary>
    /// まだ残りがあるメニュー（残りの割合が 0 より大きいもの）を、新しい順に返します。
    /// </summary>
    /// <returns>残りのあるメニュー。</returns>
    private async Task<List<RemainingMenuDto>> RemainingMenusAsync() =>
        (await menus.ListAsync())
            .Where(m => m.Remaining.Ratio > 0m)
            .Select(m => new RemainingMenuDto(m.Id, m.Name, m.Remaining.Ratio, m.Remaining.Servings, m.ServingLabel))
            .ToList();

    /// <summary>
    /// 未測定の食材を含む栄養素があれば、画面に注記を出すための通知にします。
    /// </summary>
    /// <param name="rows">栄養素ごとの状態。</param>
    /// <returns>通知の一覧（なければ空）。</returns>
    private static List<DashboardNoticeDto> NoticesOf(IEnumerable<NutrientStatusDto> rows)
    {
        var incomplete = rows.Where(r => r.IsIncomplete).Select(r => r.Code).ToList();
        return incomplete.Count == 0 ? [] : [new DashboardNoticeDto("INCOMPLETE_NUTRIENT", incomplete)];
    }

    // ---- 摂取量の集計 ----

    /// <summary>
    /// 期間内の日ごとの摂取量を、保存済みの栄養量から集計します（栄養の計算はしません）。記録のある日だけが入ります。
    /// </summary>
    /// <param name="from">期間の開始日。</param>
    /// <param name="to">期間の終了日。</param>
    /// <returns>日付 → その日の摂取量。</returns>
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

    /// <summary>
    /// 朝・昼・夕それぞれの食事のタンパク質（g）を集めます（間食は含めません）。筋肉スコアの分配率に使います。
    /// </summary>
    /// <param name="days">対象の日の摂取量。</param>
    /// <returns>食事ごとのタンパク質（g）。</returns>
    private static List<decimal> MainMealProteins(IEnumerable<DayIntake> days) =>
        days.SelectMany(d => MainMeals.Where(d.MealProtein.ContainsKey).Select(m => d.MealProtein[m])).ToList();

    // ---- 評価（達成率・状態・スコア） ----

    /// <summary>
    /// 栄養素の達成率と状態を判定します。
    /// 下限型の目標があれば達成率（100% で頭打ち）、上限を超えていれば OVER_UL、達成率が 1 未満なら DEFICIENT、1 なら ACHIEVED です。上限しかない栄養素は、超えなければ ACHIEVED（達成率なし）。目標がなければ NO_TARGET です。
    /// </summary>
    /// <param name="intake">摂取量。</param>
    /// <param name="view">目標。</param>
    /// <returns>達成率（なければ null）と状態。</returns>
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

    /// <summary>
    /// 摂取量から、美肌・筋肉・基礎の 3 スコアを求めます。
    /// 未測定の食材を含む栄養素がスコアの対象にあるときは、その印も付けます。
    /// </summary>
    /// <param name="intake">栄養素コード → 摂取量。</param>
    /// <param name="incomplete">未測定を含む栄養素のコード。</param>
    /// <param name="mainMealProteins">朝・昼・夕それぞれの食事のタンパク質（g）。</param>
    /// <param name="targets">目標。</param>
    /// <returns>3 スコア。</returns>
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

    /// <summary>
    /// スコアの数値を、区分（GOOD / FAIR / LOW）つきの返却用の形にします。
    /// </summary>
    /// <param name="value">スコア。出せないときは null。</param>
    /// <param name="hasIncomplete">未測定を含むか。</param>
    /// <param name="settings">区分の設定。</param>
    /// <returns>返却用のスコア。</returns>
    private static ScoreDto Score(int? value, bool hasIncomplete, ScoringSettings settings) => value is { } v
        ? new ScoreDto(v, ScoreCalculator.Band(v, settings) switch { ScoreBand.Good => "GOOD", ScoreBand.Fair => "FAIR", _ => "LOW" }, false, hasIncomplete)
        : new ScoreDto(null, null, false, hasIncomplete);

    /// <summary>
    /// スコアを出せないときの、3 スコアとも値のない結果を作ります。
    /// </summary>
    /// <param name="insufficientData">データ不足が理由のとき true。</param>
    /// <returns>値のない 3 スコア。</returns>
    private static ScoresDto EmptyScores(bool insufficientData = false)
    {
        var none = new ScoreDto(null, null, insufficientData, false);
        return new ScoresDto(none, none, none);
    }

    /// <summary>
    /// 目標を返却用の形に変換します（量の基準は常に 1 日あたり）。
    /// </summary>
    /// <param name="v">目標。なければ null。</param>
    /// <returns>返却用の目標。</returns>
    private static NutrientTargetViewDto ToDto(NutrientTargetView? v) =>
        new(v?.Kind, v?.Min, v?.Max, v?.Ul, "PER_DAY", v?.Amount);
}
