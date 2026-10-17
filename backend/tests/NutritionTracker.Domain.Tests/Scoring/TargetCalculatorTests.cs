using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Domain.Tests.Scoring;

public class TargetCalculatorTests
{
    private static readonly TargetSettings Settings = TargetSettings.Default;

    [Fact]
    public void AC_SCORE_007_Energy_MifflinStJeor_Male()
    {
        // 650 + 1062.5 − 150 + 5 = 1567.5、× 1.375 = 2155.3125 → 2155
        Assert.Equal(2155, TargetCalculator.EnergyKcal(Sex.Male, 65m, 170m, 30, 2, Settings));
    }

    [Fact]
    public void Energy_Female_UsesMinus161()
    {
        // 10×55 + 6.25×160 − 5×30 − 161 = 1239、× 1.375 = 1703.625 → 1704
        Assert.Equal(1704, TargetCalculator.EnergyKcal(Sex.Female, 55m, 160m, 30, 2, Settings));
    }

    [Fact]
    public void Energy_UserOverride_TakesPrecedence()
    {
        Assert.Equal(1800, TargetCalculator.EffectiveEnergyKcal(2155, 1800));
        Assert.Equal(2155, TargetCalculator.EffectiveEnergyKcal(2155, null));
    }

    [Theory]
    [InlineData(Goal.Skin, "60.0")]
    [InlineData(Goal.Both, "84.0")]
    [InlineData(Goal.Muscle, "96.0")]
    public void AC_SCORE_008_Protein_IsWeightTimesGoalCoefficient(Goal goal, string expected)
    {
        Assert.Equal(decimal.Parse(expected), TargetCalculator.ProteinG(60m, goal, Settings));
    }

    [Theory]
    [InlineData("1996-04-01", "2026-03-31", 29)]
    [InlineData("1996-04-01", "2026-04-01", 30)]
    [InlineData("2000-02-29", "2026-02-28", 25)]
    public void Age_IsFullYearsOnTheGivenDay(string birth, string on, int expected)
    {
        Assert.Equal(expected, TargetCalculator.AgeOn(DateOnly.Parse(birth), DateOnly.Parse(on)));
    }
}
