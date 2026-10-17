using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Domain.Tests.Scoring;

/// <summary>
/// エネルギー・タンパク質の目標値の計算のテストです（docs/06.スコアリング仕様.md §2）。
/// </summary>
public class TargetCalculatorTests
{
    /// <summary>
    /// 暫定の既定の係数。
    /// </summary>
    private static readonly TargetSettings Settings = TargetSettings.Default;

    /// <summary>
    /// 男性のエネルギー目標を確認します。
    /// 30 歳・170cm・65kg・活動レベル 2 は、基礎代謝 1567.5 に係数 1.375 を掛けて 2155 kcal になります（AC-SCORE-007）。
    /// </summary>
    [Fact]
    public void AC_SCORE_007_Energy_MifflinStJeor_Male()
    {
        // 650 + 1062.5 − 150 + 5 = 1567.5、× 1.375 = 2155.3125 → 2155
        Assert.Equal(2155, TargetCalculator.EnergyKcal(Sex.Male, 65m, 170m, 30, 2, Settings));
    }

    /// <summary>
    /// 女性のエネルギー目標は、基礎代謝の式の定数が −161 になることを確認します（30 歳・160cm・55kg・活動レベル 2 → 1704 kcal）。
    /// </summary>
    [Fact]
    public void Energy_Female_UsesMinus161()
    {
        // 10×55 + 6.25×160 − 5×30 − 161 = 1239、× 1.375 = 1703.625 → 1704
        Assert.Equal(1704, TargetCalculator.EnergyKcal(Sex.Female, 55m, 160m, 30, 2, Settings));
    }

    /// <summary>
    /// 利用者が上書きしたエネルギー目標があれば、自動算出より優先されることを確認します。
    /// </summary>
    [Fact]
    public void Energy_UserOverride_TakesPrecedence()
    {
        Assert.Equal(1800, TargetCalculator.EffectiveEnergyKcal(2155, 1800));
        Assert.Equal(2155, TargetCalculator.EffectiveEnergyKcal(2155, null));
    }

    /// <summary>
    /// タンパク質の目標が「体重 × 目的別の係数」になることを確認します。
    /// 体重 60kg で、美肌は 60.0g、両方は 84.0g、筋肉は 96.0g です（AC-SCORE-008）。
    /// </summary>
    /// <param name="goal">目的。</param>
    /// <param name="expected">期待するタンパク質の目標（g）の文字列。</param>
    [Theory]
    [InlineData(Goal.Skin, "60.0")]
    [InlineData(Goal.Both, "84.0")]
    [InlineData(Goal.Muscle, "96.0")]
    public void AC_SCORE_008_Protein_IsWeightTimesGoalCoefficient(Goal goal, string expected)
    {
        Assert.Equal(decimal.Parse(expected), TargetCalculator.ProteinG(60m, goal, Settings));
    }

    /// <summary>
    /// 指定した日の時点の満年齢が正しく求まることを確認します。
    /// 誕生日の前日は 1 つ若く、当日で加齢します。閏日（2/29）生まれは、平年は 2/28 に加齢します。
    /// </summary>
    /// <param name="birth">生年月日。</param>
    /// <param name="on">年齢を求める日。</param>
    /// <param name="expected">期待する満年齢。</param>
    [Theory]
    [InlineData("1996-04-01", "2026-03-31", 29)]
    [InlineData("1996-04-01", "2026-04-01", 30)]
    [InlineData("2000-02-29", "2026-02-28", 26)] // 閏日生まれは、平年は 2/28 に加齢（DateOnly.AddYears の仕様に合わせる）
    public void Age_IsFullYearsOnTheGivenDay(string birth, string on, int expected)
    {
        Assert.Equal(expected, TargetCalculator.AgeOn(DateOnly.Parse(birth), DateOnly.Parse(on)));
    }
}
