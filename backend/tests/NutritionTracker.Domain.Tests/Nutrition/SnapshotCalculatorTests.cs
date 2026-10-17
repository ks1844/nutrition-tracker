using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

public class SnapshotCalculatorTests
{
    private static NutrientTotals MenuTotal() => MenuNutritionCalculator.Total(MenuNutritionCalculatorTests.Tonjiru(), GoldenData.Codes);

    [Fact]
    public void AC_CALC_005_OneServing()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.125000m);

        Assert.Equal(11.4175m, s[NutrientCodes.Protein].Amount);
        Assert.Equal(new NutrientAmount(3.8750m, true), s[NutrientCodes.VitaminC]);
        Assert.Equal(241.2500m, s[NutrientCodes.VitaminARae].Amount);
        Assert.Equal(11.4m, SnapshotCalculator.ForDisplay(s[NutrientCodes.Protein].Amount, "g")); // 表示は小数 1 桁
    }

    [Fact]
    public void AC_CALC_006_OneAndHalfServing_RoundsHalfUpOnce()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.187500m);

        Assert.Equal(17.1263m, s[NutrientCodes.Protein].Amount); // 17.12625 → HALF_UP
        Assert.Equal(5.8125m, s[NutrientCodes.VitaminC].Amount);
        Assert.Equal(361.8750m, s[NutrientCodes.VitaminARae].Amount);
    }

    [Fact]
    public void AC_CALC_007_OneThird()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.333333m);

        Assert.Equal(30.4466m, s[NutrientCodes.Protein].Amount);
        Assert.Equal(10.3333m, s[NutrientCodes.VitaminC].Amount);
        Assert.Equal(643.3327m, s[NutrientCodes.VitaminARae].Amount);
        Assert.True(s[NutrientCodes.VitaminC].IsIncomplete);
    }

    [Fact]
    public void AC_CALC_009_Ratio025()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.250000m);

        Assert.Equal(22.8350m, s[NutrientCodes.Protein].Amount);
        Assert.Equal(7.7500m, s[NutrientCodes.VitaminC].Amount);
        Assert.Equal(482.5000m, s[NutrientCodes.VitaminARae].Amount);
    }

    [Fact]
    public void ForFood_RoundsToFourDecimals_AndKeepsIncomplete()
    {
        var food = FoodNutritionCalculator.Calculate(GoldenData.Miso, 18m, GoldenData.Codes);

        var s = SnapshotCalculator.ForFood(food);

        Assert.Equal(2.1600m, s[NutrientCodes.Protein].Amount);
        Assert.True(s[NutrientCodes.VitaminC].IsIncomplete);
    }

    [Theory]
    [InlineData(1704.4, "kcal", 1704)]
    [InlineData(11.4175, "g", 11.4)]
    public void ForDisplay_KcalIsInteger_OthersOneDecimal(double value, string unit, double expected)
    {
        Assert.Equal((decimal)expected, SnapshotCalculator.ForDisplay((decimal)value, unit));
    }
}
