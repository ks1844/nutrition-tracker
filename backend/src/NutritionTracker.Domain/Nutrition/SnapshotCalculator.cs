namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 記録時の栄養スナップショット（docs/03.栄養計算仕様.md §5, §6、docs/adr/0002）。
/// 丸めは保存時に<b>ここで 1 回だけ</b>（小数 4 桁 HALF_UP）。途中では丸めない。
/// </summary>
public static class SnapshotCalculator
{
    /// <summary>メニューの記録: メニュー合計 × 食べた割合 を小数 4 桁にする。</summary>
    public static NutrientTotals ForMenu(NutrientTotals menuTotals, decimal portionRatio) =>
        Map(menuTotals, a => a.Amount * portionRatio);

    /// <summary>単品食材の記録: 食材の栄養量（丸め前）を小数 4 桁にする（割合は使わない）。</summary>
    public static NutrientTotals ForFood(NutrientTotals foodTotals) => Map(foodTotals, a => a.Amount);

    /// <summary>表示用の丸め（保存値から作る）。kcal は整数、それ以外は小数 1 桁。</summary>
    public static decimal ForDisplay(decimal storedAmount, string unit) =>
        RoundingPolicy.Round(storedAmount, unit == "kcal" ? 0 : 1);

    private static NutrientTotals Map(NutrientTotals source, Func<NutrientAmount, decimal> amount) =>
        new(source.Select(p => KeyValuePair.Create(
            p.Key,
            new NutrientAmount(RoundingPolicy.Round(amount(p.Value), RoundingPolicy.SnapshotDigits), p.Value.IsIncomplete))));
}
