namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 丸めの方針（docs/03.栄養計算仕様.md §5）。HALF_UP（四捨五入）で統一する。
/// 栄養値は非負なので、<see cref="MidpointRounding.AwayFromZero"/> が HALF_UP と等価。
/// 丸めは保存時に 1 回だけ行い、途中では丸めない。
/// </summary>
public static class RoundingPolicy
{
    public const int GramDigits = 2;
    public const int RatioDigits = 6;
    public const int SnapshotDigits = 4;

    public static decimal Round(decimal value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
}
