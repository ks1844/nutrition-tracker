namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 栄養素コードの一覧です（docs/04.データモデル.md §5 が正本）。API・DB・計算で、栄養素を指すキーとして使います。
/// </summary>
public static class NutrientCodes
{
    /// <summary>
    /// エネルギー（kcal）の栄養素コード。
    /// </summary>
    public const string Energy = "ENERGY";
    /// <summary>
    /// タンパク質の栄養素コード。
    /// </summary>
    public const string Protein = "PROT";
    /// <summary>
    /// 脂質の栄養素コード。
    /// </summary>
    public const string Fat = "FAT";
    /// <summary>
    /// 炭水化物の栄養素コード。
    /// </summary>
    public const string Carbohydrate = "CHO";
    /// <summary>
    /// 食物繊維総量の栄養素コード。
    /// </summary>
    public const string DietaryFiber = "FIBTG";
    /// <summary>
    /// 食塩相当量の栄養素コード。
    /// </summary>
    public const string SaltEquivalent = "NACL_EQ";
    /// <summary>
    /// カリウムの栄養素コード。
    /// </summary>
    public const string Potassium = "K";
    /// <summary>
    /// カルシウムの栄養素コード。
    /// </summary>
    public const string Calcium = "CA";
    /// <summary>
    /// マグネシウムの栄養素コード。
    /// </summary>
    public const string Magnesium = "MG";
    /// <summary>
    /// 鉄の栄養素コード。
    /// </summary>
    public const string Iron = "FE";
    /// <summary>
    /// 亜鉛の栄養素コード。
    /// </summary>
    public const string Zinc = "ZN";
    /// <summary>
    /// ビタミン A（レチノール活性当量）の栄養素コード。
    /// </summary>
    public const string VitaminARae = "VITA_RAE";
    /// <summary>
    /// β-カロテン当量の栄養素コード。
    /// </summary>
    public const string BetaCaroteneEquivalent = "CARTB_EQ";
    /// <summary>
    /// ビタミン Dの栄養素コード。
    /// </summary>
    public const string VitaminD = "VITD";
    /// <summary>
    /// ビタミン E（α-トコフェロール）の栄養素コード。
    /// </summary>
    public const string VitaminE = "VITE_ATOC";
    /// <summary>
    /// ビタミン Kの栄養素コード。
    /// </summary>
    public const string VitaminK = "VITK";
    /// <summary>
    /// ビタミン B1の栄養素コード。
    /// </summary>
    public const string VitaminB1 = "VITB1";
    /// <summary>
    /// ビタミン B2の栄養素コード。
    /// </summary>
    public const string VitaminB2 = "VITB2";
    /// <summary>
    /// ナイアシンの栄養素コード。
    /// </summary>
    public const string Niacin = "NIA";
    /// <summary>
    /// ビタミン B6の栄養素コード。
    /// </summary>
    public const string VitaminB6 = "VITB6";
    /// <summary>
    /// ビタミン B12の栄養素コード。
    /// </summary>
    public const string VitaminB12 = "VITB12";
    /// <summary>
    /// 葉酸の栄養素コード。
    /// </summary>
    public const string Folate = "FOL";
    /// <summary>
    /// ビタミン Cの栄養素コード。
    /// </summary>
    public const string VitaminC = "VITC";
    /// <summary>
    /// アスタキサンチン（参考値。スコアには使わない）の栄養素コード。
    /// </summary>
    public const string Astaxanthin = "ASTX";

    /// <summary>
    /// 画面に並べる順（T栄養素の表示順）の全 24 コード。
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Energy, Protein, Fat, Carbohydrate, DietaryFiber, SaltEquivalent, Potassium, Calcium, Magnesium, Iron, Zinc,
        VitaminARae, BetaCaroteneEquivalent, VitaminD, VitaminE, VitaminK, VitaminB1, VitaminB2, Niacin, VitaminB6,
        VitaminB12, Folate, VitaminC, Astaxanthin,
    ];
}
