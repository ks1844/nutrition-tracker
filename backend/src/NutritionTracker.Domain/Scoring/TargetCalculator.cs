using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Domain.Scoring;

/// <summary>
/// プロフィールから、エネルギーとタンパク質の 1 日の目標を求めます（docs/06.スコアリング仕様.md §2）。
/// </summary>
public static class TargetCalculator
{
    /// <summary>
    /// 1 日のエネルギー目標（kcal）を求めます（基礎代謝 × 活動係数）。
    /// 基礎代謝は Mifflin-St Jeor 式（男性: 10×体重＋6.25×身長−5×年齢＋5、女性: 同式で末尾が−161）で、結果は整数に四捨五入します。
    /// </summary>
    /// <param name="sex">性別。</param>
    /// <param name="weightKg">体重（kg）。</param>
    /// <param name="heightCm">身長（cm）。</param>
    /// <param name="age">満年齢。</param>
    /// <param name="activityLevel">活動レベル（1〜4）。</param>
    /// <param name="settings">活動係数などの設定。</param>
    /// <returns>エネルギー目標（kcal）。</returns>
    public static int EnergyKcal(Sex sex, decimal weightKg, decimal heightCm, int age, int activityLevel, TargetSettings settings)
    {
        var bmr = 10m * weightKg + 6.25m * heightCm - 5m * age + (sex == Sex.Male ? 5m : -161m);
        return (int)RoundingPolicy.Round(bmr * settings.ActivityFactor[activityLevel], 0);
    }

    /// <summary>
    /// 実際に使うエネルギー目標を決めます。利用者が上書きしていればそれを、なければ自動算出の値を使います。
    /// </summary>
    /// <param name="autoKcal">自動算出したエネルギー目標（kcal）。</param>
    /// <param name="overrideKcal">利用者が手入力した目標（kcal）。なければ null。</param>
    /// <returns>採用するエネルギー目標（kcal）。</returns>
    public static int EffectiveEnergyKcal(int autoKcal, int? overrideKcal) => overrideKcal ?? autoKcal;

    /// <summary>
    /// 1 日のタンパク質目標（g）を求めます（体重 × 目的別の係数）。小数 1 桁に四捨五入します。
    /// </summary>
    /// <param name="weightKg">体重（kg）。</param>
    /// <param name="goal">目的（美肌・筋肉・両方）。</param>
    /// <param name="settings">目的別の係数（g/kg）の設定。</param>
    /// <returns>タンパク質の目標（g）。</returns>
    public static decimal ProteinG(decimal weightKg, Goal goal, TargetSettings settings) =>
        RoundingPolicy.Round(weightKg * settings.ProteinGPerKg[goal], 1);

    /// <summary>
    /// 指定した日の時点での満年齢を求めます（その年の誕生日がまだなら 1 つ引きます）。
    /// </summary>
    /// <param name="birthDate">生年月日。</param>
    /// <param name="on">年齢を求める日。</param>
    /// <returns>満年齢。</returns>
    public static int AgeOn(DateOnly birthDate, DateOnly on)
    {
        var age = on.Year - birthDate.Year;
        return on < birthDate.AddYears(age) ? age - 1 : age;
    }
}
