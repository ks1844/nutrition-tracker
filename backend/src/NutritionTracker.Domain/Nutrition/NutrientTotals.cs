using System.Collections;

namespace NutritionTracker.Domain.Nutrition;

/// <summary>栄養素コード → 量の集合（不変）。存在しないコードは 0（不完全ではない）として扱う。</summary>
public sealed class NutrientTotals : IEnumerable<KeyValuePair<string, NutrientAmount>>
{
    private readonly IReadOnlyDictionary<string, NutrientAmount> _values;

    public NutrientTotals(IEnumerable<KeyValuePair<string, NutrientAmount>> values)
    {
        _values = values.ToDictionary(p => p.Key, p => p.Value);
    }

    public static NutrientTotals Empty { get; } = new([]);

    public NutrientAmount this[string code] => _values.TryGetValue(code, out var v) ? v : NutrientAmount.Zero;

    public IReadOnlyCollection<string> Codes => _values.Keys.ToList();

    /// <summary>同じコードどうしを合算する。どちらか一方にしか無いコードも残す。</summary>
    public NutrientTotals Add(NutrientTotals other)
    {
        var merged = _values.ToDictionary(p => p.Key, p => p.Value);
        foreach (var (code, amount) in other)
        {
            merged[code] = merged.TryGetValue(code, out var cur) ? cur.Plus(amount) : amount;
        }
        return new NutrientTotals(merged);
    }

    public static NutrientTotals Sum(IEnumerable<NutrientTotals> items) =>
        items.Aggregate(Empty, (acc, x) => acc.Add(x));

    public IEnumerator<KeyValuePair<string, NutrientAmount>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
