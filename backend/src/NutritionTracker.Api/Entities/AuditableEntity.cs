namespace NutritionTracker.Api.Entities;

/// <summary>
/// 全テーブル共通の監査列（作成日時・更新日時）を持つ基底クラスです（docs/05.テーブル定義書.md §1.3）。値は DB 側の既定値で設定されます。
/// </summary>
public abstract class AuditableEntity
{
    /// <summary>
    /// 行を作成した日時。
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 行を最後に更新した日時。
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
