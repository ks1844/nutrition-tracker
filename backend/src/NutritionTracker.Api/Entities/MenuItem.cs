namespace NutritionTracker.Api.Entities;

/// <summary>Tメニュー食材。<see cref="AmountG"/> は換算済みの廃棄前重量で、計算の正。</summary>
public class MenuItem : AuditableEntity
{
    public long Id { get; set; }

    public long MenuId { get; set; }

    public long FoodId { get; set; }

    /// <summary>null = g 入力</summary>
    public long? UnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal AmountG { get; set; }

    public int SortOrder { get; set; }

    public Menu? Menu { get; set; }

    public Food? Food { get; set; }

    public FoodUnit? Unit { get; set; }
}
