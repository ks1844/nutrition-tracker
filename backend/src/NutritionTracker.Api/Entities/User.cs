namespace NutritionTracker.Api.Entities;

/// <summary>
/// 利用者（Tユーザー）。この版では固定の 1 名だけです（docs/adr/0005）。
/// </summary>
public class User : AuditableEntity
{
    /// <summary>
    /// 固定ユーザーの ID。認証を導入するまで、API は常にこのユーザーとして動きます。
    /// </summary>
    public const long FixedUserId = 1;

    /// <summary>
    /// 利用者の ID。
    /// </summary>
    public long Id { get; set; }
}
