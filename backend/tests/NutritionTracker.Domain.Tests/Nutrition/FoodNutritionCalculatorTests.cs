using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

public class FoodNutritionCalculatorTests
{
    private static NutrientTotals Calc(FoodData food, decimal amountG) =>
        FoodNutritionCalculator.Calculate(food, amountG, GoldenData.Codes);

    [Fact]
    public void AC_CALC_001_Carrot_UsesEdiblePortion()
    {
        var t = Calc(GoldenData.Carrot, 300m); // 可食部 270g

        Assert.Equal(270m, FoodNutritionCalculator.EdibleG(GoldenData.Carrot, 300m));
        Assert.Equal(new NutrientAmount(2.7m, false), t[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(27m, false), t[NutrientCodes.VitaminC]);
        Assert.Equal(new NutrientAmount(1890m, false), t[NutrientCodes.VitaminARae]);
    }

    [Fact]
    public void AC_CALC_002_Pork_HasNoRefuse()
    {
        var t = Calc(GoldenData.Pork, 400m);

        Assert.Equal(new NutrientAmount(80m, false), t[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(4m, false), t[NutrientCodes.VitaminC]);
        Assert.Equal(new NutrientAmount(40m, false), t[NutrientCodes.VitaminARae]);
    }

    [Fact]
    public void AC_CALC_003_Miso_UnmeasuredIsZeroAndIncomplete()
    {
        var t = Calc(GoldenData.Miso, 72m);

        Assert.Equal(new NutrientAmount(8.64m, false), t[NutrientCodes.Protein]);
        Assert.Equal(new NutrientAmount(0m, true), t[NutrientCodes.VitaminC]); // 欠測: 0 + 不完全
        Assert.Equal(new NutrientAmount(0m, false), t[NutrientCodes.VitaminARae]); // 測定済みの 0
    }

    [Fact]
    public void MissingRow_IsTreatedAsUnmeasured()
    {
        var food = new FoodData(0m, new Dictionary<string, decimal?> { [NutrientCodes.Protein] = 10m });

        var t = FoodNutritionCalculator.Calculate(food, 100m, [NutrientCodes.Protein, NutrientCodes.Zinc]);

        Assert.Equal(new NutrientAmount(0m, true), t[NutrientCodes.Zinc]);
    }

    [Fact]
    public void AC_CALC_010_Trace_IsZeroAndNotIncomplete()
    {
        var food = new FoodData(0m, new Dictionary<string, decimal?> { [NutrientCodes.VitaminD] = 0m }); // Tr は 0

        var t = FoodNutritionCalculator.Calculate(food, 100m, [NutrientCodes.VitaminD]);

        Assert.Equal(new NutrientAmount(0m, false), t[NutrientCodes.VitaminD]);
    }

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
