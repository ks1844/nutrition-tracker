namespace NutritionTracker.Domain.Nutrition;

/// <summary>食材 1 つの栄養量（docs/03.栄養計算仕様.md §3, §4）。丸めない。</summary>
public static class FoodNutritionCalculator
{
    /// <summary>可食部重量 = 重量 ×（1 − 廃棄率/100）。</summary>
    public static decimal EdibleG(FoodData food, decimal amountG) => amountG * (1m - food.RefuseRatePct / 100m);

    /// <summary>
    /// 栄養量 = 可食部重量 ÷ 100 × 100g あたり含有量。未測定は 0 とし、<see cref="NutrientAmount.IsIncomplete"/> を立てる。
    /// </summary>
    /// <param name="codes">計算する栄養素コード。</param>
    /// <param name="referenceCodes">
    /// 参考値の栄養素（例: ASTX）。成分表に無い食材が多いため、<b>行が無くても未測定（不完全）として扱わない</b>。
    /// </param>
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
