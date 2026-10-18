using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// PFC 比（タンパク質・脂質・炭水化物のエネルギー比）の計算のテストです。
/// </summary>
public class PfcCalculatorTests
{
    /// <summary>
    /// PFC 比が 1g あたり 4・9・4kcal で求まり、表示用の整数の合計が 100 になることを確認します。
    /// P 62g（248kcal）・F 40g（360kcal）・C 180g（720kcal）は、18.7% / 27.1% / 54.2%、表示用は 19 / 27 / 54 です（AC-CALC-012）。
    /// </summary>
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

    /// <summary>
    /// すべて 0g で計算できないときは、割合がすべて 0 になることを確認します。
    /// </summary>
    [Fact]
    public void ZeroDenominator_ReturnsZeros()
    {
        var r = PfcCalculator.Calculate(0m, 0m, 0m);

        Assert.Equal((0, 0, 0), (r.ProteinDisplay, r.FatDisplay, r.CarbDisplay));
    }
}
