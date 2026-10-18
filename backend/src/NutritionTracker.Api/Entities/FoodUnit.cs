namespace NutritionTracker.Api.Entities;

/// <summary>
/// 食材の換算単位（T食材単位）。「にんじん 1 本 = 150g」のように、単位を重さに直すための情報です。
/// </summary>
public class FoodUnit : AuditableEntity
{
    /// <summary>
    /// 換算単位の ID。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 食材の ID。
    /// </summary>
    public long FoodId { get; set; }

    /// <summary>
    /// 単位の名前（例: 本、大さじ）。同じ食材の中で重複しません。
    /// </summary>
    public string UnitName { get; set; } = "";

    /// <summary>
    /// 1 単位あたりの重さ（g）。皮や種などを含む、購入時の重さです。
    /// </summary>
    public decimal GramsPerUnit { get; set; }

    /// <summary>
    /// その食材の既定の単位かどうか。1 食材につき最大 1 つです。
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// この単位を持つ食材。
    /// </summary>
    public Food? Food { get; set; }
}
