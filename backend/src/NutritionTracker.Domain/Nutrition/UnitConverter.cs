namespace NutritionTracker.Domain.Nutrition;

/// <summary>数量×単位 → 重量 g（廃棄前）。docs/03.栄養計算仕様.md §2。</summary>
public static class UnitConverter
{
    /// <param name="quantity">入力した数量（g 入力なら g、単位入力なら単位数）。</param>
    /// <param name="gramsPerUnit">1 単位あたりの g（廃棄部込み）。g 入力のときは null。</param>
    public static decimal ToAmountG(decimal quantity, decimal? gramsPerUnit) =>
        RoundingPolicy.Round(quantity * (gramsPerUnit ?? 1m), RoundingPolicy.GramDigits);
}
