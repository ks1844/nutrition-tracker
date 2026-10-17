namespace NutritionTracker.Api.Entities;

/// <summary>T栄養目標（食事摂取基準 2025 年版。数値は Q-2 で転記するまで空）。</summary>
public class NutrientTarget : AuditableEntity
{
    public long Id { get; set; }

    public short NutrientId { get; set; }

    /// <summary>male / female / all</summary>
    public string Sex { get; set; } = "";

    public short AgeMin { get; set; }

    public short AgeMax { get; set; }

    /// <summary>RDA / AI / DG_MIN / DG_MAX / UL</summary>
    public string Kind { get; set; } = "";

    public decimal Amount { get; set; }

    /// <summary>PER_DAY / PER_KG_BW / PCT_ENERGY</summary>
    public string Basis { get; set; } = "";

    public string? SourceNote { get; set; }

    public Nutrient? Nutrient { get; set; }
}
