namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 栄養素 1 つ分の量と、その量が「未測定の食材を含む」かどうかの印を持つ値です（docs/03.栄養計算仕様.md §4）。
/// </summary>
/// <param name="Amount">栄養素の量（丸める前の値）。</param>
/// <param name="IsIncomplete">未測定の食材が含まれていて、実際より低めに出ている可能性があるとき true。</param>
public readonly record struct NutrientAmount(decimal Amount, bool IsIncomplete)
{
    /// <summary>
    /// 量が 0 で、未測定でもない値を返します（参考値の栄養素で、成分表に行がない食材に使います）。
    /// </summary>
    public static NutrientAmount Zero => new(0m, false);

    /// <summary>
    /// 2 つの栄養素の量を足し合わせます。
    /// 量は足し算、未測定の印は「どちらか一方でも未測定なら未測定」（OR）として引き継ぎます。
    /// </summary>
    /// <param name="other">足す相手の量。</param>
    /// <returns>合計の量と、引き継いだ未測定の印。</returns>
    public NutrientAmount Plus(NutrientAmount other) => new(Amount + other.Amount, IsIncomplete || other.IsIncomplete);
}
