namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// 目標値の算出に使う係数。<b>すべて暫定</b>（docs/06.スコアリング仕様.md §2、Q-4）。
/// 実際の値は設定（appsettings.json の Nutrition:Targets）から渡す。
/// </summary>
public sealed record TargetSettings(
    IReadOnlyDictionary<int, decimal> ActivityFactor,
    IReadOnlyDictionary<Goal, decimal> ProteinGPerKg)
{
    public static TargetSettings Default { get; } = new(
        new Dictionary<int, decimal> { [1] = 1.2m, [2] = 1.375m, [3] = 1.55m, [4] = 1.725m },
        new Dictionary<Goal, decimal> { [Goal.Skin] = 1.0m, [Goal.Both] = 1.4m, [Goal.Muscle] = 1.6m });
}
