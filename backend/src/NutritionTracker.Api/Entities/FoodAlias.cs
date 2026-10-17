namespace NutritionTracker.Api.Entities;

/// <summary>
/// 食材の別名（T食材別名）。「人参」「ニンジン」など、検索でたどり着くための呼び名です。
/// </summary>
public class FoodAlias : AuditableEntity
{
    /// <summary>
    /// 別名がついている食材の ID。
    /// </summary>
    public long FoodId { get; set; }

    /// <summary>
    /// 別名。
    /// </summary>
    public string Alias { get; set; } = "";

    /// <summary>
    /// 別名がついている食材。
    /// </summary>
    public Food? Food { get; set; }
}
