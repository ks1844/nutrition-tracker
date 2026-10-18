using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// 食材 1 つの栄養量の計算のテストです（docs/03.栄養計算仕様.md §3, §4）。
/// </summary>
public class FoodNutritionCalculatorTests
{
    /// <summary>
    /// 正解データの栄養素コードで、食材の栄養量を計算します。
    /// </summary>
    /// <param name="food">食材のデータ。</param>
    /// <param name="amountG">廃棄前の重量（g）。</param>
    /// <returns>栄養素ごとの量。</returns>
    private static NutrientTotals Calc(FoodData food, decimal amountG) =>
        FoodNutritionCalculator.Calculate(food, amountG, GoldenData.Codes);

    /// <summary>
    /// 廃棄のある食材は、廃棄を除いた可食部で計算されることを確認します。
    /// にんじん 300g（廃棄率 10%）は可食部 270g で、タンパク質 2.7g・ビタミン C 27mg・ビタミン A 1890μg になります（AC-CALC-001）。
    /// </summary>
    [Fact]
    public void AC_CALC_001_Carrot_UsesEdiblePortion()
    {
        var t = Calc(GoldenData.Carrot, 300m); // 可食部 270g

        Assert.Equal(270m, FoodNutritionCalculator.EdibleG(GoldenData.Carrot, 300m));
        Assert.Equal(new NutrientAmount(2.7m, false), t[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(27m, false), t[NutrientCodes.VitaminC]);
        Assert.Equal(new NutrientAmount(1890m, false), t[NutrientCodes.VitaminARae]);
    }

    /// <summary>
    /// 廃棄のない食材は、重量そのままで計算されることを確認します。
    /// 豚こま肉 400g は、タンパク質 80g・ビタミン C 4mg・ビタミン A 40μg になります（AC-CALC-002）。
    /// </summary>
    [Fact]
    public void AC_CALC_002_Pork_HasNoRefuse()
    {
        var t = Calc(GoldenData.Pork, 400m);

        Assert.Equal(new NutrientAmount(80m, false), t[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(4m, false), t[NutrientCodes.VitaminC]);
        Assert.Equal(new NutrientAmount(40m, false), t[NutrientCodes.VitaminARae]);
    }

    /// <summary>
    /// 未測定の栄養素は 0 として扱い、未測定の印が立つことを確認します。
    /// みそ 72g のビタミン C（未測定）は 0 で未測定の印あり、ビタミン A（測定済みの 0）は未測定の印なしになります（AC-CALC-003）。
    /// </summary>
    [Fact]
    public void AC_CALC_003_Miso_UnmeasuredIsZeroAndIncomplete()
    {
        var t = Calc(GoldenData.Miso, 72m);

        Assert.Equal(new NutrientAmount(8.64m, false), t[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(0m, true), t[NutrientCodes.VitaminC]); // 欠測: 0 + 不完全
        Assert.Equal(new NutrientAmount(0m, false), t[NutrientCodes.VitaminARae]); // 測定済みの 0
    }

    /// <summary>
    /// 食材のデータにその栄養素の行がないときも、未測定（0 で未測定の印あり）として扱うことを確認します。
    /// </summary>
    [Fact]
    public void MissingRow_IsTreatedAsUnmeasured()
    {
        var food = new FoodData(0m, new Dictionary<string, decimal?> { [NutrientCodes.Protein] = 10m });

        var t = FoodNutritionCalculator.Calculate(food, 100m, [NutrientCodes.Protein, NutrientCodes.Zinc]);

        Assert.Equal(new NutrientAmount(0m, true), t[NutrientCodes.Zinc]);
    }

    /// <summary>
    /// 微量（Tr）は 0 として扱い、未測定の印は立たないことを確認します（AC-CALC-010）。
    /// </summary>
    [Fact]
    public void AC_CALC_010_Trace_IsZeroAndNotIncomplete()
    {
        var food = new FoodData(0m, new Dictionary<string, decimal?> { [NutrientCodes.VitaminD] = 0m }); // Tr は 0

        var t = FoodNutritionCalculator.Calculate(food, 100m, [NutrientCodes.VitaminD]);

        Assert.Equal(new NutrientAmount(0m, false), t[NutrientCodes.VitaminD]);
    }

    /// <summary>
    /// 単品の食材を食べたときの栄養量を確認します。
    /// にんじん 100g はタンパク質 0.9g・ビタミン C 9mg・ビタミン A 630μg、みそ 18g はタンパク質 2.16g で、ビタミン C は未測定です（AC-CALC-011）。
    /// </summary>
    [Fact]
    public void AC_CALC_011_SingleFood_Carrot100g_AndMiso18g()
    {
        var carrot = Calc(GoldenData.Carrot, 100m);
        Assert.Equal(0.9m, carrot[NutrientCodes.Protein].Amount);
        Assert.Equal(9m, carrot[NutrientCodes.VitaminC].Amount);
        Assert.Equal(630m, carrot[NutrientCodes.VitaminARae].Amount);

        var miso = Calc(GoldenData.Miso, 18m);
        Assert.Equal(2.16m, miso[NutrientCodes.Protein].Amount);
        Assert.Equal(new NutrientAmount(0m, true), miso[NutrientCodes.VitaminC]);
    }
}
