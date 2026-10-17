namespace NutritionTracker.Api.Entities;

/// <summary>T食事記録項目栄養素（栄養スナップショット。docs/adr/0002）。丸めは保存時に小数 4 桁 HALF_UP で 1 回だけ。</summary>
public class MealRecordItemNutrient : AuditableEntity
{
    public long MealRecordItemId { get; set; }

    public short NutrientId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>未測定の食材を含む（欠測の伝播）。</summary>
    public bool IsIncomplete { get; set; }

    public MealRecordItem? MealRecordItem { get; set; }

    public Nutrient? Nutrient { get; set; }
}
