using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

public class UnitConverterTests
{
    [Fact]
    public void AC_CALC_008_UnitInput_IsConvertedToGrams()
    {
        Assert.Equal(300.00m, UnitConverter.ToAmountG(2m, GoldenData.CarrotGramsPerUnit)); // にんじん 2 本
        Assert.Equal(72.00m, UnitConverter.ToAmountG(4m, GoldenData.MisoGramsPerUnit)); // みそ 大さじ 4
    }

    [Fact]
    public void GramInput_IsUsedAsIs()
    {
        Assert.Equal(400m, UnitConverter.ToAmountG(400m, null));
    }

    [Fact]
    public void Result_IsRoundedHalfUpToTwoDecimals()
    {
        Assert.Equal(0.13m, UnitConverter.ToAmountG(0.125m, null));
    }
}
