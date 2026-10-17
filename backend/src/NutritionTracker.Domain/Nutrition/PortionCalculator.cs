namespace NutritionTracker.Domain.Nutrition;

/// <summary>入力方式（docs/03.栄養計算仕様.md §6）。</summary>
public enum InputMode
{
    /// <summary>割合（1 = 全量）</summary>
    Ratio,

    /// <summary>杯数</summary>
    Serving,

    /// <summary>食べた g</summary>
    Gram,

    /// <summary>単位（単品食材のみ）</summary>
    Unit,
}

/// <summary>メニューの残量。<paramref name="Servings"/> は杯数換算。</summary>
public sealed record RemainingPortion(decimal Ratio, decimal Servings, bool IsOver);

/// <summary>食べた割合（portion_ratio）と残量（D-4: 食べた量 ÷ 完成後重量。docs/adr/0001）。</summary>
public static class PortionCalculator
{
    /// <summary>食べた割合を小数 6 桁（HALF_UP）で求める。</summary>
    public static decimal Ratio(InputMode mode, decimal value, decimal cookedTotalWeightG, decimal servings) => mode switch
    {
        InputMode.Gram => RoundingPolicy.Round(value / cookedTotalWeightG, RoundingPolicy.RatioDigits),
        InputMode.Serving => RoundingPolicy.Round(value / servings, RoundingPolicy.RatioDigits),
        InputMode.Ratio => RoundingPolicy.Round(value, RoundingPolicy.RatioDigits),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "メニューの入力方式は割合・杯数・g のいずれかです"),
    };

    /// <summary>残量 = 1 − Σ割合（保存しない）。1 を超えて記録されていれば <see cref="RemainingPortion.IsOver"/>。</summary>
    public static RemainingPortion Remaining(IEnumerable<decimal> ratios, decimal menuServings)
    {
        var ratio = RoundingPolicy.Round(1m - ratios.Sum(), RoundingPolicy.RatioDigits);
        return new RemainingPortion(ratio, RoundingPolicy.Round(ratio * menuServings, 2), ratio < 0m);
    }

    /// <summary>1 杯の重さ（表示用）= 完成後重量 ÷ 杯数。</summary>
    public static decimal ServingWeightG(decimal cookedTotalWeightG, decimal servings) =>
        RoundingPolicy.Round(cookedTotalWeightG / servings, 1);
}
