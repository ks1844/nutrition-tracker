namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 栄養計算に使う食材 1 つ分のデータです。
/// 可食部 100g あたりの量が null、またはコードがないときは「未測定」で、0 とは区別します（Tr＝微量は 0）。
/// </summary>
/// <param name="RefuseRatePct">廃棄率（%）。皮・種・ヘタなど食べない部分の割合。</param>
/// <param name="Per100g">栄養素コード → 可食部 100g あたりの含有量。</param>
public sealed record FoodData(decimal RefuseRatePct, IReadOnlyDictionary<string, decimal?> Per100g);
