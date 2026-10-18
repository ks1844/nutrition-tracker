namespace NutritionTracker.Api.Entities;

/// <summary>
/// メニュー（Tメニュー）。作り置きの料理 1 つ分で、論理削除します。
/// </summary>
public class Menu : AuditableEntity
{
    /// <summary>
    /// メニューの ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// メニューを持つ利用者の ID。
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// メニューの名前（例: 豚汁）。
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 完成後の全体の重さ（g）。鍋ごとの重さから空鍋を引いた値で、食べた量から割合を求めるときの分母です。
    /// </summary>
    public decimal CookedTotalWeightG { get; set; }

    /// <summary>
    /// 全体を何杯分とするか。
    /// </summary>
    public decimal Servings { get; set; }

    /// <summary>
    /// 1 杯の呼び方（既定は「お椀」）。
    /// </summary>
    public string ServingLabel { get; set; } = "お椀";

    /// <summary>
    /// 作った日。
    /// </summary>
    public DateOnly? CookedOn { get; set; }

    /// <summary>
    /// メモ。
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// 削除した日時。null のときは有効です。削除しても過去の記録は残ります。
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// メニューを持つ利用者。
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// メニューを構成する食材。
    /// </summary>
    public List<MenuItem> Items { get; set; } = [];
}
