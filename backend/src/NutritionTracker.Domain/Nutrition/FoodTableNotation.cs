using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NutritionTracker.Domain.Nutrition;

/// <summary>成分表の表記を変換した値。<see cref="Amount"/> が null = 未測定（0 とは区別）。</summary>
public readonly record struct NotationValue(decimal? Amount, bool IsTrace);

/// <summary>
/// 成分表の記号の変換（docs/07.マスタデータ仕様.md §3）。<b>変換規則は要原典確認</b>。
/// <c>-</c>・空欄 = 未測定、<c>Tr</c> = 0 + 微量、<c>(0)</c>・<c>(数値)</c>・数値 = その数値。上記以外は変換できない。
/// </summary>
public static partial class FoodTableNotation
{
    [GeneratedRegex(@"^\d+(\.\d+)?$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^\((\d+(\.\d+)?)\)$")]
    private static partial Regex EstimatedPattern();

    /// <summary>変換する。形式が合わなければ false。</summary>
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

    /// <summary>前後の空白を除き、全角の英数字・記号を半角に、桁区切りのカンマを除く。</summary>
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
