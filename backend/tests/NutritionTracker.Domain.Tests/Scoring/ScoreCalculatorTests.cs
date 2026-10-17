using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Domain.Tests.Scoring;

/// <summary>スコアの正解データ（docs/06.スコアリング仕様.md §6）。目標値はテスト用の仮の値で、食事摂取基準の公式値ではない。</summary>
public class ScoreCalculatorTests
{
    private static readonly ScoringSettings Settings = ScoringSettings.Default;

    [Fact]
    public void AC_SCORE_001_Beauty_IsWeightedAverageOfCappedRates()
    {
        var intake = new Dictionary<string, decimal>
        {
            [NutrientCodes.VitaminC] = 78m, [NutrientCodes.VitaminE] = 9m, [NutrientCodes.VitaminARae] = 450m, [NutrientCodes.Zinc] = 8m,
        };
        var targets = new Dictionary<string, decimal>
        {
            [NutrientCodes.VitaminC] = 100m, [NutrientCodes.VitaminE] = 6m, [NutrientCodes.VitaminARae] = 700m, [NutrientCodes.Zinc] = 10m,
        };

        // 0.78 / 1.0（頭打ち）/ 0.642857 / 0.8（重み 0.5）→ (0.78+1+0.642857+0.4)÷3.5 = 0.806531 → 81
        Assert.Equal(81, ScoreCalculator.Beauty(intake, targets, Settings));
    }

    [Fact]
    public void Beauty_Astaxanthin_IsNotIncluded()
    {
        var intake = new Dictionary<string, decimal> { [NutrientCodes.VitaminC] = 100m, [NutrientCodes.Astaxanthin] = 0m };
        var targets = new Dictionary<string, decimal> { [NutrientCodes.VitaminC] = 100m, [NutrientCodes.Astaxanthin] = 5m };

        Assert.Equal(100, ScoreCalculator.Beauty(intake, targets, Settings));
    }

    [Fact]
    public void AC_SCORE_002_Muscle_ProteinDistributionAndEnergy()
    {
        // 目標 = 60kg × 1.5 = 90g（仮係数）、摂取 62g → 0.688889。朝昼夕 25/22/15g → 2/3。エネルギー 1800÷2000 = 0.9 → 1.0
        var score = ScoreCalculator.Muscle(62m, 90m, [25m, 22m, 15m], 1800m, 2000m, Settings);

        Assert.Equal(75, score); // 0.6×0.688889 + 0.2×0.666667 + 0.2×1.0 = 0.746667
    }

    [Fact]
    public void AC_SCORE_010_Muscle_WithoutMainMeals_ReweightsWithoutDistribution()
    {
        // 間食のみ（朝昼夕なし）→ 分配率を除外し、0.6 : 0.2 に再正規化: (0.6×0.688889 + 0.2×1) ÷ 0.8 = 0.766667
        Assert.Equal(77, ScoreCalculator.Muscle(62m, 90m, [], 1800m, 2000m, Settings));
    }

    [Fact]
    public void AC_SCORE_003_Base_SaltPenalty()
    {
        var targets = Settings.BaseNutrients.ToDictionary(c => c, _ => 100m);
        var intake = Settings.BaseNutrients.ToDictionary(c => c, _ => 80m); // 達成率 すべて 0.8 → 平均 80

        // 食塩 8.4g / 仮の上限 7.0g → 減点 min(10, 20×(1.2−1)) = 4.0 → 76
        Assert.Equal(76, ScoreCalculator.Base(intake, targets, 8.4m, 7.0m, Settings));
        // 上限以内なら減点なし
        Assert.Equal(80, ScoreCalculator.Base(intake, targets, 6.9m, 7.0m, Settings));
    }

    [Fact]
    public void Base_SaltPenalty_IsCappedAndScoreNeverNegative()
    {
        var targets = Settings.BaseNutrients.ToDictionary(c => c, _ => 100m);
        var intake = Settings.BaseNutrients.ToDictionary(c => c, _ => 0m);

        Assert.Equal(0, ScoreCalculator.Base(intake, targets, 14m, 7m, Settings));
    }

    [Theory]
    [InlineData(80, ScoreBand.Good)]
    [InlineData(79, ScoreBand.Fair)]
    [InlineData(60, ScoreBand.Fair)]
    [InlineData(59, ScoreBand.Low)]
    public void AC_SCORE_009_Band(int score, ScoreBand expected)
    {
        Assert.Equal(expected, ScoreCalculator.Band(score, Settings));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void AC_SCORE_005_InsufficientData_WhenFewerThanThreeRecordDays(int recordDays, bool expected)
    {
        Assert.Equal(expected, ScoreCalculator.IsInsufficientData(recordDays, Settings));
    }
}
