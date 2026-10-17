namespace NutritionTracker.Api.Entities;

/// <summary>Tユーザー。MVP は固定 1 名（ID=1。D-1、docs/adr/0005）。</summary>
public class User : AuditableEntity
{
    /// <summary>固定ユーザーの ID。認証を導入するまで、API は常にこのユーザーに解決する。</summary>
    public const long FixedUserId = 1;

    public long Id { get; set; }
}
