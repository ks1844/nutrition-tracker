using NutritionTracker.Domain.Text;

namespace NutritionTracker.Domain.Tests.Text;

/// <summary>
/// 食材検索の検索語の正規化のテストです。
/// </summary>
public class SearchTextNormalizerTests
{
    /// <summary>
    /// 検索の候補に、ひらがなとカタカナの両方が含まれることを確認します（「ニンジン」でも「にんじん」でも探せます）。
    /// </summary>
    [Fact]
    public void Candidates_IncludeHiraganaAndKatakana()
    {
        Assert.Equal(["ニンジン", "にんじん"], SearchTextNormalizer.Candidates("ニンジン").OrderBy(x => x == "にんじん"));
        Assert.Contains("ニンジン", SearchTextNormalizer.Candidates("にんじん"));
        Assert.Contains("にんじん", SearchTextNormalizer.Candidates("にんじん"));
    }

    /// <summary>
    /// 全角の英数字と空白、半角カナ（濁点・半濁点つき）が標準形にそろうことを確認します。
    /// 「　ＡＢＣ１２３ 」は「ABC123」、「ﾆﾝｼﾞﾝ」は「ニンジン」、「ﾊﾟﾝ」は「パン」になります。
    /// </summary>
    [Fact]
    public void Normalize_UnifiesFullWidthAndHalfWidth()
    {
        Assert.Equal("ABC123", SearchTextNormalizer.Normalize("　ＡＢＣ１２３ "));
        Assert.Equal("ニンジン", SearchTextNormalizer.Normalize("ﾆﾝｼﾞﾝ")); // 半角カナ（濁点つき）
        Assert.Equal("パン", SearchTextNormalizer.Normalize("ﾊﾟﾝ")); // 半濁点
        Assert.Equal("ヴァ", SearchTextNormalizer.Normalize("ｳﾞｧ"));
    }

    /// <summary>
    /// 漢字は変換されず、そのまま候補になることを確認します（「人参」）。
    /// </summary>
    [Fact]
    public void Kanji_IsKeptAsIs()
    {
        Assert.Equal(["人参"], SearchTextNormalizer.Candidates("人参"));
    }

    /// <summary>
    /// 空白だけの検索語には候補がないことを確認します。
    /// </summary>
    [Fact]
    public void Blank_HasNoCandidates()
    {
        Assert.Empty(SearchTextNormalizer.Candidates("   "));
    }
}
