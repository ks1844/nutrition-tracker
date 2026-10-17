namespace NutritionTracker.Api.Entities;

/// <summary>
/// 栄養素のマスタ（T栄養素）。エネルギー・タンパク質・ビタミンなど 24 種の名前・単位・区分を持ちます。
/// </summary>
public class Nutrient : AuditableEntity
{
    /// <summary>
    /// 栄養素の ID。
    /// </summary>
    public short Id { get; set; }

    /// <summary>
    /// 栄養素コード（例: PROT、VITC）。API や計算で栄養素を指すキーです。
    /// </summary>
    public string Code { get; set; } = "";

    /// <summary>
    /// 栄養素の日本語名（例: タンパク質）。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 量の単位（例: g、mg、μg、kcal）。
    /// </summary>
    public string Unit { get; set; } = "";

    /// <summary>
    /// 栄養素の区分（エネルギー・ミネラル・ビタミンなど）。
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// 参考値の栄養素かどうか（例: アスタキサンチン）。スコアには使わず、未測定（不完全）の印も立てません。
    /// </summary>
    public bool IsReference { get; set; }

    /// <summary>
    /// 画面に並べる順番。
    /// </summary>
    public short DisplayOrder { get; set; }
}
