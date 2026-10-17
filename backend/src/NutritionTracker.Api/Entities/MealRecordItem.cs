namespace NutritionTracker.Api.Entities;

/// <summary>
/// 食事記録で食べたもの 1 つ（T食事記録項目）。メニューか単品の食材のどちらか一方を持ちます。
/// </summary>
public class MealRecordItem : AuditableEntity
{
    /// <summary>
    /// 項目の ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 食事記録の ID。
    /// </summary>
    public long MealRecordId { get; set; }

    /// <summary>
    /// 食べたメニューの ID。単品の食材のときは null。
    /// </summary>
    public long? MenuId { get; set; }

    /// <summary>
    /// 食べた食材の ID。メニューのときは null。
    /// </summary>
    public long? FoodId { get; set; }

    /// <summary>
    /// 単位で入力したときの単位の ID（再表示用）。計算には使いません。
    /// </summary>
    public long? UnitId { get; set; }

    /// <summary>
    /// 入力方式（RATIO 割合 / SERVING 杯数 / GRAM g / UNIT 単位）。
    /// </summary>
    public string InputMode { get; set; } = "";

    /// <summary>
    /// 入力した値（小数 3 桁）。
    /// </summary>
    public decimal InputValue { get; set; }

    /// <summary>
    /// メニューを食べた割合（1 = 全量、小数 6 桁）。メニューの記録だけに入ります。
    /// </summary>
    public decimal? PortionRatio { get; set; }

    /// <summary>
    /// 食べた重さ（g）。単品の食材では必ず入り、メニューでは g 入力のときだけ入ります。
    /// </summary>
    public decimal? AmountG { get; set; }

    /// <summary>
    /// この項目を含む食事記録。
    /// </summary>
    public MealRecord? MealRecord { get; set; }

    /// <summary>
    /// 食べたメニュー。
    /// </summary>
    public Menu? Menu { get; set; }

    /// <summary>
    /// 食べた食材。
    /// </summary>
    public Food? Food { get; set; }

    /// <summary>
    /// 入力に使った単位。
    /// </summary>
    public FoodUnit? Unit { get; set; }

    /// <summary>
    /// 記録した時点の栄養量（スナップショット）。栄養素ごとに 1 行です。
    /// </summary>
    public List<MealRecordItemNutrient> Nutrients { get; set; } = [];
}
