namespace NutritionTracker.Domain.Nutrition;

/// <summary>PFC 比。<c>*Pct</c> は小数 1 桁の百分率、<c>*Display</c> は合計が 100 になる整数（最大剰余法）。</summary>
public sealed record PfcRatio(
    decimal ProteinPct,
    decimal FatPct,
    decimal CarbPct,
    int ProteinDisplay,
    int FatDisplay,
    int CarbDisplay);

/// <summary>
/// PFC 比（docs/03.栄養計算仕様.md §8）。P・C は 4kcal/g、F は 9kcal/g で算出する。
/// kcal 自体は成分表のエネルギー値を使い、PFC 比とは別計算（情報表示でスコアには使わない）。
/// </summary>
public static class PfcCalculator
{
    private const decimal ProteinKcalPerG = 4m;
    private const decimal FatKcalPerG = 9m;
    private const decimal CarbKcalPerG = 4m;

    /// <summary>算出できない（分母 0）ときは 0 を返す。</summary>
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

    /// <summary>合計が 100 になるよう、小数部の大きい順に 1 ずつ繰り上げる（最大剰余法）。</summary>
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
