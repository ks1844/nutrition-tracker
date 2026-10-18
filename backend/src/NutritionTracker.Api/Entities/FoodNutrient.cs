namespace NutritionTracker.Api.Entities;

/// <summary>
/// 食材の栄養素ごとの含有量（T食材栄養素）。含有量が null のときは「未測定」で、0 とは区別します。
/// </summary>
public class FoodNutrient : AuditableEntity
{
    /// <summary>
    /// 食材の ID。
    /// </summary>
    public long FoodId { get; set; }

    /// <summary>
    /// 栄養素の ID。
    /// </summary>
    public short NutrientId { get; set; }

    /// <summary>
    /// 可食部 100g あたりの含有量。null は未測定。
    /// </summary>
    public decimal? AmountPer100g { get; set; }

    /// <summary>
    /// 成分表で微量（Tr）とされた値かどうか。微量の含有量は 0 で、未測定とは別です。
    /// </summary>
    public bool IsTrace { get; set; }

    /// <summary>
    /// 出典の補足（ページや脚注など）。
    /// </summary>
    public string? SourceNote { get; set; }

    /// <summary>
    /// この含有量を持つ食材。
    /// </summary>
    public Food? Food { get; set; }

    /// <summary>
    /// この含有量の栄養素。
    /// </summary>
    public Nutrient? Nutrient { get; set; }
}
