namespace NutritionTracker.Api.Entities;

/// <summary>
/// 食事記録（T食事記録）。ある日の 1 食分です。削除すると物理的に消え、中の項目と栄養スナップショットも一緒に消えます。
/// </summary>
public class MealRecord : AuditableEntity
{
    /// <summary>
    /// 食事記録の ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 記録した利用者の ID。
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 食べた日。
    /// </summary>
    public DateOnly EatenOn { get; set; }

    /// <summary>
    /// 食事の区分（breakfast 朝 / lunch 昼 / dinner 夕 / snack 間食）。
    /// </summary>
    public string MealType { get; set; } = "";

    /// <summary>
    /// メモ。
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// 記録した利用者。
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// この食事で食べたもの（メニューや食材）。
    /// </summary>
    public List<MealRecordItem> Items { get; set; } = [];
}
