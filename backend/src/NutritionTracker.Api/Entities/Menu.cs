namespace NutritionTracker.Api.Entities;

/// <summary>Tメニュー（作り置きの料理）。論理削除（<see cref="DeletedAt"/>）。</summary>
public class Menu : AuditableEntity
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Name { get; set; } = "";

    public decimal CookedTotalWeightG { get; set; }

    public decimal Servings { get; set; }

    public string ServingLabel { get; set; } = "お椀";

    public DateOnly? CookedOn { get; set; }

    public string? Memo { get; set; }

    /// <summary>null = 有効。削除しても過去の記録は残る（FR-026）。</summary>
    public DateTime? DeletedAt { get; set; }

    public User? User { get; set; }

    public List<MenuItem> Items { get; set; } = [];
}
