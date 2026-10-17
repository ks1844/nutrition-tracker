namespace NutritionTracker.Domain.Nutrition;

/// <summary>栄養素コード（docs/04.データモデル.md §5 が正本）。</summary>
public static class NutrientCodes
{
    public const string Energy = "ENERGY";
    public const string Protein = "PROT";
    public const string Fat = "FAT";
    public const string Carbohydrate = "CHO";
    public const string DietaryFiber = "FIBTG";
    public const string SaltEquivalent = "NACL_EQ";
    public const string Potassium = "K";
    public const string Calcium = "CA";
    public const string Magnesium = "MG";
    public const string Iron = "FE";
    public const string Zinc = "ZN";
    public const string VitaminARae = "VITA_RAE";
    public const string BetaCaroteneEquivalent = "CARTB_EQ";
    public const string VitaminD = "VITD";
    public const string VitaminE = "VITE_ATOC";
    public const string VitaminK = "VITK";
    public const string VitaminB1 = "VITB1";
    public const string VitaminB2 = "VITB2";
    public const string Niacin = "NIA";
    public const string VitaminB6 = "VITB6";
    public const string VitaminB12 = "VITB12";
    public const string Folate = "FOL";
    public const string VitaminC = "VITC";
    public const string Astaxanthin = "ASTX";

    /// <summary>表示順（T栄養素.表示順）に並べた全コード。</summary>
    public static readonly IReadOnlyList<string> All =
    [
        Energy, Protein, Fat, Carbohydrate, DietaryFiber, SaltEquivalent, Potassium, Calcium, Magnesium, Iron, Zinc,
        VitaminARae, BetaCaroteneEquivalent, VitaminD, VitaminE, VitaminK, VitaminB1, VitaminB2, Niacin, VitaminB6,
        VitaminB12, Folate, VitaminC, Astaxanthin,
    ];
}
