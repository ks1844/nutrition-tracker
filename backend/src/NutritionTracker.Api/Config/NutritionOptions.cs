using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Api.Config;

/// <summary>
/// 栄養計算・スコアの設定（appsettings.json の Nutrition）。<b>係数・重み・閾値はすべて暫定</b>
/// （docs/06.スコアリング仕様.md §8、Q-4）。
/// </summary>
public sealed class NutritionOptions
{
    public const string Section = "Nutrition";

    /// <summary>日付の区切りに使うタイムゾーン（暫定: 0:00 区切り。Q-5）。</summary>
    public string TimeZone { get; set; } = "Asia/Tokyo";

    public TargetOptions Targets { get; set; } = new();

    public ScoringOptions Scoring { get; set; } = new();

    public sealed class TargetOptions
    {
        /// <summary>目的（skin / both / muscle）→ タンパク質の係数 g/kg</summary>
        public Dictionary<string, decimal> ProteinGPerKg { get; set; } = [];

        /// <summary>活動レベル（"1"〜"4"）→ 活動係数</summary>
        public Dictionary<string, decimal> ActivityFactor { get; set; } = [];

        public TargetSettings ToSettings() => new(
            ActivityFactor.ToDictionary(p => int.Parse(p.Key), p => p.Value),
            ProteinGPerKg.ToDictionary(p => Enum.Parse<Goal>(p.Key, ignoreCase: true), p => p.Value));
    }

    /// <summary>スコアの係数・重み・閾値（docs/06.スコアリング仕様.md §8）。未設定の項目は Domain の既定値（暫定）を使う。</summary>
    public sealed class ScoringOptions
    {
        private static readonly ScoringSettings Defaults = ScoringSettings.Default;

        public Dictionary<string, decimal> BeautyWeights { get; set; } = Defaults.BeautyWeights.ToDictionary(p => p.Key, p => p.Value);

        public MuscleOptions Muscle { get; set; } = new();

        public List<string> BaseNutrients { get; set; } = [.. Defaults.BaseNutrients];

        public SaltPenaltyOptions SaltPenalty { get; set; } = new();

        public BandOptions Bands { get; set; } = new();

        public int InsufficientDataDays { get; set; } = Defaults.InsufficientDataDays;

        public ScoringSettings ToSettings() => new(
            BeautyWeights,
            Muscle.Weights.Protein, Muscle.Weights.Distribution, Muscle.Weights.Energy,
            Muscle.ProteinPerMealThresholdG, Muscle.EnergyTolerance, Muscle.EnergyFalloff,
            BaseNutrients.Distinct().ToList(), // 既定値の入った List に設定の配列を束ねると追記されるため、重複を除く
            SaltPenalty.Multiplier, SaltPenalty.Max,
            Bands.Good, Bands.Fair,
            InsufficientDataDays);
    }

    public sealed class MuscleOptions
    {
        public MuscleWeightOptions Weights { get; set; } = new();

        public decimal ProteinPerMealThresholdG { get; set; } = ScoringSettings.Default.ProteinPerMealThresholdG;

        public decimal EnergyTolerance { get; set; } = ScoringSettings.Default.EnergyTolerance;

        public decimal EnergyFalloff { get; set; } = ScoringSettings.Default.EnergyFalloff;
    }

    public sealed class MuscleWeightOptions
    {
        public decimal Protein { get; set; } = ScoringSettings.Default.MuscleProteinWeight;

        public decimal Distribution { get; set; } = ScoringSettings.Default.MuscleDistributionWeight;

        public decimal Energy { get; set; } = ScoringSettings.Default.MuscleEnergyWeight;
    }

    public sealed class SaltPenaltyOptions
    {
        public decimal Multiplier { get; set; } = ScoringSettings.Default.SaltPenaltyMultiplier;

        public decimal Max { get; set; } = ScoringSettings.Default.SaltPenaltyMax;
    }

    public sealed class BandOptions
    {
        public int Good { get; set; } = ScoringSettings.Default.BandGood;

        public int Fair { get; set; } = ScoringSettings.Default.BandFair;
    }
}
