using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

public enum ScoreBand
{
    /// <summary>良好（80 以上）</summary>
    Good,

    /// <summary>もう少し（60〜79）</summary>
    Fair,

    /// <summary>不足気味（60 未満）</summary>
    Low,
}

/// <summary>美肌・筋肉・基礎のスコア（docs/06.スコアリング仕様.md §3〜§5）。整数に四捨五入（HALF_UP）。</summary>
public static class ScoreCalculator
{
    /// <summary>
    /// 美肌 = 達成率の加重平均 × 100。<paramref name="lowerTargets"/> に目標が無い栄養素は除外して重みを再正規化する（暫定）。
    /// アスタキサンチンはスコア外。
    /// </summary>
    public static int? Beauty(
        IReadOnlyDictionary<string, decimal> intake,
        IReadOnlyDictionary<string, decimal> lowerTargets,
        ScoringSettings settings)
    {
        decimal weighted = 0m, weightSum = 0m;
        foreach (var (code, weight) in settings.BeautyWeights)
        {
            if (!lowerTargets.TryGetValue(code, out var target))
            {
                continue;
            }
            weighted += weight * AchievementCalculator.LowerRate(intake.GetValueOrDefault(code), target);
            weightSum += weight;
        }
        return weightSum == 0m ? null : ToScore(weighted / weightSum);
    }

    /// <summary>
    /// 筋肉 = 0.6×タンパク質達成率 + 0.2×分配率 + 0.2×エネルギー充足。
    /// 朝昼夕の記録が無ければ分配率を除外し、残りの重みを再正規化する。
    /// </summary>
    public static int Muscle(
        decimal proteinIntakeG,
        decimal proteinTargetG,
        IReadOnlyCollection<decimal> mainMealProteinsG,
        decimal energyIntakeKcal,
        decimal energyTargetKcal,
        ScoringSettings settings)
    {
        var protein = AchievementCalculator.LowerRate(proteinIntakeG, proteinTargetG);
        var distribution = AchievementCalculator.DistributionRate(mainMealProteinsG, settings);
        var energy = AchievementCalculator.EnergyFitness(energyIntakeKcal, energyTargetKcal, settings);

        var wDistribution = distribution is null ? 0m : settings.MuscleDistributionWeight;
        var weightSum = settings.MuscleProteinWeight + wDistribution + settings.MuscleEnergyWeight;
        var raw = settings.MuscleProteinWeight * protein + wDistribution * (distribution ?? 0m) + settings.MuscleEnergyWeight * energy;
        return ToScore(raw / weightSum);
    }

    /// <summary>
    /// 基礎 = 対象栄養素の達成率の平均 × 100 − 食塩の減点（下限 0）。
    /// <paramref name="saltMax"/> が null（目標未設定）なら減点しない。
    /// </summary>
    public static int? Base(
        IReadOnlyDictionary<string, decimal> intake,
        IReadOnlyDictionary<string, decimal> lowerTargets,
        decimal saltIntakeG,
        decimal? saltMax,
        ScoringSettings settings)
    {
        var rates = settings.BaseNutrients
            .Where(lowerTargets.ContainsKey)
            .Select(code => AchievementCalculator.LowerRate(intake.GetValueOrDefault(code), lowerTargets[code]))
            .ToList();
        if (rates.Count == 0)
        {
            return null;
        }

        var average = rates.Sum() / rates.Count * 100m;
        var penalty = saltMax is { } max ? AchievementCalculator.SaltPenalty(saltIntakeG, max, settings) : 0m;
        return (int)RoundingPolicy.Round(Math.Max(0m, average - penalty), 0);
    }

    /// <summary>帯: 80 以上 良好 / 60〜79 もう少し / 60 未満 不足気味。</summary>
    public static ScoreBand Band(int score, ScoringSettings settings) =>
        score >= settings.BandGood ? ScoreBand.Good : score >= settings.BandFair ? ScoreBand.Fair : ScoreBand.Low;

    /// <summary>7 日平均で、記録日が少なすぎてスコアを出さない（データ不足）か。</summary>
    public static bool IsInsufficientData(int recordDays, ScoringSettings settings) => recordDays < settings.InsufficientDataDays;

    private static int ToScore(decimal rate) => (int)RoundingPolicy.Round(rate * 100m, 0);
}
