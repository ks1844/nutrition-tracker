using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 成分表の表記を変換した値です。
/// </summary>
/// <param name="Amount">100g あたりの含有量。null は未測定（0 とは区別します）。</param>
/// <param name="IsTrace">微量（Tr）のとき true。</param>
public readonly record struct NotationValue(decimal? Amount, bool IsTrace);

/// <summary>
/// 成分表の記号を、含有量と微量の印に変換します（docs/07.マスタデータ仕様.md §3。変換規則は原典での確認が必要）。
/// 「-」と空欄は未測定、「Tr」は 0 で微量、「(0)」「(数値)」「数値」はその数値です。それ以外は変換できません。
/// </summary>
public static partial class FoodTableNotation
{
    /// <summary>
    /// 「12」「0.04」のような、符号のない数値の書式。
    /// </summary>
    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// 「(1.2)」のような、括弧つきの推定値の書式（括弧の中の数値を取り出します）。
    /// </summary>
    [GeneratedRegex(@"^\((\d+(\.\d+)?)\)$")]
    private static partial Regex EstimatedPattern();

    /// <summary>
    /// 成分表の表記を、含有量と微量の印に変換します。
    /// 全角の数字や括弧、桁区切りのカンマ、全角のハイフンなどの揺れは吸収します。
    /// </summary>
    /// <param name="raw">成分表の表記そのまま（例: "-"、"Tr"、"(1.2)"、"1,000"）。</param>
    /// <param name="value">変換結果。変換できなかったときは既定値。</param>
    /// <returns>変換できたとき true、規則にない表記のとき false。</returns>
    public static bool TryParse(string? raw, out NotationValue value)
    {
        value = default;
        var text = Fold(raw ?? "");
        if (text.Length == 0 || text == "-")
        {
            value = new NotationValue(null, false);
            return true;
        }
        if (text.Equals("Tr", StringComparison.OrdinalIgnoreCase))
        {
            value = new NotationValue(0m, true);
            return true;
        }
        var number = EstimatedPattern().Match(text) is { Success: true } estimated ? estimated.Groups[1].Value : text;
        if (!NumberPattern().IsMatch(number))
        {
            return false;
        }
        value = new NotationValue(decimal.Parse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture), false);
        return true;
    }

    /// <summary>
    /// 表記を比べやすいように整えます。
    /// 前後の空白、途中の空白、桁区切りのカンマを除き、全角の英数字・記号を半角にし、全角のハイフン類を「-」にそろえます。
    /// </summary>
    /// <param name="text">整える前の表記。</param>
    /// <returns>整えた表記。</returns>
    private static string Fold(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Trim())
        {
            var folded = c switch
            {
                >= '！' and <= '～' => (char)(c - '！' + '!'),
                '　' => ' ',
                '−' or '‐' or '―' or 'ー' => '-', // マイナス・ハイフン・ダッシュ・長音（未測定の「-」の打ち間違い）
                _ => c,
            };
            if (folded != ',' && !char.IsWhiteSpace(folded))
            {
                sb.Append(folded);
            }
        }
        return sb.ToString();
    }
}
