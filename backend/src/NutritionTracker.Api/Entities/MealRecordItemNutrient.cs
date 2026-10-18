namespace NutritionTracker.Api.Entities;

/// <summary>
/// 記録した時点の栄養量（T食事記録項目栄養素）。マスタやメニューを後から変えても、過去の記録の値が変わらないよう、記録時に計算して保存します（docs/adr/0002）。丸めは保存時に小数 4 桁で 1 回だけ行います。
/// </summary>
public class MealRecordItemNutrient : AuditableEntity
{
    /// <summary>
    /// 記録項目の ID。
    /// </summary>
    public long MealRecordItemId { get; set; }

    /// <summary>
    /// 栄養素の ID。
    /// </summary>
    public short NutrientId { get; set; }

    /// <summary>
    /// 記録した栄養素の量。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 未測定の食材を含んでいて、実際より低めに出ている可能性があるか。
    /// </summary>
    public bool IsIncomplete { get; set; }

    /// <summary>
    /// この栄養量を持つ記録項目。
    /// </summary>
    public MealRecordItem? MealRecordItem { get; set; }

    /// <summary>
    /// この栄養量の栄養素。
    /// </summary>
    public Nutrient? Nutrient { get; set; }
}
