namespace NutritionTracker.Api.Entities;

/// <summary>
/// メニューを構成する食材 1 行（Tメニュー食材）。
/// </summary>
public class MenuItem : AuditableEntity
{
    /// <summary>
    /// メニュー食材の ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// メニューの ID。
    /// </summary>
    public long MenuId { get; set; }

    /// <summary>
    /// 食材の ID。
    /// </summary>
    public long FoodId { get; set; }

    /// <summary>
    /// 入力に使った単位の ID。g で入力したときは null。
    /// </summary>
    public long? UnitId { get; set; }

    /// <summary>
    /// 入力した数量（g 入力なら g、単位入力なら単位の数）。
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// 換算済みの廃棄前の重さ（g）。栄養計算の元になる値です。
    /// </summary>
    public decimal AmountG { get; set; }

    /// <summary>
    /// メニュー内での並び順。
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// この食材を含むメニュー。
    /// </summary>
    public Menu? Menu { get; set; }

    /// <summary>
    /// 使っている食材。
    /// </summary>
    public Food? Food { get; set; }

    /// <summary>
    /// 入力に使った単位。
    /// </summary>
    public FoodUnit? Unit { get; set; }
}
