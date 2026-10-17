namespace NutritionTracker.Api.Entities;

/// <summary>T食材単位。1 単位あたりの g は廃棄部込み。</summary>
public class FoodUnit : AuditableEntity
{
    public long Id { get; set; }

    public long FoodId { get; set; }

    public string UnitName { get; set; } = "";

    public decimal GramsPerUnit { get; set; }

    public bool IsDefault { get; set; }

    public Food? Food { get; set; }
}
