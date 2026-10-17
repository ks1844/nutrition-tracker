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
}
