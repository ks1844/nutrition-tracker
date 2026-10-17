using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Domain.Tests.Scoring;

/// <summary>達成率の正解データ（docs/06.スコアリング仕様.md §1, §6）。目標値はテスト用の仮の値。</summary>
public class AchievementCalculatorTests
{
    private static readonly ScoringSettings Settings = ScoringSettings.Default;

    [Theory]
    [InlineData(78, 100, "0.780000")]
    [InlineData(9, 6, "1.000000")] // 100% で頭打ち（他の栄養素の不足を相殺させない）
    [InlineData(450, 700, "0.642857")]
    [InlineData(8, 10, "0.800000")]
    [InlineData(62, 90, "0.688889")]
    public void AC_SCORE_001_002_LowerRate_IsCappedAtOne_AndSixDecimals(int intake, int target, string expected)
    {
        Assert.Equal(decimal.Parse(expected), AchievementCalculator.LowerRate(intake, target));
    }

    [Fact]
    public void DistributionRate_IsShareOfMainMealsWithEnoughProtein()
    {
        Assert.Equal(0.666667m, AchievementCalculator.DistributionRate([25m, 22m, 15m], Settings)); // 25・22 は 20g 以上
        Assert.Null(AchievementCalculator.DistributionRate([], Settings)); // 朝昼夕の記録なし → 除外
    }

    [Theory]
    [InlineData(0.90, 1.10, 1.0)]
    [InlineData(0.70, 1.30, 0.5)]
    [InlineData(0.50, 1.50, 0.0)]
    public void AC_SCORE_004_EnergyFitness_Boundaries(double lowRatio, double highRatio, double expected)
    {
        Assert.Equal((decimal)expected, AchievementCalculator.EnergyFitness((decimal)lowRatio, 1m, Settings));
        Assert.Equal((decimal)expected, AchievementCalculator.EnergyFitness((decimal)highRatio, 1m, Settings));
    }

    [Fact]
    public void SaltPenalty_OnlyWhenOverLimit_AndCapped()
    {
        Assert.Equal(4.0m, AchievementCalculator.SaltPenalty(8.4m, 7.0m, Settings)); // 20 × (1.2 − 1)
        Assert.Equal(0m, AchievementCalculator.SaltPenalty(6.9m, 7.0m, Settings));
        Assert.Equal(10m, AchievementCalculator.SaltPenalty(14m, 7m, Settings)); // 20 → 上限 10
    }
}
