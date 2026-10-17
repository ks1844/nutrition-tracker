using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

public class MenuNutritionCalculatorTests
{
    /// <summary>メニュー『豚汁』: にんじん 2 本(=300g)、豚こま肉 400g、みそ 大さじ 4(=72g)</summary>
    public static IReadOnlyList<MenuLine> Tonjiru() =>
    [
        new(GoldenData.Carrot, UnitConverter.ToAmountG(2m, GoldenData.CarrotGramsPerUnit)),
        new(GoldenData.Pork, UnitConverter.ToAmountG(400m, null)),
        new(GoldenData.Miso, UnitConverter.ToAmountG(4m, GoldenData.MisoGramsPerUnit)),
    ];

    [Fact]
    public void AC_CALC_004_MenuTotal_SumsFoodsAndPropagatesIncomplete()
    {
        var total = MenuNutritionCalculator.Total(Tonjiru(), GoldenData.Codes);

        Assert.Equal(new NutrientAmount(91.34m, false), total[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(31m, true), total[NutrientCodes.VitaminC]); // みそが未測定 → 不完全
        Assert.Equal(new NutrientAmount(1930m, false), total[NutrientCodes.VitaminARae]);
    }

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
