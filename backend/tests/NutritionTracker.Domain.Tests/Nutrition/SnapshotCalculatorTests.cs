using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// 記録時の栄養スナップショットの計算のテストです（docs/03.栄養計算仕様.md §5, §6）。
/// </summary>
public class SnapshotCalculatorTests
{
    /// <summary>
    /// メニュー「豚汁」全体の栄養量を作ります。
    /// </summary>
    /// <returns>豚汁の栄養素ごとの量。</returns>
    private static NutrientTotals MenuTotal() => MenuNutritionCalculator.Total(MenuNutritionCalculatorTests.Tonjiru(), GoldenData.Codes);

    /// <summary>
    /// お椀 1 杯（割合 0.125）を食べたときの記録用の量を確認します。
    /// タンパク質 11.4175g、ビタミン C 3.875mg（未測定の印あり）、ビタミン A 241.25μg で、表示用は小数 1 桁の 11.4g になります（AC-CALC-005）。
    /// </summary>
    [Fact]
    public void AC_CALC_005_OneServing()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.125000m);

        Assert.Equal(11.4175m, s[NutrientCodes.Protein].Amount);
        Assert.Equal(new NutrientAmount(3.8750m, true), s[NutrientCodes.VitaminC]);
        Assert.Equal(241.2500m, s[NutrientCodes.VitaminARae].Amount);
        Assert.Equal(11.4m, SnapshotCalculator.ForDisplay(s[NutrientCodes.Protein].Amount, "g")); // 表示は小数 1 桁
    }

    /// <summary>
    /// 1.5 杯（割合 0.1875）を食べたとき、丸めが最後に 1 回だけ行われることを確認します。
    /// タンパク質は 17.12625 が四捨五入で 17.1263 になります（AC-CALC-006）。
    /// </summary>
    [Fact]
    public void AC_CALC_006_OneAndHalfServing_RoundsHalfUpOnce()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.187500m);

        Assert.Equal(17.1263m, s[NutrientCodes.Protein].Amount); // 17.12625 → HALF_UP
        Assert.Equal(5.8125m, s[NutrientCodes.VitaminC].Amount);
        Assert.Equal(361.8750m, s[NutrientCodes.VitaminARae].Amount);
    }

    /// <summary>
    /// 割合が 0.333333 のときの記録用の量を確認します。
    /// タンパク質 30.4466g、ビタミン C 10.3333mg（未測定の印あり）、ビタミン A 643.3327μg になります（AC-CALC-007）。
    /// </summary>
    [Fact]
    public void AC_CALC_007_OneThird()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.333333m);

        Assert.Equal(30.4466m, s[NutrientCodes.Protein].Amount);
        Assert.Equal(10.3333m, s[NutrientCodes.VitaminC].Amount);
        Assert.Equal(643.3327m, s[NutrientCodes.VitaminARae].Amount);
        Assert.True(s[NutrientCodes.VitaminC].IsIncomplete);
    }

    /// <summary>
    /// 割合 0.25 で記録したときの量を確認します。
    /// タンパク質 22.835g、ビタミン C 7.75mg、ビタミン A 482.5μg になります（AC-CALC-009）。
    /// </summary>
    [Fact]
    public void AC_CALC_009_Ratio025()
    {
        var s = SnapshotCalculator.ForMenu(MenuTotal(), 0.250000m);

        Assert.Equal(22.8350m, s[NutrientCodes.Protein].Amount);
        Assert.Equal(7.7500m, s[NutrientCodes.VitaminC].Amount);
        Assert.Equal(482.5000m, s[NutrientCodes.VitaminARae].Amount);
    }

    /// <summary>
    /// 単品の食材の記録用の量が小数 4 桁に丸められ、未測定の印が引き継がれることを確認します（みそ 18g）。
    /// </summary>
    [Fact]
    public void ForFood_RoundsToFourDecimals_AndKeepsIncomplete()
    {
        var food = FoodNutritionCalculator.Calculate(GoldenData.Miso, 18m, GoldenData.Codes);

        var s = SnapshotCalculator.ForFood(food);

        Assert.Equal(2.1600m, s[NutrientCodes.Protein].Amount);
        Assert.True(s[NutrientCodes.VitaminC].IsIncomplete);
    }

    /// <summary>
    /// 表示用の丸めを確認します。kcal は整数（1704.4 → 1704）、それ以外は小数 1 桁（11.4175 → 11.4）です。
    /// </summary>
    /// <param name="value">保存されている量。</param>
    /// <param name="unit">栄養素の単位。</param>
    /// <param name="expected">期待する表示用の量。</param>
    [Theory]
    [InlineData(1704.4, "kcal", 1704)]
    [InlineData(11.4175, "g", 11.4)]
    public void ForDisplay_KcalIsInteger_OthersOneDecimal(double value, string unit, double expected)
    {
        Assert.Equal((decimal)expected, SnapshotCalculator.ForDisplay((decimal)value, unit));
    }
}
