namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 計算に使う食材のデータ。
/// <paramref name="Per100g"/> は栄養素コード → 可食部 100g あたり含有量。
/// <b>値が null、またはコードが無い = 未測定</b>（0 とは区別する）。Tr（微量）は 0。
/// </summary>
public sealed record FoodData(decimal RefuseRatePct, IReadOnlyDictionary<string, decimal?> Per100g);
