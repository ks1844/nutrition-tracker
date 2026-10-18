using System.Collections;

namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 栄養素コード（例: PROT）ごとの量をまとめた、変更できない集合です。
/// 集合にないコードを引いたときは「量 0・未測定ではない」として扱います。
/// </summary>
public sealed class NutrientTotals : IEnumerable<KeyValuePair<string, NutrientAmount>>
{
    /// <summary>
    /// 栄養素コード → 量。
    /// </summary>
    private readonly IReadOnlyDictionary<string, NutrientAmount> _values;

    /// <summary>
    /// コードと量の組から集合を作ります。
    /// </summary>
    /// <param name="values">栄養素コードと量の組。</param>
    public NutrientTotals(IEnumerable<KeyValuePair<string, NutrientAmount>> values)
    {
        _values = values.ToDictionary(p => p.Key, p => p.Value);
    }

    /// <summary>
    /// 何も入っていない空の集合です（合計を取るときの出発点に使います）。
    /// </summary>
    public static NutrientTotals Empty { get; } = new([]);

    /// <summary>
    /// 指定した栄養素コードの量を返します。集合にないコードは 0（未測定ではない）です。
    /// </summary>
    /// <param name="code">栄養素コード（例: PROT、VITC）。</param>
    /// <returns>そのコードの量と未測定の印。</returns>
    public NutrientAmount this[string code] => _values.TryGetValue(code, out var v) ? v : NutrientAmount.Zero;

    /// <summary>
    /// この集合に入っている栄養素コードの一覧です。
    /// </summary>
    public IReadOnlyCollection<string> Codes => _values.Keys.ToList();

    /// <summary>
    /// 別の集合を足し合わせた新しい集合を返します。
    /// 同じコードの量は足し算し、未測定の印は OR で引き継ぎます。片方にしかないコードもそのまま残します。
    /// </summary>
    /// <param name="other">足す相手の集合。</param>
    /// <returns>合算した新しい集合（元の集合は変わりません）。</returns>
    public NutrientTotals Add(NutrientTotals other)
    {
        var merged = _values.ToDictionary(p => p.Key, p => p.Value);
        foreach (var (code, amount) in other)
        {
            merged[code] = merged.TryGetValue(code, out var cur) ? cur.Plus(amount) : amount;
        }
        return new NutrientTotals(merged);
    }

    /// <summary>
    /// 複数の集合をすべて足し合わせた 1 つの集合を返します（例: メニューの全食材の栄養量の合計）。
    /// </summary>
    /// <param name="items">足し合わせる集合。</param>
    /// <returns>合計の集合。1 件もなければ空の集合。</returns>
    public static NutrientTotals Sum(IEnumerable<NutrientTotals> items) =>
        items.Aggregate(Empty, (acc, x) => acc.Add(x));

    /// <summary>
    /// コードと量の組を順に取り出します。
    /// </summary>
    public IEnumerator<KeyValuePair<string, NutrientAmount>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
