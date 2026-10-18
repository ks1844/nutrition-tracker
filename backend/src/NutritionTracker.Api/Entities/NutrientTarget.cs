namespace NutritionTracker.Api.Entities;

/// <summary>
/// 栄養素ごとの目標（T栄養目標）。食事摂取基準を、性別・年齢の範囲ごとに持ちます。数値は転記が済むまで空です（Q-2）。
/// </summary>
public class NutrientTarget : AuditableEntity
{
    /// <summary>
    /// 目標の ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 栄養素の ID。
    /// </summary>
    public short NutrientId { get; set; }

    /// <summary>
    /// 対象の性別（male / female / all）。
    /// </summary>
    public string Sex { get; set; } = "";

    /// <summary>
    /// 対象の年齢の下限。
    /// </summary>
    public short AgeMin { get; set; }

    /// <summary>
    /// 対象の年齢の上限。
    /// </summary>
    public short AgeMax { get; set; }

    /// <summary>
    /// 目標の種類（RDA 推奨量 / AI 目安量 / DG_MIN 目標量の下限 / DG_MAX 目標量の上限 / UL 耐容上限量）。
    /// </summary>
    public string Kind { get; set; } = "";

    /// <summary>
    /// 目標の値。
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 値の基準（PER_DAY 1 日あたり / PER_KG_BW 体重 1kg あたり / PCT_ENERGY エネルギー比）。
    /// </summary>
    public string Basis { get; set; } = "";

    /// <summary>
    /// 出典の補足（表の名前やページなど）。
    /// </summary>
    public string? SourceNote { get; set; }

    /// <summary>
    /// 目標の栄養素。
    /// </summary>
    public Nutrient? Nutrient { get; set; }
}
