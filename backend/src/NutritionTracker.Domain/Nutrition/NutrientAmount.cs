namespace NutritionTracker.Domain.Nutrition;

/// <summary>栄養素 1 つの量。<paramref name="IsIncomplete"/> は「未測定の食材を含む」印（docs/03.栄養計算仕様.md §4）。</summary>
public readonly record struct NutrientAmount(decimal Amount, bool IsIncomplete)
{
    public static NutrientAmount Zero => new(0m, false);

    /// <summary>合算。<see cref="IsIncomplete"/> は OR で伝播する。</summary>
    public NutrientAmount Plus(NutrientAmount other) => new(Amount + other.Amount, IsIncomplete || other.IsIncomplete);
}
