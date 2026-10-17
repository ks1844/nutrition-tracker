using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>達成率（docs/06.スコアリング仕様.md §1.1）。小数 6 桁（HALF_UP）で保持する。</summary>
public static class AchievementCalculator
{
    /// <summary>下限型: min(摂取量 ÷ 目標, 1.0)。100% で頭打ちにし、他の栄養素の不足を相殺させない（docs/adr/0004）。</summary>
    public static decimal LowerRate(decimal intake, decimal target) =>
        RoundingPolicy.Round(Math.Min(intake / target, 1m), RoundingPolicy.RatioDigits);

    /// <summary>エネルギー充足: |摂取/目標 − 1| が許容幅以内なら 1。超えたら 1 − (差 − 許容) ÷ 減衰幅（下限 0）。</summary>
    public static decimal EnergyFitness(decimal intake, decimal target, ScoringSettings settings)
    {
        var d = Math.Abs(intake / target - 1m);
        var fit = d <= settings.EnergyTolerance ? 1m : Math.Max(0m, 1m - (d - settings.EnergyTolerance) / settings.EnergyFalloff);
        return RoundingPolicy.Round(fit, RoundingPolicy.RatioDigits);
    }

    /// <summary>食塩の減点: 上限を超えたときだけ min(上限点, 係数 × (摂取/上限 − 1))。超えなければ 0。</summary>
    public static decimal SaltPenalty(decimal intake, decimal max, ScoringSettings settings)
    {
        if (intake <= max)
        {
            return 0m;
        }
        return Math.Min(settings.SaltPenaltyMax, settings.SaltPenaltyMultiplier * (intake / max - 1m));
    }

    /// <summary>朝・昼・夕のうち、タンパク質が閾値以上だった食事の割合。朝昼夕の記録が無ければ null（除外）。</summary>
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
