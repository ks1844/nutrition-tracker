using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Profile;

/// <summary>
/// プロフィールの取得（GET /api/profile、API-04）と登録・更新（PUT /api/profile、API-05）のテストです（docs/09.API仕様.md §4）。「今日」は 2026-10-17 に固定しています。
/// </summary>
[Collection(ApiCollection.Name)]
public class ProfileApiTests(ApiFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// 各テストの前に、データを初期状態に戻します。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public Task InitializeAsync() => fixture.ResetAsync();

    /// <summary>
    /// 各テストの後の片づけはありません。
    /// </summary>
    /// <returns>完了済みのタスク。</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 正しいプロフィールの入力を作ります（男性・30 歳・170cm・活動レベル 2。体重と目的は指定可）。
    /// </summary>
    /// <param name="energyOverride">エネルギー目標の手入力。</param>
    /// <param name="weight">体重（kg）。</param>
    /// <param name="goal">目的。</param>
    /// <returns>プロフィールの入力。</returns>
    private static ProfileInput Valid(int? energyOverride = null, decimal weight = 65.0m, string goal = "skin") =>
        new("male", new DateOnly(1996, 4, 1), 170.0m, weight, 2, goal, energyOverride);

    /// <summary>
    /// プロフィールの登録・更新 API を呼びます。
    /// </summary>
    /// <param name="input">プロフィールの入力。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> PutAsync(ProfileInput input) => await fixture.Client.PutAsJsonAsync("/api/profile", input);

    /// <summary>
    /// プロフィールを登録すると、算出した目標が返ることを確認します。男性・30 歳・170cm・65kg・活動レベル 2 のエネルギーは 2155 kcal、目的 skin のタンパク質は 65.0g で、栄養目標は転記前なので空です（AC-PROF-001、AC-SCORE-007）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 範囲外の値（性別・生年月日・身長・体重・活動レベル・目的・エネルギー目標）がすべて項目別の 400 になり、何も保存されないことを確認します（AC-PROF-002）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 必須項目が抜けているとき、必須の 6 項目が 400 になることを確認します（エネルギー目標の手入力は任意なので含まれません）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Put_MissingFields_Return400ForEachRequiredField()
    {
        var res = await PutAsync(new ProfileInput(null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["sex", "birthDate", "heightCm", "weightKg", "activityLevel", "goal"], fields); // 上書き（任意）は含まれない
    }

    /// <summary>
    /// 生年月日が今日だと 400、前日なら受け付けられることを確認します（過去の日付だけ有効）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Put_BirthDateToday_IsRejected_PastDayIsAccepted()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(Valid() with { BirthDate = ApiFixture.Today })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(Valid() with { BirthDate = ApiFixture.Today.AddDays(-1) })).StatusCode);
    }

    /// <summary>
    /// エネルギー目標を手入力すると、それが実際に使う値になり（自動算出の値は残る）、null に戻すと自動算出に戻ることを確認します（AC-PROF-003）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task AC_PROF_003_EnergyOverride_TakesPrecedence_AndNullReturnsToAuto()
    {
        var overridden = (await (await PutAsync(Valid(energyOverride: 1800))).Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal(new EnergyTargetDto(2155, 1800, 1800), overridden.Targets.EnergyKcal); // auto は維持される

        var back = (await (await PutAsync(Valid(energyOverride: null))).Content.ReadFromJsonAsync<ProfileDto>())!;
        Assert.Equal(new EnergyTargetDto(2155, null, 2155), back.Targets.EnergyKcal);
    }

    /// <summary>
    /// エネルギー目標の手入力が 500 未満または 10000 超だと 400 になることを確認します。
    /// </summary>
    /// <param name="kcal">手入力したエネルギー目標。</param>
    /// <returns>テストの完了を表すタスク。</returns>
    [Theory]
    [InlineData(499)]
    [InlineData(10001)]
    public async Task Put_EnergyOverride_OutOfRange_Returns400(int kcal)
    {
        var res = await PutAsync(Valid(energyOverride: kcal));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("energyTargetKcal", (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single().Field);
    }

    /// <summary>
    /// プロフィールを更新すると目標が計算し直され（目的を skin から muscle に変えるとタンパク質が 96.0g になる）、行は増えず 1 行のままであることを確認します（AC-PROF-004）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task AC_PROF_004_Update_RecomputesTargets_AndKeepsSingleRow()
    {
        await PutAsync(Valid(weight: 60m, goal: "skin"));
        var updated = (await (await PutAsync(Valid(weight: 60m, goal: "muscle"))).Content.ReadFromJsonAsync<ProfileDto>())!;

        Assert.Equal(96.0m, updated.Targets.ProteinG); // 60kg × 1.6（muscle）
        Assert.Equal(1, await fixture.WithDbAsync(db => db.UserProfiles.CountAsync())); // 更新であり、行は増えない
    }

    /// <summary>
    /// 身長・体重が小数 1 桁に四捨五入されることを確認します（170.04 → 170.0、64.95 → 65.0）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Put_HeightAndWeight_AreRoundedToOneDecimal()
    {
        var p = (await (await PutAsync(Valid() with { HeightCm = 170.04m, WeightKg = 64.95m })).Content.ReadFromJsonAsync<ProfileDto>())!;

        Assert.Equal(170.0m, p.HeightCm);
        Assert.Equal(65.0m, p.WeightKg); // 64.95 → HALF_UP で 65.0
    }

    // ---- GET ----
    /// <summary>
    /// プロフィール未登録のとき、取得が 404 の NOT_FOUND になることを確認します（画面はプロフィール設定へ案内します）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Get_NotRegistered_Returns404()
    {
        var res = await fixture.Client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("NOT_FOUND", (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Code);
    }

    /// <summary>
    /// 登録したプロフィールを取得すると、同じ内容（生年月日、手入力のエネルギー目標）が返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Get_AfterPut_ReturnsSameContent()
    {
        await PutAsync(Valid(energyOverride: 1900));

        var p = (await fixture.Client.GetFromJsonAsync<ProfileDto>("/api/profile"))!;

        Assert.Equal(new DateOnly(1996, 4, 1), p.BirthDate);
        Assert.Equal(new EnergyTargetDto(2155, 1900, 1900), p.Targets.EnergyKcal);
    }

    /// <summary>
    /// 取得したプロフィールの栄養目標に、性別と年齢が合う行だけが栄養素の表示順で入ることを確認します。男性 30 歳なら、女性の行と 50 歳以上の行は含まれません（テスト用の仮の値）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Get_IncludesNutrientTargets_MatchingSexAndAge()
    {
        await PutAsync(Valid()); // 男性・30 歳
        await fixture.WithDbAsync(async db =>
        {
            var vitC = await db.Nutrients.SingleAsync(n => n.Code == "VITC");
            var zn = await db.Nutrients.SingleAsync(n => n.Code == "ZN");
            // テスト用の仮の値（食事摂取基準の公式値ではない）
            db.NutrientTargets.AddRange(
                new NutrientTarget { NutrientId = vitC.Id, Sex = "male", AgeMin = 18, AgeMax = 49, Kind = "RDA", Amount = 100m, Basis = "PER_DAY" },
                new NutrientTarget { NutrientId = vitC.Id, Sex = "female", AgeMin = 18, AgeMax = 49, Kind = "RDA", Amount = 90m, Basis = "PER_DAY" },
                new NutrientTarget { NutrientId = zn.Id, Sex = "all", AgeMin = 0, AgeMax = 999, Kind = "UL", Amount = 40m, Basis = "PER_DAY" },
                new NutrientTarget { NutrientId = zn.Id, Sex = "male", AgeMin = 50, AgeMax = 999, Kind = "RDA", Amount = 11m, Basis = "PER_DAY" });
            await db.SaveChangesAsync();
        });

        var p = (await fixture.Client.GetFromJsonAsync<ProfileDto>("/api/profile"))!;

        // 男性 30 歳に該当する 2 件（性別が違う行・年齢が合わない行は含まれない）。栄養素の表示順（ZN → VITC）
        Assert.Equal(
            new[] { new NutrientTargetDto("ZN", "UL", 40m, "PER_DAY"), new NutrientTargetDto("VITC", "RDA", 100m, "PER_DAY") },
            p.Targets.Nutrients.ToArray());
    }
}
