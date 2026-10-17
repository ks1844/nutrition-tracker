using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// メニューを食べた割合と残りの量のテストです（docs/03.栄養計算仕様.md §6）。
/// </summary>
public class PortionCalculatorTests
{
    /// <summary>
    /// 豚汁の完成後の重さ（2400g）。
    /// </summary>
    private const decimal Cooked = GoldenData.CookedTotalWeightG;
    /// <summary>
    /// 豚汁の杯数（8 杯）。
    /// </summary>
    private const decimal Servings = GoldenData.Servings;

    /// <summary>
    /// 入力方式（杯数・g・割合）ごとに、食べた割合が小数 6 桁で求まることを確認します。
    /// お椀 1 杯・300g はどちらも 0.125、1.5 杯は 0.1875、800g は 800÷2400 で 0.333333、割合 0.25 は 0.25 になります。
    /// </summary>
    /// <param name="mode">入力方式。</param>
    /// <param name="value">入力した値。</param>
    /// <param name="expected">期待する食べた割合（小数 6 桁の文字列）。</param>
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

    /// <summary>
    /// メニューの入力方式に「単位」を指定すると、例外になることを確認します（単位は単品の食材だけで使えます）。
    /// </summary>
    [Fact]
    public void UnitMode_IsNotAllowedForMenu()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PortionCalculator.Ratio(InputMode.Unit, 1m, Cooked, Servings));
    }

    /// <summary>
    /// 残りが「1 − 食べた割合の合計」になることを確認します。
    /// 1.5 杯（0.1875）と 1 杯（0.125）を食べた豚汁は、残り 0.6875（5.5 杯分）で、食べすぎではありません（AC-CALC-013）。
    /// </summary>
    [Fact]
    public void AC_CALC_013_Remaining_IsOneMinusSumOfRatios()
    {
        var remaining = PortionCalculator.Remaining([0.1875m, 0.125m], Servings);

        Assert.Equal(0.6875m, remaining.Ratio);
        Assert.Equal(5.5m, remaining.Servings);
        Assert.False(remaining.IsOver);
    }

    /// <summary>
    /// 合計が 100% を超えて記録されたとき、残りが負の値になり、食べすぎの印が立つことを確認します（0.75 + 0.5 → 残り −0.25）。
    /// </summary>
    [Fact]
    public void Remaining_IsOver_WhenRecordedMoreThanWhole()
    {
        var remaining = PortionCalculator.Remaining([0.75m, 0.5m], Servings);

        Assert.True(remaining.IsOver);
        Assert.Equal(-0.25m, remaining.Ratio);
    }

    /// <summary>
    /// 1 杯の重さが「完成後の重さ ÷ 杯数」になることを確認します（2400g ÷ 8 杯 = 300g）。
    /// </summary>
    [Fact]
    public void ServingWeight_IsCookedWeightDividedByServings()
    {
        Assert.Equal(300.0m, PortionCalculator.ServingWeightG(Cooked, Servings));
    }
}
