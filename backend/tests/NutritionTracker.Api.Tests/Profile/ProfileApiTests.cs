using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Profile;

/// <summary>API-05 プロフィール更新（docs/09.API仕様.md §4）。「今日」は 2026-10-17 に固定。</summary>
[Collection(ApiCollection.Name)]
public class ProfileApiTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>男性・30 歳（1996-04-01 生まれ）・170cm・65kg・活動レベル 2・目的 skin</summary>
    private static ProfileInput Valid(int? energyOverride = null, decimal weight = 65.0m, string goal = "skin") =>
        new("male", new DateOnly(1996, 4, 1), 170.0m, weight, 2, goal, energyOverride);

    private async Task<HttpResponseMessage> PutAsync(ProfileInput input) => await fixture.Client.PutAsJsonAsync("/api/profile", input);

    [Fact]
    public async Task AC_PROF_001_Put_Registers_AndReturnsComputedTargets()
    {
        var res = await PutAsync(Valid());

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var p = (await res.Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal(("male", 170.0m, 65.0m, 2, "skin", null), (p.Sex, p.HeightCm, p.WeightKg, p.ActivityLevel, p.Goal, p.EnergyTargetKcal));
        // AC-SCORE-007: 650 + 1062.5 − 150 + 5 = 1567.5、× 1.375 = 2155.3125 → 2155
        Assert.Equal(new EnergyTargetDto(2155, null, 2155), p.Targets.EnergyKcal);
        Assert.Equal(65.0m, p.Targets.ProteinG); // 65kg × 1.0（skin）
        Assert.Empty(p.Targets.Nutrients); // T栄養目標は Q-2 で転記するまで空
    }

    [Fact]
    public async Task AC_PROF_002_Put_InvalidValues_Return400WithFieldDetails()
    {
        var bad = new ProfileInput("other", new DateOnly(2030, 1, 1), 0m, 301m, 5, "x", 100);

        var res = await PutAsync(bad);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.Equal("VALIDATION_ERROR", err.Code);
        Assert.Equal(
            ["sex", "birthDate", "heightCm", "weightKg", "activityLevel", "goal", "energyTargetKcal"],
            err.Details.Select(d => d.Field).ToList());
        Assert.Equal(0, await fixture.WithDbAsync(db => db.UserProfiles.CountAsync())); // 保存されない
    }

    [Fact]
    public async Task Put_MissingFields_Return400ForEachRequiredField()
    {
        var res = await PutAsync(new ProfileInput(null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["sex", "birthDate", "heightCm", "weightKg", "activityLevel", "goal"], fields); // 上書き（任意）は含まれない
    }

    [Fact]
    public async Task Put_BirthDateToday_IsRejected_PastDayIsAccepted()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(Valid() with { BirthDate = ApiFixture.Today })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(Valid() with { BirthDate = ApiFixture.Today.AddDays(-1) })).StatusCode);
    }

    [Fact]
    public async Task AC_PROF_003_EnergyOverride_TakesPrecedence_AndNullReturnsToAuto()
    {
        var overridden = (await (await PutAsync(Valid(energyOverride: 1800))).Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal(new EnergyTargetDto(2155, 1800, 1800), overridden.Targets.EnergyKcal); // auto は維持される

        var back = (await (await PutAsync(Valid(energyOverride: null))).Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal(new EnergyTargetDto(2155, null, 2155), back.Targets.EnergyKcal);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(10001)]
    public async Task Put_EnergyOverride_OutOfRange_Returns400(int kcal)
    {
        var res = await PutAsync(Valid(energyOverride: kcal));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("energyTargetKcal", (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single().Field);
    }

    [Fact]
    public async Task AC_PROF_004_Update_RecomputesTargets_AndKeepsSingleRow()
    {
        await PutAsync(Valid(weight: 60m, goal: "skin"));
        var updated = (await (await PutAsync(Valid(weight: 60m, goal: "muscle"))).Content.ReadFromJsonAsync<ProfileDto>())!;

        Assert.Equal(96.0m, updated.Targets.ProteinG); // 60kg × 1.6（muscle）
        Assert.Equal(1, await fixture.WithDbAsync(db => db.UserProfiles.CountAsync())); // 更新であり、行は増えない
    }

    [Fact]
    public async Task Put_HeightAndWeight_AreRoundedToOneDecimal()
    {
        var p = (await (await PutAsync(Valid() with { HeightCm = 170.04m, WeightKg = 64.95m })).Content.ReadFromJsonAsync<ProfileDto>())!;

        Assert.Equal(170.0m, p.HeightCm);
        Assert.Equal(65.0m, p.WeightKg); // 64.95 → HALF_UP で 65.0
    }
}
