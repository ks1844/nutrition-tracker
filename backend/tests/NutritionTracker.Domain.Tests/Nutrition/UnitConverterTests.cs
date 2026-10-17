using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// 数量×単位から重量（g）への換算のテストです（docs/03.栄養計算仕様.md §2）。
/// </summary>
public class UnitConverterTests
{
    /// <summary>
    /// 単位で入力した数量が、1 単位あたりの重さを掛けた重量（g）になることを確認します。
    /// にんじん 2 本は 300g、みそ大さじ 4 は 72g になります（AC-CALC-008）。
    /// </summary>
    [Fact]
    public void AC_CALC_008_UnitInput_IsConvertedToGrams()
    {
        Assert.Equal(300.00m, UnitConverter.ToAmountG(2m, GoldenData.CarrotGramsPerUnit)); // にんじん 2 本
        Assert.Equal(72.00m, UnitConverter.ToAmountG(4m, GoldenData.MisoGramsPerUnit)); // みそ 大さじ 4
    }

    /// <summary>
    /// g で入力したときは換算せず、入力した重さがそのまま重量になることを確認します（400g → 400g）。
    /// </summary>
    [Fact]
    public void GramInput_IsUsedAsIs()
    {
        Assert.Equal(400m, UnitConverter.ToAmountG(400m, null));
    }

    /// <summary>
    /// 換算結果が小数 2 桁に四捨五入されることを確認します（0.125g → 0.13g）。
    /// </summary>
    [Fact]
    public void Result_IsRoundedHalfUpToTwoDecimals()
    {
        Assert.Equal(0.13m, UnitConverter.ToAmountG(0.125m, null));
    }
}
