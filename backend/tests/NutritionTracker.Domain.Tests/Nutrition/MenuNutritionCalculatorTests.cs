using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// メニュー全体の栄養量の計算のテストです（docs/03.栄養計算仕様.md §6）。
/// </summary>
public class MenuNutritionCalculatorTests
{
    /// <summary>
    /// メニュー「豚汁」の構成食材を作ります。
    /// にんじん 2 本（300g）、豚こま肉 400g、みそ大さじ 4（72g）です。
    /// </summary>
    /// <returns>豚汁の構成食材（食材と重量）。</returns>
    public static IReadOnlyList<MenuLine> Tonjiru() =>
    [
        new(GoldenData.Carrot, UnitConverter.ToAmountG(2m, GoldenData.CarrotGramsPerUnit)),
        new(GoldenData.Pork, UnitConverter.ToAmountG(400m, null)),
        new(GoldenData.Miso, UnitConverter.ToAmountG(4m, GoldenData.MisoGramsPerUnit)),
    ];

    /// <summary>
    /// メニューの栄養量が構成食材の合計になり、未測定の印が引き継がれることを確認します。
    /// 豚汁はタンパク質 91.34g、ビタミン A 1930μg、ビタミン C 31mg（みそが未測定のため未測定の印あり）になります（AC-CALC-004）。
    /// </summary>
    [Fact]
    public void AC_CALC_004_MenuTotal_SumsFoodsAndPropagatesIncomplete()
    {
        var total = MenuNutritionCalculator.Total(Tonjiru(), GoldenData.Codes);

        Assert.Equal(new NutrientAmount(91.34m, false), total[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(31m, true), total[NutrientCodes.VitaminC]); // みそが未測定 → 不完全
        Assert.Equal(new NutrientAmount(1930m, false), total[NutrientCodes.VitaminARae]);
    }

    /// <summary>
    /// 参考値の栄養素（アスタキサンチン）は、行のない食材があっても未測定の印が立たないことを確認します。
    /// </summary>
    [Fact]
    public void ReferenceNutrient_WithoutRow_IsNotIncomplete()
    {
        var withRow = new FoodData(0m, new Dictionary<string, decimal?> { [NutrientCodes.Astaxanthin] = 3m });
        var withoutRow = new FoodData(0m, new Dictionary<string, decimal?>());
        IReadOnlySet<string> reference = new HashSet<string> { NutrientCodes.Astaxanthin };

        var total = MenuNutritionCalculator.Total(
            [new MenuLine(withRow, 100m), new MenuLine(withoutRow, 100m)], [NutrientCodes.Astaxanthin], reference);

        Assert.Equal(new NutrientAmount(3m, false), total[NutrientCodes.Astaxanthin]);
    }
}
