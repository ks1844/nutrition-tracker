using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// 正解データ（docs/03.栄養計算仕様.md §9）。<b>テスト用ダミー値で、実際の成分表の値ではない。</b>
/// </summary>
public static class GoldenData
{
    public static readonly string[] Codes = [NutrientCodes.Protein, NutrientCodes.VitaminC, NutrientCodes.VitaminARae];

    /// <summary>F1 にんじん（廃棄率 10.0%。1 本 = 150g）</summary>
    public static readonly FoodData Carrot = new(10.0m, new Dictionary<string, decimal?>
    {
        [NutrientCodes.Protein] = 1.0m, [NutrientCodes.VitaminC] = 10m, [NutrientCodes.VitaminARae] = 700m,
    });

    /// <summary>F2 豚こま肉（廃棄率 0）</summary>
    public static readonly FoodData Pork = new(0m, new Dictionary<string, decimal?>
    {
        [NutrientCodes.Protein] = 20.0m, [NutrientCodes.VitaminC] = 1m, [NutrientCodes.VitaminARae] = 10m,
    });

    /// <summary>F3 みそ（廃棄率 0。ビタミン C は未測定。大さじ = 18g）</summary>
    public static readonly FoodData Miso = new(0m, new Dictionary<string, decimal?>
    {
        [NutrientCodes.Protein] = 12.0m, [NutrientCodes.VitaminC] = null, [NutrientCodes.VitaminARae] = 0m,
    });

    public const decimal CarrotGramsPerUnit = 150m;
    public const decimal MisoGramsPerUnit = 18m;

    // メニュー『豚汁』: にんじん 2 本、豚こま肉 400g、みそ 大さじ 4。完成後重量 2400g、8 杯分
    public const decimal CookedTotalWeightG = 2400m;
    public const decimal Servings = 8m;
}
