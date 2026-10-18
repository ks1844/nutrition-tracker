namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// 性別。
/// </summary>
public enum Sex
{
    /// <summary>
    /// 男性。
    /// </summary>
    Male,
    /// <summary>
    /// 女性。
    /// </summary>
    Female,
}

/// <summary>
/// 利用者の目的（docs/05.テーブル定義書.md §4.3）。タンパク質の目標量の係数が変わります。
/// </summary>
public enum Goal
{
    /// <summary>
    /// 美肌を重視する。
    /// </summary>
    Skin,
    /// <summary>
    /// 筋肉づくりを重視する。
    /// </summary>
    Muscle,
    /// <summary>
    /// 美肌と筋肉づくりの両方を重視する。
    /// </summary>
    Both,
}
