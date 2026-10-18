namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 「2 本」「大さじ 4」のような数量と単位を、重量（g、廃棄前）に換算します（docs/03.栄養計算仕様.md §2）。
/// </summary>
public static class UnitConverter
{
    /// <summary>
    /// 入力した数量を重量（g）に換算し、小数 2 桁に四捨五入して返します。
    /// g で入力したときは単位あたりの g を 1 として、そのままの重量になります。
    /// </summary>
    /// <param name="quantity">入力した数量（g 入力なら g、単位入力なら単位の数）。</param>
    /// <param name="gramsPerUnit">1 単位あたりの g（廃棄部込み）。g 入力のときは null。</param>
    /// <returns>換算した重量（g、廃棄前）。</returns>
    public static decimal ToAmountG(decimal quantity, decimal? gramsPerUnit) =>
        RoundingPolicy.Round(quantity * (gramsPerUnit ?? 1m), RoundingPolicy.GramDigits);
}
