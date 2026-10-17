namespace NutritionTracker.Api.Entities;

/// <summary>T食事記録項目。メニューか単品食材のどちらか一方を持つ。</summary>
public class MealRecordItem : AuditableEntity
{
    public long Id { get; set; }

    public long MealRecordId { get; set; }

    public long? MenuId { get; set; }

    public long? FoodId { get; set; }

    /// <summary>UNIT 入力の再表示用（計算は <see cref="AmountG"/> が正）</summary>
    public long? UnitId { get; set; }

    /// <summary>RATIO / SERVING / GRAM / UNIT</summary>
    public string InputMode { get; set; } = "";

    public decimal InputValue { get; set; }

    /// <summary>メニュー記録のみ。小数 6 桁。</summary>
    public decimal? PortionRatio { get; set; }

    /// <summary>単品食材は必須。メニューの GRAM 入力は食べた g。</summary>
    public decimal? AmountG { get; set; }

    public MealRecord? MealRecord { get; set; }

    public Menu? Menu { get; set; }

    public Food? Food { get; set; }

    public FoodUnit? Unit { get; set; }

    public List<MealRecordItemNutrient> Nutrients { get; set; } = [];
}
