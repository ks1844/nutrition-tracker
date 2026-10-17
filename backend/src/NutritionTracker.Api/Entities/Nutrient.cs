namespace NutritionTracker.Api.Entities;

/// <summary>T栄養素。</summary>
public class Nutrient : AuditableEntity
{
    public short Id { get; set; }

    public string Code { get; set; } = "";

    public string Name { get; set; } = "";

    public string Unit { get; set; } = "";

    public string Category { get; set; } = "";

    public bool IsReference { get; set; }

    public short DisplayOrder { get; set; }
}
