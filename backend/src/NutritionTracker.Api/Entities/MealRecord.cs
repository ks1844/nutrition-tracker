namespace NutritionTracker.Api.Entities;

/// <summary>T食事記録（1 食）。利用者の削除操作で物理削除し、子（項目・スナップショット）はカスケード削除する。</summary>
public class MealRecord : AuditableEntity
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public DateOnly EatenOn { get; set; }

    /// <summary>breakfast / lunch / dinner / snack</summary>
    public string MealType { get; set; } = "";

    public string? Memo { get; set; }

    public User? User { get; set; }

    public List<MealRecordItem> Items { get; set; } = [];
}
