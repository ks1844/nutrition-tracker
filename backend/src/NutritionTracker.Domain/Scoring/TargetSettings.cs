namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// 目標値の算出に使う係数です。すべて暫定値です（docs/06.スコアリング仕様.md §2、Q-4）。
/// 実際の値は設定ファイル（appsettings.json の Nutrition:Targets）から渡します。
/// </summary>
/// <param name="ActivityFactor">活動レベル（1〜4）→ 基礎代謝に掛ける活動係数。</param>
/// <param name="ProteinGPerKg">目的 → 体重 1kg あたりのタンパク質量（g）。</param>
public sealed record TargetSettings(
    IReadOnlyDictionary<int, decimal> ActivityFactor,
    IReadOnlyDictionary<Goal, decimal> ProteinGPerKg)
{
    /// <summary>
    /// 設定がないときに使う暫定の既定値です。
    /// </summary>
    public static TargetSettings Default { get; } = new(
        new Dictionary<int, decimal> { [1] = 1.2m, [2] = 1.375m, [3] = 1.55m, [4] = 1.725m },
        new Dictionary<Goal, decimal> { [Goal.Skin] = 1.0m, [Goal.Both] = 1.4m, [Goal.Muscle] = 1.6m });
}
