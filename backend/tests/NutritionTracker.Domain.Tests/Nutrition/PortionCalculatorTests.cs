using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

public class PortionCalculatorTests
{
    private const decimal Cooked = GoldenData.CookedTotalWeightG;
    private const decimal Servings = GoldenData.Servings;

    [Theory]
    [InlineData(InputMode.Serving, 1, "0.125000")] // AC-CALC-005（お椀 1 杯）
    [InlineData(InputMode.Gram, 300, "0.125000")] // AC-CALC-005（300g）
    [InlineData(InputMode.Serving, 1.5, "0.187500")] // AC-CALC-006
    [InlineData(InputMode.Gram, 800, "0.333333")] // AC-CALC-007（800÷2400 を小数 6 桁）
    [InlineData(InputMode.Ratio, 0.25, "0.250000")] // AC-CALC-009
    public void Ratio_IsNormalizedToSixDecimals(InputMode mode, double value, string expected)
    {
        var ratio = PortionCalculator.Ratio(mode, (decimal)value, Cooked, Servings);

        Assert.Equal(decimal.Parse(expected), ratio);
    }

    [Fact]
    public void UnitMode_IsNotAllowedForMenu()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PortionCalculator.Ratio(InputMode.Unit, 1m, Cooked, Servings));
    }

    [Fact]
    public void AC_CALC_013_Remaining_IsOneMinusSumOfRatios()
    {
        var remaining = PortionCalculator.Remaining([0.1875m, 0.125m], Servings);

        Assert.Equal(0.6875m, remaining.Ratio);
        Assert.Equal(5.5m, remaining.Servings);
        Assert.False(remaining.IsOver);
    }

    [Fact]
    public void Remaining_IsOver_WhenRecordedMoreThanWhole()
    {
        var remaining = PortionCalculator.Remaining([0.75m, 0.5m], Servings);

        Assert.True(remaining.IsOver);
        Assert.Equal(-0.25m, remaining.Ratio);
    }

    [Fact]
    public void ServingWeight_IsCookedWeightDividedByServings()
    {
        Assert.Equal(300.0m, PortionCalculator.ServingWeightG(Cooked, Servings));
    }
}
