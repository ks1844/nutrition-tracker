using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// 計算のテストで使う正解データです（docs/03.栄養計算仕様.md §9）。
/// <b>テスト用のダミー値で、実際の成分表の値ではありません。</b>
/// </summary>
public static class GoldenData
{
    /// <summary>
    /// 計算対象にする栄養素コード（タンパク質・ビタミン C・ビタミン A）。
    /// </summary>
    public static readonly string[] Codes = [NutrientCodes.Protein, NutrientCodes.VitaminC, NutrientCodes.VitaminARae];

    /// <summary>
    /// にんじん（廃棄率 10.0%、1 本 = 150g）。
    /// </summary>
    public static readonly FoodData Carrot = new(10.0m, new Dictionary<string, decimal?>
    {
        [NutrientCodes.Protein] = 1.0m, [NutrientCodes.VitaminC] = 10m, [NutrientCodes.VitaminARae] = 700m,
    });

    /// <summary>
    /// 豚こま肉（廃棄率 0）。
    /// </summary>
    public static readonly FoodData Pork = new(0m, new Dictionary<string, decimal?>
    {
        [NutrientCodes.Protein] = 20.0m, [NutrientCodes.VitaminC] = 1m, [NutrientCodes.VitaminARae] = 10m,
    });

    /// <summary>
    /// みそ（廃棄率 0、ビタミン C は未測定、大さじ 1 = 18g）。
    /// </summary>
    public static readonly FoodData Miso = new(0m, new Dictionary<string, decimal?>
    {
        [NutrientCodes.Protein] = 12.0m, [NutrientCodes.VitaminC] = null, [NutrientCodes.VitaminARae] = 0m,
    });

    /// <summary>
    /// にんじん 1 本あたりの重さ（g）。
    /// </summary>
    public const decimal CarrotGramsPerUnit = 150m;
    /// <summary>
    /// みそ大さじ 1 あたりの重さ（g）。
    /// </summary>
    public const decimal MisoGramsPerUnit = 18m;

    // メニュー『豚汁』: にんじん 2 本、豚こま肉 400g、みそ 大さじ 4。完成後重量 2400g、8 杯分
    /// <summary>
    /// メニュー「豚汁」の完成後の重さ（g）。にんじん 2 本・豚こま肉 400g・みそ大さじ 4 で作り、8 杯分。
    /// </summary>
    public const decimal CookedTotalWeightG = 2400m;
    /// <summary>
    /// メニュー「豚汁」の杯数。
    /// </summary>
    public const decimal Servings = 8m;
}
