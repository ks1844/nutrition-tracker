namespace NutritionTracker.Api.Entities;

/// <summary>T食材別名。</summary>
public class FoodAlias : AuditableEntity
{
    public long FoodId { get; set; }

    public string Alias { get; set; } = "";

    public Food? Food { get; set; }
}
