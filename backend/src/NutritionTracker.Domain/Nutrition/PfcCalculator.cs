namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// タンパク質（P）・脂質（F）・炭水化物（C）のエネルギー比です。
/// </summary>
/// <param name="ProteinPct">P の割合（%、小数 1 桁）。</param>
/// <param name="FatPct">F の割合（%、小数 1 桁）。</param>
/// <param name="CarbPct">C の割合（%、小数 1 桁）。</param>
/// <param name="ProteinDisplay">P の割合（表示用の整数。3 つの合計が 100 になる）。</param>
/// <param name="FatDisplay">F の割合（表示用の整数）。</param>
/// <param name="CarbDisplay">C の割合（表示用の整数）。</param>
public sealed record PfcRatio(
    decimal ProteinPct,
    decimal FatPct,
    decimal CarbPct,
    int ProteinDisplay,
    int FatDisplay,
    int CarbDisplay);

/// <summary>
/// PFC 比を求めます（docs/03.栄養計算仕様.md §8）。P・C は 1g あたり 4kcal、F は 9kcal で換算します。
/// エネルギー（kcal）自体は成分表の値を使い、PFC 比とは別の計算です。情報表示用で、スコアには使いません。
/// </summary>
public static class PfcCalculator
{
    /// <summary>
    /// タンパク質 1g あたりのエネルギー（kcal）。
    /// </summary>
    private const decimal ProteinKcalPerG = 4m;
    /// <summary>
    /// 脂質 1g あたりのエネルギー（kcal）。
    /// </summary>
    private const decimal FatKcalPerG = 9m;
    /// <summary>
    /// 炭水化物 1g あたりのエネルギー（kcal）。
    /// </summary>
    private const decimal CarbKcalPerG = 4m;

    /// <summary>
    /// タンパク質・脂質・炭水化物の重さ（g）から、エネルギーに占める割合を求めます。
    /// エネルギーの合計が 0 のように計算できないときは、すべて 0 を返します。
    /// </summary>
    /// <param name="proteinG">タンパク質の量（g）。</param>
    /// <param name="fatG">脂質の量（g）。</param>
    /// <param name="carbG">炭水化物の量（g）。</param>
    /// <returns>小数 1 桁の割合と、合計 100 の表示用の整数。</returns>
    public static PfcRatio Calculate(decimal proteinG, decimal fatG, decimal carbG)
    {
        var kcal = new[] { proteinG * ProteinKcalPerG, fatG * FatKcalPerG, carbG * CarbKcalPerG };
        var sum = kcal.Sum();
        if (sum <= 0m)
        {
            return new PfcRatio(0m, 0m, 0m, 0, 0, 0);
        }

        var pct = kcal.Select(k => k / sum * 100m).ToArray();
        var display = LargestRemainder(pct);
        return new PfcRatio(
            RoundingPolicy.Round(pct[0], 1), RoundingPolicy.Round(pct[1], 1), RoundingPolicy.Round(pct[2], 1),
            display[0], display[1], display[2]);
    }

    /// <summary>
    /// 小数の割合を、合計がちょうど 100 になる整数に直します（最大剰余法）。
    /// まず小数部を切り捨て、足りない分を小数部の大きいものから順に 1 ずつ繰り上げます。
    /// </summary>
    /// <param name="values">合計 100 の小数の割合。</param>
    /// <returns>合計 100 の整数の割合。</returns>
    private static int[] LargestRemainder(decimal[] values)
    {
        var floors = values.Select(v => (int)Math.Floor(v)).ToArray();
        var left = 100 - floors.Sum();
        foreach (var i in Enumerable.Range(0, values.Length)
                     .OrderByDescending(i => values[i] - Math.Floor(values[i]))
                     .Take(Math.Max(left, 0)))
        {
            floors[i]++;
        }
        return floors;
    }
}
