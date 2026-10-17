namespace NutritionTracker.Api.Entities;

/// <summary>Tユーザープロフィール。1 ユーザー 1 行。</summary>
public class UserProfile : AuditableEntity
{
    public long UserId { get; set; }

    /// <summary>male / female</summary>
    public string Sex { get; set; } = "";

    public DateOnly BirthDate { get; set; }

    public decimal HeightCm { get; set; }

    public decimal WeightKg { get; set; }

    /// <summary>1〜4</summary>
    public short ActivityLevel { get; set; }

    /// <summary>skin / muscle / both</summary>
    public string Goal { get; set; } = "";

    /// <summary>null = 自動算出を使う（手動上書き: FR-002）</summary>
    public int? EnergyTargetKcal { get; set; }

    public User? User { get; set; }
}
