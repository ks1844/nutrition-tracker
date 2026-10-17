using NutritionTracker.Domain.Text;

namespace NutritionTracker.Domain.Tests.Text;

public class SearchTextNormalizerTests
{
    [Fact]
    public void Candidates_IncludeHiraganaAndKatakana()
    {
        Assert.Equal(["ニンジン", "にんじん"], SearchTextNormalizer.Candidates("ニンジン").OrderBy(x => x == "にんじん"));
        Assert.Contains("ニンジン", SearchTextNormalizer.Candidates("にんじん"));
        Assert.Contains("にんじん", SearchTextNormalizer.Candidates("にんじん"));
    }

    [Fact]
    public void Normalize_UnifiesFullWidthAndHalfWidth()
    {
        Assert.Equal("ABC123", SearchTextNormalizer.Normalize("　ＡＢＣ１２３ "));
        Assert.Equal("ニンジン", SearchTextNormalizer.Normalize("ﾆﾝｼﾞﾝ")); // 半角カナ（濁点つき）
        Assert.Equal("パン", SearchTextNormalizer.Normalize("ﾊﾟﾝ")); // 半濁点
        Assert.Equal("ヴァ", SearchTextNormalizer.Normalize("ｳﾞｧ"));
    }

    [Fact]
    public void Kanji_IsKeptAsIs()
    {
        Assert.Equal(["人参"], SearchTextNormalizer.Candidates("人参"));
    }

    [Fact]
    public void Blank_HasNoCandidates()
    {
        Assert.Empty(SearchTextNormalizer.Candidates("   "));
    }
}
