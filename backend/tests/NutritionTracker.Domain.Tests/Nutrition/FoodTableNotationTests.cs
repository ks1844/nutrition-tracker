using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>成分表の記号の変換（docs/07.マスタデータ仕様.md §3）。</summary>
public class FoodTableNotationTests
{
    private static NotationValue Parse(string? raw)
    {
        Assert.True(FoodTableNotation.TryParse(raw, out var value), $"変換できるはず: '{raw}'");
        return value;
    }

    [Theory]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Unmeasured_IsNullAndNotTrace(string? raw) => Assert.Equal(new NotationValue(null, false), Parse(raw));

    [Theory]
    [InlineData("Tr")]
    [InlineData("tr")]
    [InlineData(" Tr ")]
    public void Trace_IsZeroWithTraceFlag(string raw) => Assert.Equal(new NotationValue(0m, true), Parse(raw));

    [Theory]
    [InlineData("(0)", 0)]
    [InlineData("0", 0)]
    [InlineData("1.2", 1.2)]
    [InlineData("(1.2)", 1.2)]
    [InlineData("0.04", 0.04)]
    [InlineData("1,000", 1000)]
    [InlineData("1,234.5", 1234.5)]
    [InlineData("（3.5）", 3.5)]
    [InlineData("１２．５", 12.5)]
    [InlineData(" 7 ", 7)]
    public void Numbers_AreParsed_WithoutTraceFlag(string raw, double expected) =>
        Assert.Equal(new NotationValue((decimal)expected, false), Parse(raw));

    [Theory]
    [InlineData("－")]
    [InlineData("‐")]
    public void FullWidthHyphens_AreUnmeasured(string raw) => Assert.Equal(new NotationValue(null, false), Parse(raw));

    [Theory]
    [InlineData("abc")]
    [InlineData("(Tr)")]
    [InlineData("-1")]
    [InlineData("(-1)")]
    [InlineData("1.2.3")]
    [InlineData("1e3")]
    [InlineData("(1.2")]
    [InlineData("*")]
    [InlineData("--")]
    [InlineData(".5")]
    public void UnknownNotation_IsRejected(string raw)
    {
        Assert.False(FoodTableNotation.TryParse(raw, out var value));
        Assert.Equal(default, value);
    }
}
