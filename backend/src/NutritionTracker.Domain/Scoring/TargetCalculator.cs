using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>エネルギー・タンパク質の目標値（docs/06.スコアリング仕様.md §2）。</summary>
public static class TargetCalculator
{
    /// <summary>
    /// エネルギー（kcal）= Mifflin-St Jeor × 活動係数。整数に四捨五入（HALF_UP）。
    /// 男性 10W+6.25H−5A+5、女性 10W+6.25H−5A−161。
    /// </summary>
    public static int EnergyKcal(Sex sex, decimal weightKg, decimal heightCm, int age, int activityLevel, TargetSettings settings)
    {
        var bmr = 10m * weightKg + 6.25m * heightCm - 5m * age + (sex == Sex.Male ? 5m : -161m);
        return (int)RoundingPolicy.Round(bmr * settings.ActivityFactor[activityLevel], 0);
    }

    /// <summary>ユーザーが上書きしていればそれを優先する。</summary>
    public static int EffectiveEnergyKcal(int autoKcal, int? overrideKcal) => overrideKcal ?? autoKcal;

    /// <summary>タンパク質（g）= 体重 × 目的別の係数（g/kg）。小数 1 桁。</summary>
    public static decimal ProteinG(decimal weightKg, Goal goal, TargetSettings settings) =>
        RoundingPolicy.Round(weightKg * settings.ProteinGPerKg[goal], 1);

    /// <summary>対象日時点の満年齢。</summary>
    public static int AgeOn(DateOnly birthDate, DateOnly on)
    {
        var age = on.Year - birthDate.Year;
        return on < birthDate.AddYears(age) ? age - 1 : age;
    }
}
