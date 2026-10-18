using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Tests.Nutrition;

/// <summary>
/// 成分表の記号の変換のテストです（docs/07.マスタデータ仕様.md §3）。
/// </summary>
public class FoodTableNotationTests
{
    /// <summary>
    /// 表記を変換し、変換できることを確かめたうえで結果を返します。
    /// </summary>
    /// <param name="raw">成分表の表記。</param>
    /// <returns>変換結果。</returns>
    private static NotationValue Parse(string? raw)
    {
        Assert.True(FoodTableNotation.TryParse(raw, out var value), $"変換できるはず: '{raw}'");
        return value;
    }

    /// <summary>
    /// 「-」、空欄、空白だけ、null は、すべて未測定（含有量 null・微量でない）になることを確認します。
    /// </summary>
    /// <param name="raw">成分表の表記。</param>
    [Theory]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Unmeasured_IsNullAndNotTrace(string? raw) => Assert.Equal(new NotationValue(null, false), Parse(raw));

    /// <summary>
    /// 「Tr」は、大文字小文字や前後の空白にかかわらず、含有量 0 で微量の印がつくことを確認します。
    /// </summary>
    /// <param name="raw">成分表の表記。</param>
    [Theory]
    [InlineData("Tr")]
    [InlineData("tr")]
    [InlineData(" Tr ")]
    public void Trace_IsZeroWithTraceFlag(string raw) => Assert.Equal(new NotationValue(0m, true), Parse(raw));

    /// <summary>
    /// 数値、括弧つきの推定値、桁区切りのカンマ、全角の数字や括弧が、微量の印なしでその数値になることを確認します。
    /// </summary>
    /// <param name="raw">成分表の表記。</param>
    /// <param name="expected">期待する含有量。</param>
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

    /// <summary>
    /// 全角のハイフン類も、未測定として扱われることを確認します。
    /// </summary>
    /// <param name="raw">成分表の表記。</param>
    [Theory]
    [InlineData("－")]
    [InlineData("‐")]
    public void FullWidthHyphens_AreUnmeasured(string raw) => Assert.Equal(new NotationValue(null, false), Parse(raw));

    /// <summary>
    /// 規則にない表記（文字、「(Tr)」、負の数、小数点が 2 つ、指数、閉じていない括弧、記号、「--」、先頭が小数点）は変換できないことを確認します。
    /// </summary>
    /// <param name="raw">成分表の表記。</param>
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
