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

public class ProfileService(AppDbContext db, AppClock clock, IOptions<NutritionOptions> options)
{
    private static readonly string[] Sexes = ["male", "female"];
    private static readonly string[] Goals = ["skin", "muscle", "both"];

    /// <summary>API-04 プロフィール取得。未登録は 404（画面はプロフィール設定へ誘導する）。</summary>
    public async Task<ProfileDto> GetAsync()
    {
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == User.FixedUserId)
            ?? throw new NotFoundException("プロフィールが未登録です");
        return await ToDtoAsync(profile);
    }

    /// <summary>API-05 プロフィール登録・更新（upsert）。</summary>
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

    /// <summary>プロフィール未登録なら 404（ダッシュボードなど、目標が必要な API が使う）。</summary>
    public async Task<UserProfile> RequireAsync() =>
        await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == User.FixedUserId)
        ?? throw new NotFoundException("プロフィールが未登録です");

    /// <summary>エネルギー・タンパク質の目標（上書きがあれば優先）。</summary>
    public (int AutoKcal, int EffectiveKcal, decimal ProteinG) ComputeTargets(UserProfile p)
    {
        var settings = options.Value.Targets.ToSettings();
        var sex = p.Sex == "male" ? Sex.Male : Sex.Female;
        var age = TargetCalculator.AgeOn(p.BirthDate, clock.Today);
        var auto = TargetCalculator.EnergyKcal(sex, p.WeightKg, p.HeightCm, age, p.ActivityLevel, settings);
        var goal = Enum.Parse<Goal>(p.Goal, ignoreCase: true);
        return (auto, TargetCalculator.EffectiveEnergyKcal(auto, p.EnergyTargetKcal), TargetCalculator.ProteinG(p.WeightKg, goal, settings));
    }

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
