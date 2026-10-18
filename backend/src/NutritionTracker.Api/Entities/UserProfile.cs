namespace NutritionTracker.Api.Entities;

/// <summary>
/// 利用者のプロフィール（Tユーザープロフィール）。性別・生年月日・身長・体重・活動レベル・目的を持ち、1 ユーザーにつき 1 行です。
/// </summary>
public class UserProfile : AuditableEntity
{
    /// <summary>
    /// 利用者の ID。
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 性別（male / female）。
    /// </summary>
    public string Sex { get; set; } = "";

    /// <summary>
    /// 生年月日。
    /// </summary>
    public DateOnly BirthDate { get; set; }

    /// <summary>
    /// 身長（cm）。
    /// </summary>
    public decimal HeightCm { get; set; }

    /// <summary>
    /// 体重（kg）。
    /// </summary>
    public decimal WeightKg { get; set; }

    /// <summary>
    /// 活動レベル（1〜4）。エネルギー目標の活動係数が決まります。
    /// </summary>
    public short ActivityLevel { get; set; }

    /// <summary>
    /// 目的（skin / muscle / both）。タンパク質目標の係数が決まります。
    /// </summary>
    public string Goal { get; set; } = "";

    /// <summary>
    /// 利用者が手入力したエネルギー目標（kcal）。null のときは自動算出の値を使います。
    /// </summary>
    public int? EnergyTargetKcal { get; set; }

    /// <summary>
    /// このプロフィールの利用者。
    /// </summary>
    public User? User { get; set; }
}
