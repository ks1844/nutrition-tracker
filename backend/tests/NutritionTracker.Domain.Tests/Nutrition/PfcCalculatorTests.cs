using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

public class PfcCalculatorTests
{
    [Fact]
    public void AC_CALC_012_PfcRatio_UsesFourNineFourKcalPerGram()
    {
        // P 62g(=248kcal)、F 40g(=360kcal)、C 180g(=720kcal)、計 1328kcal
        var r = PfcCalculator.Calculate(62m, 40m, 180m);

        Assert.Equal(18.7m, r.ProteinPct);
        Assert.Equal(27.1m, r.FatPct);
        Assert.Equal(54.2m, r.CarbPct);
        Assert.Equal((19, 27, 54), (r.ProteinDisplay, r.FatDisplay, r.CarbDisplay)); // 最大剰余法で合計 100
    }

    [Fact]
    public void ZeroDenominator_ReturnsZeros()
    {
        var r = PfcCalculator.Calculate(0m, 0m, 0m);

        Assert.Equal((0, 0, 0), (r.ProteinDisplay, r.FatDisplay, r.CarbDisplay));
    }
}
