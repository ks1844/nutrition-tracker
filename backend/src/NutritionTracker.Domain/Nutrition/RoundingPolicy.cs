namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 丸めの方針（docs/03.栄養計算仕様.md §5）。HALF_UP（四捨五入）で統一する。
/// 栄養値は非負なので、<see cref="MidpointRounding.AwayFromZero"/> が HALF_UP と等価。
/// 丸めは保存時に 1 回だけ行い、途中では丸めない。
/// </summary>
public static class RoundingPolicy
{
    /// <summary>
    /// 重量（g）を保存するときの小数の桁数（2 桁）。
    /// </summary>
    public const int GramDigits = 2;
    /// <summary>
    /// 食べた割合や達成率を保持するときの小数の桁数（6 桁）。
    /// </summary>
    public const int RatioDigits = 6;
    /// <summary>
    /// 記録時の栄養スナップショットを保存するときの小数の桁数（4 桁）。
    /// </summary>
    public const int SnapshotDigits = 4;

    /// <summary>
    /// 値を指定した小数の桁数に四捨五入（HALF_UP）します。
    /// </summary>
    /// <param name="value">丸める前の値（非負を想定）。</param>
    /// <param name="digits">残す小数の桁数。</param>
    /// <returns>丸めた値。</returns>
    public static decimal Round(decimal value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
}
