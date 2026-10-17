using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Entities;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 栄養素 1 つの目標。<paramref name="Kind"/>/<paramref name="Amount"/> は下限型（RDA &gt; AI &gt; DG_MIN の順に 1 つ）、
/// <paramref name="Min"/>/<paramref name="Max"/>/<paramref name="Ul"/> は T栄養目標の DG_MIN/DG_MAX/UL。単位は 1 日あたりに換算済み。
/// </summary>
public sealed record NutrientTargetView(string? Kind, decimal? Amount, decimal? Min, decimal? Max, decimal? Ul)
{
    /// <summary>上限（UL を優先し、無ければ DG_MAX）。超えると警告する。</summary>
    public decimal? Limit => Ul ?? Max;

    public bool HasAny => Amount is not null || Limit is not null;
}

/// <summary>ある日付時点の目標一式。ENERGY/PROT はプロフィールからの算出値（Kind = CALCULATED）。</summary>
public sealed record ResolvedTargets(int EnergyKcal, decimal ProteinG, IReadOnlyDictionary<string, NutrientTargetView> ByCode)
{
    /// <summary>下限型の目標量（コード → 量）。スコアの達成率に使う。</summary>
    public IReadOnlyDictionary<string, decimal> Lower { get; } =
        ByCode.Where(p => p.Value.Amount is not null).ToDictionary(p => p.Key, p => p.Value.Amount!.Value);

    public NutrientTargetView? Get(string code) => ByCode.GetValueOrDefault(code);
}

public class TargetService(AppDbContext db, ProfileService profiles)
{
    private const string Calculated = "CALCULATED";
    private static readonly string[] LowerPriority = ["RDA", "AI", "DG_MIN"];

    /// <summary>
    /// 目標を解決する。エネルギー・タンパク質はプロフィールから、その他は T栄養目標を性別・年齢（<paramref name="on"/> 時点）で引く。
    /// 体重あたり（PER_KG_BW）は体重を掛けて 1 日あたりにする。エネルギー比（PCT_ENERGY）は未対応のため目標なしとして扱う（暫定）。
    /// </summary>
    public async Task<ResolvedTargets> ResolveAsync(UserProfile profile, DateOnly on)
    {
        var (_, effectiveKcal, proteinG) = profiles.ComputeTargets(profile, on);
        var age = TargetCalculator.AgeOn(profile.BirthDate, on);
        var rows = await db.NutrientTargets.AsNoTracking()
            .Include(t => t.Nutrient)
            .Where(t => (t.Sex == profile.Sex || t.Sex == "all") && t.AgeMin <= age && age <= t.AgeMax)
            .ToListAsync();

        var byCode = new Dictionary<string, NutrientTargetView>();
        foreach (var group in rows.GroupBy(t => t.Nutrient!.Code))
        {
            var perDay = group
                .Select(t => (t.Kind, Amount: ToPerDay(t, profile.WeightKg)))
                .Where(x => x.Amount is not null)
                .ToList();
            var lower = LowerPriority
                .Select(kind => perDay.FirstOrDefault(x => x.Kind == kind && x.Amount > 0m))
                .FirstOrDefault(x => x.Kind is not null);
            byCode[group.Key] = new NutrientTargetView(
                lower.Kind, lower.Amount,
                perDay.FirstOrDefault(x => x.Kind == "DG_MIN").Amount,
                perDay.FirstOrDefault(x => x.Kind == "DG_MAX").Amount,
                perDay.FirstOrDefault(x => x.Kind == "UL").Amount);
        }

        byCode[NutrientCodes.Energy] = byCode.GetValueOrDefault(NutrientCodes.Energy) is { } e
            ? e with { Kind = Calculated, Amount = effectiveKcal }
            : new NutrientTargetView(Calculated, effectiveKcal, null, null, null);
        byCode[NutrientCodes.Protein] = byCode.GetValueOrDefault(NutrientCodes.Protein) is { } p
            ? p with { Kind = Calculated, Amount = proteinG }
            : new NutrientTargetView(Calculated, proteinG, null, null, null);
        return new ResolvedTargets(effectiveKcal, proteinG, byCode);
    }

    private static decimal? ToPerDay(NutrientTarget t, decimal weightKg) => t.Basis switch
    {
        "PER_DAY" => t.Amount,
        "PER_KG_BW" => t.Amount * weightKg,
        _ => null,
    };
}
