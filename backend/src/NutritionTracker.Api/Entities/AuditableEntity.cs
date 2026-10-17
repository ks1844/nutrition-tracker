namespace NutritionTracker.Api.Entities;

/// <summary>全テーブル共通の監査列（作成日時・更新日時。docs/05.テーブル定義書.md §1.3）。DB 側の既定値で設定される。</summary>
public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
