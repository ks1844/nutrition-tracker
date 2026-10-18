namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 食事を記録した時点の栄養量（スナップショット）を作ります（docs/03.栄養計算仕様.md §5, §6、docs/adr/0002）。
/// 丸めは保存時のここで 1 回だけ（小数 4 桁、四捨五入）行い、途中では丸めません。
/// </summary>
public static class SnapshotCalculator
{
    /// <summary>
    /// メニューを食べたときの記録用の栄養量を作ります（メニュー全体の栄養量 × 食べた割合を小数 4 桁に丸める）。
    /// </summary>
    /// <param name="menuTotals">メニュー全体の栄養量（丸める前）。</param>
    /// <param name="portionRatio">食べた割合。</param>
    /// <returns>保存用の栄養素ごとの量。</returns>
    public static NutrientTotals ForMenu(NutrientTotals menuTotals, decimal portionRatio) =>
        Map(menuTotals, a => a.Amount * portionRatio);

    /// <summary>
    /// 単品の食材を食べたときの記録用の栄養量を作ります（食材の栄養量を小数 4 桁に丸めるだけで、割合は使いません）。
    /// </summary>
    /// <param name="foodTotals">食材の栄養量（丸める前）。</param>
    /// <returns>保存用の栄養素ごとの量。</returns>
    public static NutrientTotals ForFood(NutrientTotals foodTotals) => Map(foodTotals, a => a.Amount);

    /// <summary>
    /// 画面表示用に、保存済みの量を丸めます（kcal は整数、それ以外は小数 1 桁）。
    /// </summary>
    /// <param name="storedAmount">保存されている量（小数 4 桁）。</param>
    /// <param name="unit">栄養素の単位（"kcal" のとき整数にします）。</param>
    /// <returns>表示用に丸めた量。</returns>
    public static decimal ForDisplay(decimal storedAmount, string unit) =>
        RoundingPolicy.Round(storedAmount, unit == "kcal" ? 0 : 1);

    /// <summary>
    /// 各栄養素の量を指定の計算で求め、小数 4 桁に丸めた集合を作ります。未測定の印はそのまま引き継ぎます。
    /// </summary>
    /// <param name="source">元の栄養量。</param>
    /// <param name="amount">各栄養素の量から保存する値を求める計算。</param>
    /// <returns>丸めた栄養素ごとの量。</returns>
    private static NutrientTotals Map(NutrientTotals source, Func<NutrientAmount, decimal> amount) =>
        new(source.Select(p => KeyValuePair.Create(
            p.Key,
            new NutrientAmount(RoundingPolicy.Round(amount(p.Value), RoundingPolicy.SnapshotDigits), p.Value.IsIncomplete))));
}
