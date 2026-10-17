using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// スコアの係数・重み・閾値。<b>すべて暫定</b>（docs/06.スコアリング仕様.md、Q-4）。
/// 実際の値は設定（appsettings.json の Nutrition:Scoring）から渡す。
/// </summary>
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
