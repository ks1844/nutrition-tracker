namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 食材 1 つを食べた重さから、栄養素ごとの量を計算します（docs/03.栄養計算仕様.md §3, §4）。
/// 途中では丸めず、丸めは記録を保存するときに 1 回だけ行います。
/// </summary>
public static class FoodNutritionCalculator
{
    /// <summary>
    /// 廃棄する部分を除いた、食べられる部分の重量を求めます（重量 ×（1 − 廃棄率 ÷ 100））。
    /// </summary>
    /// <param name="food">食材のデータ（廃棄率を使います）。</param>
    /// <param name="amountG">廃棄前の重量（g）。</param>
    /// <returns>可食部の重量（g）。</returns>
    public static decimal EdibleG(FoodData food, decimal amountG) => amountG * (1m - food.RefuseRatePct / 100m);

    /// <summary>
    /// 食材の重量から、指定した栄養素ごとの量（可食部重量 ÷ 100 × 100g あたり含有量）を計算します。
    /// 未測定の栄養素は量 0 とし、未測定の印（IsIncomplete）を立てます。
    /// </summary>
    /// <param name="food">食材のデータ（廃棄率と 100g あたりの含有量）。</param>
    /// <param name="amountG">廃棄前の重量（g）。</param>
    /// <param name="codes">計算する栄養素コードの一覧。</param>
    /// <param name="referenceCodes">
    /// 参考値の栄養素（例: ASTX）。成分表に載っていない食材が多いため、行がなくても未測定（不完全）にしません。
    /// </param>
    /// <returns>栄養素コードごとの量（丸める前の値）。</returns>
    public static NutrientTotals Calculate(
        FoodData food,
        decimal amountG,
        IEnumerable<string> codes,
        IReadOnlySet<string>? referenceCodes = null)
    {
        var edible = EdibleG(food, amountG);
        var result = new Dictionary<string, NutrientAmount>();
        foreach (var code in codes)
        {
            if (food.Per100g.TryGetValue(code, out var per100g) && per100g is { } value)
            {
                result[code] = new NutrientAmount(edible / 100m * value, false);
            }
            else if (referenceCodes is not null && referenceCodes.Contains(code) && !food.Per100g.ContainsKey(code))
            {
                result[code] = NutrientAmount.Zero;
            }
            else
            {
                result[code] = new NutrientAmount(0m, true);
            }
        }
        return new NutrientTotals(result);
    }
}
