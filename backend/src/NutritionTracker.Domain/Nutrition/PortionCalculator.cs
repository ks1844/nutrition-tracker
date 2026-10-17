namespace NutritionTracker.Domain.Nutrition;

/// <summary>
/// 食べた量の入力方式です（docs/03.栄養計算仕様.md §6）。
/// </summary>
public enum InputMode
{
    /// <summary>
    /// 割合で入力する（1 = メニュー全量）。
    /// </summary>
    Ratio,

    /// <summary>
    /// 杯数で入力する（例: 1.5 杯）。
    /// </summary>
    Serving,

    /// <summary>
    /// 食べた重さ（g）で入力する。
    /// </summary>
    Gram,

    /// <summary>
    /// 単位（本・大さじ など）で入力する。単品の食材だけで使えます。
    /// </summary>
    Unit,
}

/// <summary>
/// メニューの残りの量です。
/// </summary>
/// <param name="Ratio">残りの割合（1 = 全量、0 = 食べきり、負 = 食べすぎ）。</param>
/// <param name="Servings">残りを杯数に換算した値。</param>
/// <param name="IsOver">記録が合計 100% を超えているとき true。</param>
public sealed record RemainingPortion(decimal Ratio, decimal Servings, bool IsOver);

/// <summary>
/// メニューを食べた割合と残りの量を求めます（食べた量 ÷ 完成後の重量。docs/adr/0001）。
/// </summary>
public static class PortionCalculator
{
    /// <summary>
    /// 入力方式に応じて、メニュー全体に対する食べた割合を求め、小数 6 桁に四捨五入して返します。
    /// 割合入力はそのまま、杯数入力は「杯数 ÷ 全体の杯数」、g 入力は「食べた g ÷ 完成後の重量」です。
    /// </summary>
    /// <param name="mode">入力方式（割合・杯数・g のいずれか。単位は不可）。</param>
    /// <param name="value">入力した値。</param>
    /// <param name="cookedTotalWeightG">メニューの完成後の重量（g）。</param>
    /// <param name="servings">メニュー全体の杯数。</param>
    /// <returns>食べた割合（1 = 全量）。</returns>
    public static decimal Ratio(InputMode mode, decimal value, decimal cookedTotalWeightG, decimal servings) => mode switch
    {
        InputMode.Gram => RoundingPolicy.Round(value / cookedTotalWeightG, RoundingPolicy.RatioDigits),
        InputMode.Serving => RoundingPolicy.Round(value / servings, RoundingPolicy.RatioDigits),
        InputMode.Ratio => RoundingPolicy.Round(value, RoundingPolicy.RatioDigits),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "メニューの入力方式は割合・杯数・g のいずれかです"),
    };

    /// <summary>
    /// メニューの残りを求めます（残り = 1 − 食べた割合の合計）。
    /// 合計が 1 を超えていれば、食べすぎとして IsOver を立てます。保存はせず、記録から毎回計算します。
    /// </summary>
    /// <param name="ratios">これまでの記録の食べた割合。</param>
    /// <param name="menuServings">メニュー全体の杯数（残りを杯数に換算するために使います）。</param>
    /// <returns>残りの割合・杯数・食べすぎかどうか。</returns>
    public static RemainingPortion Remaining(IEnumerable<decimal> ratios, decimal menuServings)
    {
        var ratio = RoundingPolicy.Round(1m - ratios.Sum(), RoundingPolicy.RatioDigits);
        return new RemainingPortion(ratio, RoundingPolicy.Round(ratio * menuServings, 2), ratio < 0m);
    }

    /// <summary>
    /// お椀 1 杯あたりの重さ（完成後の重量 ÷ 杯数）を、小数 1 桁で求めます（表示用）。
    /// </summary>
    /// <param name="cookedTotalWeightG">完成後の重量（g）。</param>
    /// <param name="servings">全体の杯数。</param>
    /// <returns>1 杯の重さ（g）。</returns>
    public static decimal ServingWeightG(decimal cookedTotalWeightG, decimal servings) =>
        RoundingPolicy.Round(cookedTotalWeightG / servings, 1);
}
