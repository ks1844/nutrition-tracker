namespace NutritionTracker.Api.Entities;

/// <summary>T食材栄養素。<see cref="AmountPer100g"/> が null = 未測定（0 とは区別）。</summary>
public class FoodNutrient : AuditableEntity
{
    public long FoodId { get; set; }

    public short NutrientId { get; set; }

    public decimal? AmountPer100g { get; set; }

    public bool IsTrace { get; set; }

    public string? SourceNote { get; set; }

    public Food? Food { get; set; }

    public Nutrient? Nutrient { get; set; }
}
