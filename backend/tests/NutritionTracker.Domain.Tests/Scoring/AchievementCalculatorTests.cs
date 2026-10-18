using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Domain.Tests.Scoring;

/// <summary>
/// 達成率の正解データのテストです（docs/06.スコアリング仕様.md §1, §6）。目標値はテスト用の仮の値です。
/// </summary>
public class AchievementCalculatorTests
{
    /// <summary>
    /// 暫定の既定の設定。
    /// </summary>
    private static readonly ScoringSettings Settings = ScoringSettings.Default;

    /// <summary>
    /// 下限型の達成率が「摂取量 ÷ 目標」になり、1.0 で頭打ちされ、小数 6 桁になることを確認します。
    /// 摂取が目標を超える 9 ÷ 6 は 1.0 のままで、他の栄養素の不足を埋め合わせません。
    /// </summary>
    /// <param name="intake">摂取量。</param>
    /// <param name="target">目標量。</param>
    /// <param name="expected">期待する達成率（小数 6 桁の文字列）。</param>
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

    /// <summary>
    /// 朝昼夕のうちタンパク質が 20g 以上の食事の割合を確認します（25g・22g・15g → 2/3）。
    /// 朝昼夕の記録がないときは、この要素を外すため null になります。
    /// </summary>
    [Fact]
    public void DistributionRate_IsShareOfMainMealsWithEnoughProtein()
    {
        Assert.Equal(0.666667m, AchievementCalculator.DistributionRate([25m, 22m, 15m], Settings)); // 25・22 は 20g 以上
        Assert.Null(AchievementCalculator.DistributionRate([], Settings)); // 朝昼夕の記録なし → 除外
    }

    /// <summary>
    /// エネルギー充足が、目標との差に応じて下がることを確認します。
    /// 摂取が目標の ±10% なら 1.0、±30% なら 0.5、±50% なら 0.0 です。多すぎる場合も少なすぎる場合も同じです。
    /// </summary>
    /// <param name="lowRatio">目標に対して少なめのときの摂取の比率。</param>
    /// <param name="highRatio">目標に対して多めのときの摂取の比率。</param>
    /// <param name="expected">期待するエネルギー充足。</param>
    [Theory]
    [InlineData(0.90, 1.10, 1.0)]
    [InlineData(0.70, 1.30, 0.5)]
    [InlineData(0.50, 1.50, 0.0)]
    public void AC_SCORE_004_EnergyFitness_Boundaries(double lowRatio, double highRatio, double expected)
    {
        Assert.Equal((decimal)expected, AchievementCalculator.EnergyFitness((decimal)lowRatio, 1m, Settings));
        Assert.Equal((decimal)expected, AchievementCalculator.EnergyFitness((decimal)highRatio, 1m, Settings));
    }

    /// <summary>
    /// 食塩の減点が、上限を超えたときだけ発生し、上限点で止まることを確認します。
    /// 8.4g（上限 7.0g）は 4 点、6.9g は 0 点、14g は 10 点（上限点）です。
    /// </summary>
    [Fact]
    public void SaltPenalty_OnlyWhenOverLimit_AndCapped()
    {
        Assert.Equal(4.0m, AchievementCalculator.SaltPenalty(8.4m, 7.0m, Settings)); // 20 × (1.2 − 1)
        Assert.Equal(0m, AchievementCalculator.SaltPenalty(6.9m, 7.0m, Settings));
        Assert.Equal(10m, AchievementCalculator.SaltPenalty(14m, 7m, Settings)); // 20 → 上限 10
    }
}
