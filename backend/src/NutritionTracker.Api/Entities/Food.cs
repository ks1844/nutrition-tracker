namespace NutritionTracker.Api.Entities;

/// <summary>
/// 食材のマスタ（T食材）。名前・読み仮名・廃棄率・出典などを持ちます。
/// </summary>
public class Food : AuditableEntity
{
    /// <summary>
    /// 食材の ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 食品コード。マスタ CSV を取り込むときのキーで、重複しません。
    /// </summary>
    public string FoodCode { get; set; } = "";

    /// <summary>
    /// 画面に表示する名前（例: にんじん）。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 成分表上の正式名称。ないときは null。
    /// </summary>
    public string? OfficialName { get; set; }

    /// <summary>
    /// 読み仮名（ひらがな）。検索に使います。
    /// </summary>
    public string NameKana { get; set; } = "";

    /// <summary>
    /// 食品群（例: 野菜類）。
    /// </summary>
    public string? FoodGroup { get; set; }

    /// <summary>
    /// 廃棄率（%）。皮・種・ヘタなど食べない部分の割合で、0 以上 100 未満です。
    /// </summary>
    public decimal RefuseRatePct { get; set; }

    /// <summary>
    /// 成分値の出典の版（どの成分表から取ったか）。
    /// </summary>
    public string SourceVersion { get; set; } = "";

    /// <summary>
    /// 有効かどうか。マスタから外れた食材は削除せず無効にします。無効な食材は新しく使えません。
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// この食材の別名（かな・カナ・漢字・通称）。
    /// </summary>
    public List<FoodAlias> Aliases { get; set; } = [];

    /// <summary>
    /// この食材の 100g あたりの栄養素ごとの含有量。
    /// </summary>
    public List<FoodNutrient> Nutrients { get; set; } = [];

    /// <summary>
    /// この食材の換算単位（本・大さじ など）。
    /// </summary>
    public List<FoodUnit> Units { get; set; } = [];
}
