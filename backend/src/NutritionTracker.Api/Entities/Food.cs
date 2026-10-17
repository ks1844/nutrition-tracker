namespace NutritionTracker.Api.Entities;

/// <summary>T食材。</summary>
public class Food : AuditableEntity
{
    public long Id { get; set; }

    public string FoodCode { get; set; } = "";

    public string Name { get; set; } = "";

    public string? OfficialName { get; set; }

    public string NameKana { get; set; } = "";

    public string? FoodGroup { get; set; }

    public decimal RefuseRatePct { get; set; }

    public string SourceVersion { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public List<FoodAlias> Aliases { get; set; } = [];

    public List<FoodNutrient> Nutrients { get; set; } = [];

    public List<FoodUnit> Units { get; set; } = [];
}
