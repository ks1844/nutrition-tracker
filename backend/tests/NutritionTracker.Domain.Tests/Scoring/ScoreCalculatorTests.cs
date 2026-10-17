using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Domain.Tests.Scoring;

/// <summary>
/// 美肌・筋肉・基礎スコアの正解データのテストです（docs/06.スコアリング仕様.md §6）。
/// 目標値はテスト用の仮の値で、食事摂取基準の公式値ではありません。
/// </summary>
public class ScoreCalculatorTests
{
    /// <summary>
    /// 暫定の既定の設定。
    /// </summary>
    private static readonly ScoringSettings Settings = ScoringSettings.Default;

    /// <summary>
    /// 美肌スコアが、頭打ちした達成率の重みつき平均になることを確認します。
    /// ビタミン C 0.78、ビタミン E 1.0（頭打ち）、ビタミン A 0.642857、亜鉛 0.8（重み 0.5）で、0.806531 → 81 になります（AC-SCORE-001）。
    /// </summary>
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

    /// <summary>
    /// アスタキサンチンは目標があってもスコアに含まれないことを確認します（ビタミン C だけで 100）。
    /// </summary>
    [Fact]
    public void Beauty_Astaxanthin_IsNotIncluded()
    {
        var intake = new Dictionary<string, decimal> { [NutrientCodes.VitaminC] = 100m, [NutrientCodes.Astaxanthin] = 0m };
        var targets = new Dictionary<string, decimal> { [NutrientCodes.VitaminC] = 100m, [NutrientCodes.Astaxanthin] = 5m };

        Assert.Equal(100, ScoreCalculator.Beauty(intake, targets, Settings));
    }

    /// <summary>
    /// 筋肉スコアを確認します。
    /// タンパク質の達成率 0.688889、分配率 2/3、エネルギー充足 1.0 を 0.6 / 0.2 / 0.2 で合わせて 0.746667 → 75 になります（AC-SCORE-002）。
    /// </summary>
    [Fact]
    public void AC_SCORE_002_Muscle_ProteinDistributionAndEnergy()
    {
        // 目標 = 60kg × 1.5 = 90g（仮係数）、摂取 62g → 0.688889。朝昼夕 25/22/15g → 2/3。エネルギー 1800÷2000 = 0.9 → 1.0
        var score = ScoreCalculator.Muscle(62m, 90m, [25m, 22m, 15m], 1800m, 2000m, Settings);

        Assert.Equal(75, score); // 0.6×0.688889 + 0.2×0.666667 + 0.2×1.0 = 0.746667
    }

    /// <summary>
    /// 朝昼夕の記録がないとき、分配率を外して重みを 0.6 : 0.2 に直して計算することを確認します（0.766667 → 77。AC-SCORE-010）。
    /// </summary>
    [Fact]
    public void AC_SCORE_010_Muscle_WithoutMainMeals_ReweightsWithoutDistribution()
    {
        // 間食のみ（朝昼夕なし）→ 分配率を除外し、0.6 : 0.2 に再正規化: (0.6×0.688889 + 0.2×1) ÷ 0.8 = 0.766667
        Assert.Equal(77, ScoreCalculator.Muscle(62m, 90m, [], 1800m, 2000m, Settings));
    }

    /// <summary>
    /// 基礎スコアが達成率の平均から食塩の減点を引いたものになることを確認します。
    /// 全栄養素の達成率 0.8（平均 80）で、食塩 8.4g（上限 7.0g）なら 4 点減って 76、上限以内なら減点なしで 80 です（AC-SCORE-003）。
    /// </summary>
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

    /// <summary>
    /// 食塩の減点が大きくてもスコアがマイナスにならず、0 で止まることを確認します。
    /// </summary>
    [Fact]
    public void Base_SaltPenalty_IsCappedAndScoreNeverNegative()
    {
        var targets = Settings.BaseNutrients.ToDictionary(c => c, _ => 100m);
        var intake = Settings.BaseNutrients.ToDictionary(c => c, _ => 0m);

        Assert.Equal(0, ScoreCalculator.Base(intake, targets, 14m, 7m, Settings));
    }

    /// <summary>
    /// スコアの区分の境目を確認します。80 以上は良好、60〜79 はもう少し、59 以下は不足気味です（AC-SCORE-009）。
    /// </summary>
    /// <param name="score">スコア。</param>
    /// <param name="expected">期待する区分。</param>
    [Theory]
    [InlineData(80, ScoreBand.Good)]
    [InlineData(79, ScoreBand.Fair)]
    [InlineData(60, ScoreBand.Fair)]
    [InlineData(59, ScoreBand.Low)]
    public void AC_SCORE_009_Band(int score, ScoreBand expected)
    {
        Assert.Equal(expected, ScoreCalculator.Band(score, Settings));
    }

    /// <summary>
    /// 7 日平均で、記録日が 3 日未満ならデータ不足になることを確認します（2 日は不足、3 日は不足でない。AC-SCORE-005）。
    /// </summary>
    /// <param name="recordDays">記録のある日数。</param>
    /// <param name="expected">データ不足かどうかの期待値。</param>
    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void AC_SCORE_005_InsufficientData_WhenFewerThanThreeRecordDays(int recordDays, bool expected)
    {
        Assert.Equal(expected, ScoreCalculator.IsInsufficientData(recordDays, Settings));
    }
}
