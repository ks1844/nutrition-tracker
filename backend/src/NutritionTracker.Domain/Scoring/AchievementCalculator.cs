using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// 栄養素ごとの達成率を求めます（docs/06.スコアリング仕様.md §1.1）。小数 6 桁（四捨五入）で保持します。
/// </summary>
public static class AchievementCalculator
{
    /// <summary>
    /// 下限型の目標（摂取量が目標以上であればよい栄養素）の達成率を求めます（摂取量 ÷ 目標。最大 1.0）。
    /// 100% で頭打ちにして、摂りすぎた栄養素で他の栄養素の不足を埋め合わせないようにします（docs/adr/0004）。
    /// </summary>
    /// <param name="intake">摂取量。</param>
    /// <param name="target">目標量（0 より大きい）。</param>
    /// <returns>達成率（0〜1）。</returns>
    public static decimal LowerRate(decimal intake, decimal target) =>
        RoundingPolicy.Round(Math.Min(intake / target, 1m), RoundingPolicy.RatioDigits);

    /// <summary>
    /// エネルギーが目標に合っている度合いを求めます。
    /// 目標との差が許容幅以内なら 1、それを超えたら差に応じて下がり、減衰幅を超えると 0 です。多すぎても少なすぎても下がります。
    /// </summary>
    /// <param name="intake">エネルギー摂取量（kcal）。</param>
    /// <param name="target">エネルギー目標（kcal）。</param>
    /// <param name="settings">許容幅などの設定。</param>
    /// <returns>エネルギー充足（0〜1）。</returns>
    public static decimal EnergyFitness(decimal intake, decimal target, ScoringSettings settings)
    {
        var d = Math.Abs(intake / target - 1m);
        var fit = d <= settings.EnergyTolerance ? 1m : Math.Max(0m, 1m - (d - settings.EnergyTolerance) / settings.EnergyFalloff);
        return RoundingPolicy.Round(fit, RoundingPolicy.RatioDigits);
    }

    /// <summary>
    /// 食塩が上限を超えたときの減点（点）を求めます。上限以内なら 0 で、超えた分に比例して増え、上限点で止まります。
    /// </summary>
    /// <param name="intake">食塩相当量の摂取量（g）。</param>
    /// <param name="max">食塩相当量の上限（g）。</param>
    /// <param name="settings">減点の係数と上限点の設定。</param>
    /// <returns>減点（点）。</returns>
    public static decimal SaltPenalty(decimal intake, decimal max, ScoringSettings settings)
    {
        if (intake <= max)
        {
            return 0m;
        }
        return Math.Min(settings.SaltPenaltyMax, settings.SaltPenaltyMultiplier * (intake / max - 1m));
    }

    /// <summary>
    /// 朝・昼・夕の食事のうち、タンパク質が十分（閾値以上）だった食事の割合を求めます（筋肉スコアの分配率）。
    /// 朝昼夕の記録が 1 件もないときは、この要素を計算から外すため null を返します。
    /// </summary>
    /// <param name="mainMealProteinsG">朝・昼・夕それぞれの食事のタンパク質量（g）。</param>
    /// <param name="settings">十分とみなす閾値の設定。</param>
    /// <returns>十分だった食事の割合（0〜1）。記録がなければ null。</returns>
    public static decimal? DistributionRate(IReadOnlyCollection<decimal> mainMealProteinsG, ScoringSettings settings)
    {
        if (mainMealProteinsG.Count == 0)
        {
            return null;
        }
        var enough = mainMealProteinsG.Count(p => p >= settings.ProteinPerMealThresholdG);
        return RoundingPolicy.Round((decimal)enough / mainMealProteinsG.Count, RoundingPolicy.RatioDigits);
    }
}
