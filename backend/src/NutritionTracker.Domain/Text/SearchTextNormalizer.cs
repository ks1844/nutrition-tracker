using System.Text;

namespace NutritionTracker.Domain.Text;

/// <summary>
/// 食材検索の検索語をそろえます。ひらがな・カタカナ、全角・半角の違いを区別せずに探せるようにするためです（docs/09.API仕様.md API-01）。
/// ICU（文化圏ごとの文字処理）に依存せずに動くよう、全角半角の統一は自前で行います。
/// </summary>
public static class SearchTextNormalizer
{
    /// <summary>
    /// 半角カナの並び（FullKana と同じ順番で対応します）。
    /// </summary>
    private const string HalfKana = "｡｢｣､･ｦｧｨｩｪｫｬｭｮｯｰｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ";
    /// <summary>
    /// 半角カナに対応する全角カナの並び。
    /// </summary>
    private const string FullKana = "。「」、・ヲァィゥェォャュョッーアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン";
    /// <summary>
    /// 濁点（゛）がつく全角カナ。
    /// </summary>
    private const string VoicedTargets = "カキクケコサシスセソタチツテトハヒフヘホウ";
    /// <summary>
    /// 半角の濁点。
    /// </summary>
    private const string HalfVoiced = "ﾞ";
    /// <summary>
    /// 半角の半濁点。
    /// </summary>
    private const string HalfSemiVoiced = "ﾟ";

    /// <summary>
    /// 前後の空白を除き、全角の英数字を半角に、全角の空白を半角の空白に、半角カナを全角カナ（濁点・半濁点を含む）にそろえます。
    /// </summary>
    /// <param name="text">入力された検索語。</param>
    /// <returns>そろえた検索語。</returns>
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

    /// <summary>
    /// カタカナをひらがなに変えます（それ以外の文字はそのままです）。
    /// </summary>
    /// <param name="text">変換前の文字列。</param>
    /// <returns>ひらがなにした文字列。</returns>
    public static string ToHiragana(string text) =>
        Convert(text, c => c is >= 'ァ' and <= 'ヶ' ? (char)(c - 0x60) : c);

    /// <summary>
    /// ひらがなをカタカナに変えます（それ以外の文字はそのままです）。
    /// </summary>
    /// <param name="text">変換前の文字列。</param>
    /// <returns>カタカナにした文字列。</returns>
    public static string ToKatakana(string text) =>
        Convert(text, c => c is >= 'ぁ' and <= 'ゖ' ? (char)(c + 0x60) : c);

    /// <summary>
    /// データベースの部分一致検索に使う候補を作ります（そろえた原文・ひらがな・カタカナ。重複と空は除く）。
    /// </summary>
    /// <param name="query">入力された検索語。</param>
    /// <returns>検索に使う候補の一覧。</returns>
    public static IReadOnlyList<string> Candidates(string query)
    {
        var normalized = Normalize(query);
        return new[] { normalized, ToHiragana(normalized), ToKatakana(normalized) }
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 文字列の各文字に変換を当てはめます。
    /// </summary>
    /// <param name="text">変換前の文字列。</param>
    /// <param name="map">1 文字を別の文字に変える関数。</param>
    /// <returns>変換後の文字列。</returns>
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
