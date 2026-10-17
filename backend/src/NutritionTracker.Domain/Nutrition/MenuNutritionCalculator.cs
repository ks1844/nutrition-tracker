namespace NutritionTracker.Domain.Nutrition;

/// <summary>メニューの構成食材 1 行。<paramref name="AmountG"/> は換算済みの廃棄前重量（docs/04.データモデル.md の重量g）。</summary>
public sealed record MenuLine(FoodData Food, decimal AmountG);

/// <summary>メニュー合計 = 食材の栄養量の合計（docs/03.栄養計算仕様.md §6）。丸めない。</summary>
public static class MenuNutritionCalculator
{
    public static NutrientTotals Total(
        IEnumerable<MenuLine> lines,
        IReadOnlyCollection<string> codes,
        IReadOnlySet<string>? referenceCodes = null) =>
        NutrientTotals.Sum(lines.Select(l => FoodNutritionCalculator.Calculate(l.Food, l.AmountG, codes, referenceCodes)));
}
