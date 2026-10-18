namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// メニューを構成する食材 1 行分です。
/// </summary>
/// <param name="Food">食材のデータ。</param>
/// <param name="AmountG">換算済みの廃棄前の重量（g）。</param>
public sealed record MenuLine(FoodData Food, decimal AmountG);

/// <summary>
/// メニュー全体の栄養量を、構成する食材の栄養量の合計として求めます（docs/03.栄養計算仕様.md §6）。丸めません。
/// </summary>
public static class MenuNutritionCalculator
{
    /// <summary>
    /// メニューの全食材の栄養量を計算して足し合わせます。
    /// </summary>
    /// <param name="lines">メニューの構成食材（食材と重量）。</param>
    /// <param name="codes">計算する栄養素コードの一覧。</param>
    /// <param name="referenceCodes">参考値の栄養素（行がなくても未測定にしない）。</param>
    /// <returns>メニュー全体の栄養素ごとの量（丸める前の値）。</returns>
    public static NutrientTotals Total(
        IEnumerable<MenuLine> lines,
        IReadOnlyCollection<string> codes,
        IReadOnlySet<string>? referenceCodes = null) =>
        NutrientTotals.Sum(lines.Select(l => FoodNutritionCalculator.Calculate(l.Food, l.AmountG, codes, referenceCodes)));
}
