using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Api.Config;

/// <summary>
/// 栄養計算・スコア・マスタ取り込みの設定です（appsettings.json の Nutrition）。係数・重み・閾値はすべて暫定です（docs/06.スコアリング仕様.md §8、Q-4）。
/// </summary>
public sealed class NutritionOptions
{
    /// <summary>
    /// 設定ファイルでのこの設定の名前（Nutrition）。
    /// </summary>
    public const string Section = "Nutrition";

    /// <summary>
    /// 日付の区切りに使うタイムゾーン（暫定: 0 時区切り。Q-5）。
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Tokyo";

    /// <summary>
    /// 目標値の算出に使う係数。
    /// </summary>
    public TargetOptions Targets { get; set; } = new();

    /// <summary>
    /// スコアの計算に使う重みや閾値。
    /// </summary>
    public ScoringOptions Scoring { get; set; } = new();

    /// <summary>
    /// 起動時のマスタ CSV 取り込みの設定。
    /// </summary>
    public MasterImportOptions MasterImport { get; set; } = new();

    /// <summary>
    /// 目標値の算出に使う係数の設定です。
    /// </summary>
    public sealed class TargetOptions
    {
        /// <summary>
        /// 目的（skin / both / muscle）→ 体重 1kg あたりのタンパク質量（g）。
        /// </summary>
        public Dictionary<string, decimal> ProteinGPerKg { get; set; } = [];

        /// <summary>
        /// 活動レベル（"1"〜"4"）→ 活動係数。
        /// </summary>
        public Dictionary<string, decimal> ActivityFactor { get; set; } = [];

        /// <summary>
        /// 設定の値を、目標値の計算に使う係数（活動レベルの整数と目的の種類つき）に変換します。
        /// </summary>
        /// <returns>目標値の算出に使う係数。</returns>
        public TargetSettings ToSettings() => new(
            ActivityFactor.ToDictionary(p => int.Parse(p.Key), p => p.Value),
            ProteinGPerKg.ToDictionary(p => Enum.Parse<Goal>(p.Key, ignoreCase: true), p => p.Value));
    }

    /// <summary>
    /// スコアの係数・重み・閾値の設定です。設定にない項目は、暫定の既定値を使います。
    /// </summary>
    public sealed class ScoringOptions
    {
        /// <summary>
        /// 暫定の既定値。
        /// </summary>
        private static readonly ScoringSettings Defaults = ScoringSettings.Default;

        /// <summary>
        /// 美肌スコアに使う栄養素コード → 重み。
        /// </summary>
        public Dictionary<string, decimal> BeautyWeights { get; set; } = Defaults.BeautyWeights.ToDictionary(p => p.Key, p => p.Value);

        /// <summary>
        /// 筋肉スコアの設定。
        /// </summary>
        public MuscleOptions Muscle { get; set; } = new();

        /// <summary>
        /// 基礎スコアに使う栄養素コード。
        /// </summary>
        public List<string> BaseNutrients { get; set; } = [.. Defaults.BaseNutrients];

        /// <summary>
        /// 食塩の減点の設定。
        /// </summary>
        public SaltPenaltyOptions SaltPenalty { get; set; } = new();

        /// <summary>
        /// スコアの区分（良好・もう少し）の境目。
        /// </summary>
        public BandOptions Bands { get; set; } = new();

        /// <summary>
        /// 7 日平均でスコアを出すのに必要な記録日数。
        /// </summary>
        public int InsufficientDataDays { get; set; } = Defaults.InsufficientDataDays;

        /// <summary>
        /// 設定の値を、スコアの計算に使う係数・重み・閾値に変換します。
        /// </summary>
        /// <returns>スコアの計算に使う設定。</returns>
        public ScoringSettings ToSettings() => new(
            BeautyWeights,
            Muscle.Weights.Protein, Muscle.Weights.Distribution, Muscle.Weights.Energy,
            Muscle.ProteinPerMealThresholdG, Muscle.EnergyTolerance, Muscle.EnergyFalloff,
            BaseNutrients.Distinct().ToList(), // 既定値の入った List に設定の配列を束ねると追記されるため、重複を除く
            SaltPenalty.Multiplier, SaltPenalty.Max,
            Bands.Good, Bands.Fair,
            InsufficientDataDays);
    }

    /// <summary>
    /// 筋肉スコアの設定です。
    /// </summary>
    public sealed class MuscleOptions
    {
        /// <summary>
        /// 3 つの要素の重み。
        /// </summary>
        public MuscleWeightOptions Weights { get; set; } = new();

        /// <summary>
        /// 1 食でタンパク質がこの量（g）以上なら「十分」と数える閾値。
        /// </summary>
        public decimal ProteinPerMealThresholdG { get; set; } = ScoringSettings.Default.ProteinPerMealThresholdG;

        /// <summary>
        /// エネルギーが目標からずれても満点とする許容幅。
        /// </summary>
        public decimal EnergyTolerance { get; set; } = ScoringSettings.Default.EnergyTolerance;

        /// <summary>
        /// 許容幅を超えてから、点が 0 になるまでの幅。
        /// </summary>
        public decimal EnergyFalloff { get; set; } = ScoringSettings.Default.EnergyFalloff;
    }

    /// <summary>
    /// 筋肉スコアの 3 つの要素の重みです。
    /// </summary>
    public sealed class MuscleWeightOptions
    {
        /// <summary>
        /// タンパク質の達成率の重み。
        /// </summary>
        public decimal Protein { get; set; } = ScoringSettings.Default.MuscleProteinWeight;

        /// <summary>
        /// 朝昼夕への分配率の重み。
        /// </summary>
        public decimal Distribution { get; set; } = ScoringSettings.Default.MuscleDistributionWeight;

        /// <summary>
        /// エネルギー充足の重み。
        /// </summary>
        public decimal Energy { get; set; } = ScoringSettings.Default.MuscleEnergyWeight;
    }

    /// <summary>
    /// 食塩による減点の設定です。
    /// </summary>
    public sealed class SaltPenaltyOptions
    {
        /// <summary>
        /// 上限を超えた割合に掛ける係数。
        /// </summary>
        public decimal Multiplier { get; set; } = ScoringSettings.Default.SaltPenaltyMultiplier;

        /// <summary>
        /// 減点の上限（点）。
        /// </summary>
        public decimal Max { get; set; } = ScoringSettings.Default.SaltPenaltyMax;
    }

    /// <summary>
    /// スコアの区分の境目の設定です。
    /// </summary>
    public sealed class BandOptions
    {
        /// <summary>
        /// 「良好」とするスコアの下限。
        /// </summary>
        public int Good { get; set; } = ScoringSettings.Default.BandGood;

        /// <summary>
        /// 「もう少し」とするスコアの下限。
        /// </summary>
        public int Fair { get; set; } = ScoringSettings.Default.BandFair;
    }

    /// <summary>
    /// 起動時のマスタ CSV 取り込みの設定です（docs/07.マスタデータ仕様.md §5）。
    /// </summary>
    public sealed class MasterImportOptions
    {
        /// <summary>
        /// 取り込みを行うか。
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// CSV を置くフォルダ。相対パスはアプリのコンテンツルート（開発時はプロジェクトのフォルダ）からの位置です。
        /// </summary>
        public string Path { get; set; } = "Master";
    }
}
