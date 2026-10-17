using System.Text;

namespace NutritionTracker.Domain.Text;

/// <summary>
/// 検索語の正規化（かな・カナ・全角半角の違いを区別しない。docs/09.API仕様.md API-01）。
/// ICU に依存しない（InvariantGlobalization でも動く）よう、全角半角の統一は自前で行う。
/// </summary>
public static class SearchTextNormalizer
{
    private const string HalfKana = "｡｢｣､･ｦｧｨｩｪｫｬｭｮｯｰｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ";
    private const string FullKana = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン";
    private const string VoicedTargets = "カキクケコサシスセソタチツテトハヒフヘホウ";
    private const string HalfVoiced = "ﾞ";
    private const string HalfSemiVoiced = "ﾟ";

    /// <summary>前後の空白を除き、全角英数・全角空白・半角カナを標準形にそろえる。</summary>
    public static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '　')
            {
                sb.Append(' ');
            }
            else if (c is >= '！' and <= '～')
            {
                sb.Append((char)(c - 0xFEE0)); // 全角 ASCII → 半角 ASCII
            }
            else if (HalfKana.IndexOf(c) is var k and >= 0)
            {
                var full = FullKana[k];
                var next = i + 1 < text.Length ? text[i + 1] : '\0';
                if (next.ToString() == HalfVoiced && VoicedTargets.Contains(full))
                {
                    full = (char)(full + 1); // カ → ガ（ウ → ヴ は +0x4E のため別扱い）
                    if (FullKana[k] == 'ウ')
                    {
                        full = 'ヴ';
                    }
                    i++;
                }
                else if (next.ToString() == HalfSemiVoiced && "ハヒフヘホ".Contains(full))
                {
                    full = (char)(full + 2); // ハ → パ
                    i++;
                }
                sb.Append(full);
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Trim();
    }

    public static string ToHiragana(string text) =>
        Convert(text, c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c);

    public static string ToKatakana(string text) =>
        Convert(text, c => c is >= 'ぁ' and <= 'ゖ' ? (char)(c + 0x60) : c);

    /// <summary>DB の部分一致に使う候補（原文・ひらがな・カタカナ）。重複は除く。</summary>
    public static IReadOnlyList<string> Candidates(string query)
    {
        var normalized = Normalize(query);
        return new[] { normalized, ToHiragana(normalized), ToKatakana(normalized) }
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string Convert(string text, Func<char, char> map)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(map(c));
        }
        return sb.ToString();
    }
}
