using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NutritionTracker.Api.Config;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Scoring;

namespace NutritionTracker.Api.Services;

/// <summary>
/// プロフィールの取得・保存と、目標（エネルギー・タンパク質・栄養目標）の算出を行うサービスです。
/// </summary>
/// <param name="db">データベースへの窓口。</param>
/// <param name="clock">今日の日付。</param>
/// <param name="options">係数などの設定。</param>
public class ProfileService(AppDbContext db, AppClock clock, IOptions<NutritionOptions> options)
{
    /// <summary>
    /// 受け付ける性別。
    /// </summary>
    private static readonly string[] Sexes = ["male", "female"];
    /// <summary>
    /// 受け付ける目的。
    /// </summary>
    private static readonly string[] Goals = ["skin", "muscle", "both"];

    /// <summary>
    /// プロフィールと算出した目標を返します（API-04）。
    /// </summary>
    /// <returns>プロフィールと目標。未登録なら 404（画面はプロフィール設定へ案内します）。</returns>
    public async Task<ProfileDto> GetAsync()
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == User.FixedUserId)
            ?? throw new NotFoundException("プロフィールが未登録です");
        return await ToDtoAsync(profile);
    }

    /// <summary>
    /// プロフィールを登録・更新します（API-05）。なければ作り、あれば上書きします。身長・体重は小数 1 桁に丸めます。
    /// </summary>
    /// <param name="input">入力内容（検証して、問題があれば項目別の 400 エラー）。</param>
    /// <returns>保存したプロフィールと算出した目標。</returns>
    public async Task<ProfileDto> SaveAsync(ProfileInput input)
    {
        Validate(input);
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == User.FixedUserId);
        if (profile is null)
        {
            profile = new UserProfile { UserId = User.FixedUserId };
            db.UserProfiles.Add(profile);
        }
        profile.Sex = input.Sex!;
        profile.BirthDate = input.BirthDate!.Value;
        profile.HeightCm = RoundingPolicy.Round(input.HeightCm!.Value, 1);
        profile.WeightKg = RoundingPolicy.Round(input.WeightKg!.Value, 1);
        profile.ActivityLevel = (short)input.ActivityLevel!.Value;
        profile.Goal = input.Goal!;
        profile.EnergyTargetKcal = input.EnergyTargetKcal;
        await db.SaveChangesAsync();
        return await ToDtoAsync(profile);
    }

    /// <summary>
    /// プロフィールを返します。目標が必要な処理（ダッシュボードなど）が使います。
    /// </summary>
    /// <returns>プロフィール。未登録なら 404。</returns>
    public async Task<UserProfile> RequireAsync() =>
        await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == User.FixedUserId)
        ?? throw new NotFoundException("プロフィールが未登録です");

    /// <summary>
    /// エネルギー目標（自動算出と実際に使う値）とタンパク質目標を計算します。
    /// </summary>
    /// <param name="p">プロフィール。</param>
    /// <param name="on">年齢を求める日（省略すると今日）。</param>
    /// <returns>自動算出のエネルギー（kcal）、実際に使うエネルギー（kcal。手入力があればそれ）、タンパク質（g）。</returns>
    public (int AutoKcal, int EffectiveKcal, decimal ProteinG) ComputeTargets(UserProfile p, DateOnly? on = null)
    {
        var settings = options.Value.Targets.ToSettings();
        var sex = p.Sex == "male" ? Sex.Male : Sex.Female;
        var age = TargetCalculator.AgeOn(p.BirthDate, on ?? clock.Today);
        var auto = TargetCalculator.EnergyKcal(sex, p.WeightKg, p.HeightCm, age, p.ActivityLevel, settings);
        var goal = Enum.Parse<Goal>(p.Goal, ignoreCase: true);
        return (auto, TargetCalculator.EffectiveEnergyKcal(auto, p.EnergyTargetKcal), TargetCalculator.ProteinG(p.WeightKg, goal, settings));
    }

    /// <summary>
    /// プロフィールを返却用の形に変換します。目標と、性別・年齢に合う栄養目標も付けます。
    /// </summary>
    /// <param name="p">プロフィール。</param>
    /// <returns>プロフィールと目標。</returns>
    private async Task<ProfileDto> ToDtoAsync(UserProfile p)
    {
        var (auto, effective, proteinG) = ComputeTargets(p);
        var age = TargetCalculator.AgeOn(p.BirthDate, clock.Today);
        var targets = await db.NutrientTargets.AsNoTracking()
            .Include(t => t.Nutrient)
            .Where(t => (t.Sex == p.Sex || t.Sex == "all") && t.AgeMin <= age && age <= t.AgeMax)
            .OrderBy(t => t.Nutrient!.DisplayOrder).ThenBy(t => t.Kind)
            .Select(t => new NutrientTargetDto(t.Nutrient!.Code, t.Kind, t.Amount, t.Basis))
            .ToListAsync();
        return new ProfileDto(
            p.Sex, p.BirthDate, p.HeightCm, p.WeightKg, p.ActivityLevel, p.Goal, p.EnergyTargetKcal,
            new ProfileTargetsDto(new EnergyTargetDto(auto, p.EnergyTargetKcal, effective), proteinG, targets));
    }

    /// <summary>
    /// プロフィールの入力を検証します。性別・生年月日・身長・体重・活動レベル・目的・エネルギー目標の範囲を確かめ、問題があれば項目別の 400 エラーにします。
    /// </summary>
    /// <param name="i">入力内容。</param>
    private void Validate(ProfileInput i)
    {
        var errors = new List<ErrorDetail>();
        if (i.Sex is null || !Sexes.Contains(i.Sex))
        {
            errors.Add(new ErrorDetail("sex", "性別を選択してください"));
        }
        if (i.BirthDate is null || i.BirthDate.Value >= clock.Today)
        {
            errors.Add(new ErrorDetail("birthDate", "過去の日付を指定してください"));
        }
        if (i.HeightCm is not (>= 50m and <= 250m))
        {
            errors.Add(new ErrorDetail("heightCm", "50.0〜250.0 の範囲で入力してください"));
        }
        if (i.WeightKg is not (>= 20m and <= 300m))
        {
            errors.Add(new ErrorDetail("weightKg", "20.0〜300.0 の範囲で入力してください"));
        }
        if (i.ActivityLevel is not (>= 1 and <= 4))
        {
            errors.Add(new ErrorDetail("activityLevel", "1〜4 を選択してください"));
        }
        if (i.Goal is null || !Goals.Contains(i.Goal))
        {
            errors.Add(new ErrorDetail("goal", "目的を選択してください"));
        }
        if (i.EnergyTargetKcal is not null and not (>= 500 and <= 10000))
        {
            errors.Add(new ErrorDetail("energyTargetKcal", "500〜10000 の整数で入力してください"));
        }
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
