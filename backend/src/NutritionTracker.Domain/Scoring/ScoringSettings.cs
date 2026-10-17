using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// スコアの係数・重み・閾値です。すべて暫定値です（docs/06.スコアリング仕様.md、Q-4）。
/// 実際の値は設定ファイル（appsettings.json の Nutrition:Scoring）から渡します。
/// </summary>
/// <param name="BeautyWeights">美肌スコアに使う栄養素コード → 重み。</param>
/// <param name="MuscleProteinWeight">筋肉スコアのうち、タンパク質の達成率の重み。</param>
/// <param name="MuscleDistributionWeight">筋肉スコアのうち、朝昼夕への分配率の重み。</param>
/// <param name="MuscleEnergyWeight">筋肉スコアのうち、エネルギー充足の重み。</param>
/// <param name="ProteinPerMealThresholdG">1 食でこの量（g）以上のタンパク質があれば「十分」と数える閾値。</param>
/// <param name="EnergyTolerance">エネルギーが目標からずれても満点とする許容幅（0.10 = ±10%）。</param>
/// <param name="EnergyFalloff">許容幅を超えたとき、点が 0 になるまでの幅（0.40）。</param>
/// <param name="BaseNutrients">基礎スコアに使う栄養素コード。</param>
/// <param name="SaltPenaltyMultiplier">食塩が上限を超えたときの減点の係数。</param>
/// <param name="SaltPenaltyMax">食塩による減点の上限（点）。</param>
/// <param name="BandGood">「良好」とするスコアの下限。</param>
/// <param name="BandFair">「もう少し」とするスコアの下限。</param>
/// <param name="InsufficientDataDays">7 日平均でスコアを出すのに必要な記録日数。</param>
public sealed record ScoringSettings(
    IReadOnlyDictionary<string, decimal> BeautyWeights,
    decimal MuscleProteinWeight,
    decimal MuscleDistributionWeight,
    decimal MuscleEnergyWeight,
    decimal ProteinPerMealThresholdG,
    decimal EnergyTolerance,
    decimal EnergyFalloff,
    IReadOnlyList<string> BaseNutrients,
    decimal SaltPenaltyMultiplier,
    decimal SaltPenaltyMax,
    int BandGood,
    int BandFair,
    int InsufficientDataDays)
{
    /// <summary>
    /// 設定がないときに使う暫定の既定値です。
    /// </summary>
    public static ScoringSettings Default { get; } = new(
        new Dictionary<string, decimal>
        {
            [NutrientCodes.VitaminC] = 1m, [NutrientCodes.VitaminE] = 1m, [NutrientCodes.VitaminARae] = 1m, [NutrientCodes.Zinc] = 0.5m,
        },
        MuscleProteinWeight: 0.6m,
        MuscleDistributionWeight: 0.2m,
        MuscleEnergyWeight: 0.2m,
        ProteinPerMealThresholdG: 20m,
        EnergyTolerance: 0.10m,
        EnergyFalloff: 0.40m,
        BaseNutrients:
        [
            NutrientCodes.DietaryFiber, NutrientCodes.Calcium, NutrientCodes.Iron, NutrientCodes.Magnesium, NutrientCodes.Potassium,
            NutrientCodes.VitaminB1, NutrientCodes.VitaminB2, NutrientCodes.VitaminB6, NutrientCodes.VitaminB12, NutrientCodes.Folate,
            NutrientCodes.VitaminD,
        ],
        SaltPenaltyMultiplier: 20m,
        SaltPenaltyMax: 10m,
        BandGood: 80,
        BandFair: 60,
        InsufficientDataDays: 3);
}
