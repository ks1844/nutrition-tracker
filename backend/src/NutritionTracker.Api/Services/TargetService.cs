using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Entities;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 栄養素 1 つの目標です。1 日あたりの量に換算済みです。
/// </summary>
/// <param name="Kind">下限型の目標の種類（RDA > AI > DG_MIN の順に 1 つ選ぶ）。エネルギー・タンパク質は CALCULATED。</param>
/// <param name="Amount">下限型の目標量。</param>
/// <param name="Min">目標量の下限（DG_MIN）。</param>
/// <param name="Max">目標量の上限（DG_MAX）。</param>
/// <param name="Ul">耐容上限量（UL）。</param>
public sealed record NutrientTargetView(string? Kind, decimal? Amount, decimal? Min, decimal? Max, decimal? Ul)
{
    /// <summary>
    /// 超えると警告する上限（耐容上限量を優先し、なければ目標量の上限）。
    /// </summary>
    public decimal? Limit => Ul ?? Max;

    /// <summary>
    /// 下限型の目標か上限のどちらかがあるか。
    /// </summary>
    public bool HasAny => Amount is not null || Limit is not null;
}

/// <summary>
/// ある日付の時点で解決した目標の一式です。
/// </summary>
/// <param name="EnergyKcal">実際に使うエネルギー目標（kcal）。</param>
/// <param name="ProteinG">タンパク質の目標（g）。</param>
/// <param name="ByCode">栄養素コード → 目標。</param>
public sealed record ResolvedTargets(int EnergyKcal, decimal ProteinG, IReadOnlyDictionary<string, NutrientTargetView> ByCode)
{
    /// <summary>
    /// 下限型の目標量（コード → 量）。スコアの達成率に使います。
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Lower { get; } =
        ByCode.Where(p => p.Value.Amount is not null).ToDictionary(p => p.Key, p => p.Value.Amount!.Value);

    /// <summary>
    /// 栄養素の目標を返します。
    /// </summary>
    /// <param name="code">栄養素コード。</param>
    /// <returns>目標。なければ null。</returns>
    public NutrientTargetView? Get(string code) => ByCode.GetValueOrDefault(code);
}

/// <summary>
/// プロフィールと栄養目標のマスタから、ダッシュボードで使う目標を解決するサービスです。
/// </summary>
/// <param name="db">データベースへの窓口。</param>
/// <param name="profiles">エネルギー・タンパク質の目標の計算。</param>
public class TargetService(AppDbContext db, ProfileService profiles)
{
    /// <summary>
    /// プロフィールから算出した目標（エネルギー・タンパク質）の種類名。
    /// </summary>
    private const string Calculated = "CALCULATED";
    /// <summary>
    /// 下限型の目標が複数あるときに選ぶ優先順位。
    /// </summary>
    private static readonly string[] LowerPriority = ["RDA", "AI", "DG_MIN"];

    /// <summary>
    /// 指定した日の目標を解決します。
    /// エネルギー・タンパク質はプロフィールから、その他は栄養目標のマスタから、性別（または all）と年齢が合う行を引きます。
    /// 体重あたりの目標は体重を掛けて 1 日あたりにし、エネルギー比の目標は未対応のため目標なしとして扱います（暫定）。
    /// </summary>
    /// <param name="profile">プロフィール。</param>
    /// <param name="on">目標を求める日（年齢の基準日）。</param>
    /// <returns>その日の目標の一式。</returns>
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

    /// <summary>
    /// 栄養目標の値を 1 日あたりの量に換算します。
    /// </summary>
    /// <param name="t">栄養目標の 1 行。</param>
    /// <param name="weightKg">体重（kg）。</param>
    /// <returns>1 日あたりの量。エネルギー比など対応しない基準は null。</returns>
    private static decimal? ToPerDay(NutrientTarget t, decimal weightKg) => t.Basis switch
    {
        "PER_DAY" => t.Amount,
        "PER_KG_BW" => t.Amount * weightKg,
        _ => null,
    };
}
