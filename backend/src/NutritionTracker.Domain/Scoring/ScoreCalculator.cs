using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// スコアの区分です。スコアが高いほど良好です。
/// </summary>
public enum ScoreBand
{
    /// <summary>
    /// 良好（既定で 80 以上）。
    /// </summary>
    Good,

    /// <summary>
    /// もう少し（既定で 60〜79）。
    /// </summary>
    Fair,

    /// <summary>
    /// 不足気味（既定で 60 未満）。
    /// </summary>
    Low,
}

/// <summary>
/// 美肌・筋肉・基礎の 3 つのスコアを求めます（docs/06.スコアリング仕様.md §3〜§5）。いずれも整数に四捨五入します。
/// </summary>
public static class ScoreCalculator
{
    /// <summary>
    /// 美肌スコアを求めます。ビタミン C・E・A・亜鉛などの達成率を重みづけして平均し、100 倍します。
    /// 目標がない栄養素は計算から外し、残りの重みで平均し直します。アスタキサンチンはスコアに含めません。
    /// </summary>
    /// <param name="intake">栄養素コード → 摂取量。</param>
    /// <param name="lowerTargets">栄養素コード → 下限型の目標量。</param>
    /// <param name="settings">重みなどの設定。</param>
    /// <returns>美肌スコア。対象の栄養素にひとつも目標がなければ null。</returns>
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
    /// 筋肉スコアを求めます。タンパク質の達成率・朝昼夕への分配率・エネルギー充足を重みづけして合計し、100 倍します。
    /// 朝昼夕の記録がないときは分配率を外し、残りの重みで計算し直します。
    /// </summary>
    /// <param name="proteinIntakeG">タンパク質の摂取量（g）。</param>
    /// <param name="proteinTargetG">タンパク質の目標（g）。</param>
    /// <param name="mainMealProteinsG">朝・昼・夕それぞれの食事のタンパク質量（g）。</param>
    /// <param name="energyIntakeKcal">エネルギーの摂取量（kcal）。</param>
    /// <param name="energyTargetKcal">エネルギーの目標（kcal）。</param>
    /// <param name="settings">重みや閾値の設定。</param>
    /// <returns>筋肉スコア。</returns>
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
    /// 基礎スコアを求めます。食物繊維・ミネラル・ビタミンなど 11 栄養素の達成率の平均を 100 倍し、食塩が上限を超えていれば減点します（下限 0）。
    /// </summary>
    /// <param name="intake">栄養素コード → 摂取量。</param>
    /// <param name="lowerTargets">栄養素コード → 下限型の目標量。</param>
    /// <param name="saltIntakeG">食塩相当量の摂取量（g）。</param>
    /// <param name="saltMax">食塩相当量の上限（g）。未設定なら減点しません。</param>
    /// <param name="settings">対象栄養素や減点の設定。</param>
    /// <returns>基礎スコア。対象の栄養素にひとつも目標がなければ null。</returns>
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

    /// <summary>
    /// スコアから区分（良好・もう少し・不足気味）を決めます。
    /// </summary>
    /// <param name="score">スコア。</param>
    /// <param name="settings">区分の境目の設定。</param>
    /// <returns>スコアの区分。</returns>
    public static ScoreBand Band(int score, ScoringSettings settings) =>
        score >= settings.BandGood ? ScoreBand.Good : score >= settings.BandFair ? ScoreBand.Fair : ScoreBand.Low;

    /// <summary>
    /// 7 日平均でスコアを出すには記録が少なすぎる（データ不足）かどうかを判定します。
    /// </summary>
    /// <param name="recordDays">記録のある日数。</param>
    /// <param name="settings">必要な日数の設定。</param>
    /// <returns>記録日が必要な日数に足りなければ true。</returns>
    public static bool IsInsufficientData(int recordDays, ScoringSettings settings) => recordDays < settings.InsufficientDataDays;

    /// <summary>
    /// 0〜1 の比率を、0〜100 の整数のスコアに四捨五入します。
    /// </summary>
    /// <param name="rate">比率。</param>
    /// <returns>整数のスコア。</returns>
    private static int ToScore(decimal rate) => (int)RoundingPolicy.Round(rate * 100m, 0);
}
