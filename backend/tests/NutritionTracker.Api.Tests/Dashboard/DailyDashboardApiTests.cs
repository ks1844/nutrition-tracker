using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>
/// 日次ダッシュボード API（API-17、docs/09.API仕様.md §7）。<b>目標値はテスト用の仮の値で、食事摂取基準の公式値ではない</b>。
/// 摂取量は保存済みスナップショットを直接作って与える（計算そのものは MealRecordsApiTests / Domain のテストで検証済み）。
/// </summary>
[Collection(ApiCollection.Name)]
public class DailyDashboardApiTests(ApiFixture fixture) : DashboardApiTestBase(fixture)
{

    private async Task<DailyDashboardDto> GetAsync(DateOnly? date = null) =>
        (await Fixture.Client.GetFromJsonAsync<DailyDashboardDto>($"/api/dashboard/daily?date={(date ?? Day):yyyy-MM-dd}"))!;

    private static NutrientStatusDto Row(DailyDashboardDto d, string code) => d.Nutrients.Single(n => n.Code == code);

    // ---- 摂取量・エネルギー・PFC ----
    [Fact]
    public async Task AC_DASH_001_SumsSnapshots_AndReturnsEnergyAndPfc()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day, "breakfast", new() { ["ENERGY"] = 400m, ["PROT"] = 20.04m, ["FAT"] = 10m, ["CHO"] = 60m });
        await AddIntakeAsync(Day, "dinner", new() { ["ENERGY"] = 1020m, ["PROT"] = 41.96m, ["FAT"] = 30m, ["CHO"] = 120m });
        await AddIntakeAsync(Day.AddDays(-1), "dinner", new() { ["ENERGY"] = 999m, ["PROT"] = 99m }); // 他の日は含めない

        var d = await GetAsync();

        Assert.True(d.HasRecords);
        Assert.Equal(new EnergySummaryDto(1420, 2155, 0.658933m), d.Energy); // AC-CALC-012 の目標 2155（AC-SCORE-007）
        Assert.Equal((62.0m, 40.0m, 180.0m), (d.Pfc.ProteinG, d.Pfc.FatG, d.Pfc.CarbG));
        Assert.Equal(new PfcRatioPctDto(19, 27, 54), d.Pfc.RatioPct); // AC-CALC-012: 18.7 / 27.1 / 54.2 → 最大剰余法
        Assert.Equal(62.0m, Row(d, "PROT").Intake); // 栄養素ごとは小数 1 桁
        Assert.Equal(1420m, Row(d, "ENERGY").Intake); // kcal は整数
    }

    [Fact]
    public async Task NoRecords_ReturnsZeroIntake_AndNullScores()
    {
        await PutProfileAsync();

        var d = await GetAsync();

        Assert.False(d.HasRecords);
        Assert.Equal(new EnergySummaryDto(0, 2155, 0m), d.Energy);
        Assert.Equal(24, d.Nutrients.Count);
        Assert.All(d.Nutrients, n => Assert.Equal(0m, n.Intake));
        Assert.All(new[] { d.Scores.Beauty, d.Scores.Muscle, d.Scores.Base }, s => Assert.Equal(new ScoreDto(null, null, false, false), s));
        Assert.Empty(d.Notices);
    }

    // ---- スコア ----
    [Fact]
    public async Task AC_SCORE_001_Beauty_IsWeightedAchievement_CappedAtOne()
    {
        await PutProfileAsync();
        await AddTargetsAsync(("VITC", "RDA", 100m), ("VITE_ATOC", "RDA", 6m), ("VITA_RAE", "RDA", 700m), ("ZN", "RDA", 10m));
        await AddIntakeAsync(Day, "dinner", new() { ["VITC"] = 78m, ["VITE_ATOC"] = 9m, ["VITA_RAE"] = 450m, ["ZN"] = 8m });

        var d = await GetAsync();

        Assert.Equal(new ScoreDto(81, "GOOD", false, false), d.Scores.Beauty); // (0.78+1+0.642857+0.4)÷3.5 = 0.806531
        Assert.Equal((0.78m, "DEFICIENT"), (Row(d, "VITC").AchievementRate, Row(d, "VITC").Status));
        Assert.Equal((1.0m, "ACHIEVED"), (Row(d, "VITE_ATOC").AchievementRate, Row(d, "VITE_ATOC").Status)); // 頭打ち
        Assert.Equal(new NutrientTargetViewDto("RDA", null, null, null, "PER_DAY", 100m), Row(d, "VITC").Target);
    }

    [Fact]
    public async Task AC_SCORE_002_Muscle_UsesProteinDistributionAndEnergyFit()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day, "breakfast", new() { ["PROT"] = 25m });
        await AddIntakeAsync(Day, "lunch", new() { ["PROT"] = 22m });
        await AddIntakeAsync(Day, "dinner", new() { ["PROT"] = 15m, ["ENERGY"] = 1800m });
        await AddIntakeAsync(Day, "snack", new() { ["PROT"] = 30m }); // 間食は分配率の分母に含めない

        var d = await GetAsync();

        // タンパク質 (25+22+15+30)=92 → 目標 91.0 で頭打ち 1.0、分配率 2/3、エネルギー 1800÷2155 → 充足 0.838167
        // 0.6×1.0 + 0.2×0.666667 + 0.2×0.838167 = 0.900967 → 90
        Assert.Equal(new ScoreDto(90, "GOOD", false, false), d.Scores.Muscle);
    }

    [Fact]
    public async Task Muscle_WithoutMainMeals_DropsDistribution()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day, "snack", new() { ["PROT"] = 45.5m, ["ENERGY"] = 2155m });

        var d = await GetAsync();

        // AC-SCORE-010: 間食だけ → 分配率を除外し 0.6 : 0.2 に再正規化。タンパク質 0.5、エネルギー 1.0 → (0.6×0.5+0.2×1)÷0.8 = 0.625 → 63
        Assert.Equal(63, d.Scores.Muscle.Value);
    }

    [Fact]
    public async Task AC_SCORE_003_Base_AveragesAchievement_AndPenalizesSalt()
    {
        await PutProfileAsync();
        string[] baseCodes = ["FIBTG", "CA", "FE", "MG", "K", "VITB1", "VITB2", "VITB6", "VITB12", "FOL", "VITD"];
        await AddTargetsAsync([.. baseCodes.Select(c => (c, "RDA", 100m)), ("NACL_EQ", "DG_MAX", 7.0m)]);
        await AddIntakeAsync(Day, "dinner", baseCodes.ToDictionary(c => c, _ => 80m).Concat([KeyValuePair.Create("NACL_EQ", 8.4m)]).ToDictionary());

        var d = await GetAsync();

        Assert.Equal(new ScoreDto(76, "FAIR", false, false), d.Scores.Base); // 平均 80 − 減点 min(10, 20×(8.4/7.0−1)) = 4.0
        Assert.Equal(("OVER_UL", null), (Row(d, "NACL_EQ").Status, Row(d, "NACL_EQ").AchievementRate)); // 上限型は達成率なし、超過で警告
        Assert.Equal(7.0m, Row(d, "NACL_EQ").Target.Max);
    }

    [Fact]
    public async Task Base_SaltWithinLimit_HasNoPenalty()
    {
        await PutProfileAsync();
        string[] baseCodes = ["FIBTG", "CA", "FE", "MG", "K", "VITB1", "VITB2", "VITB6", "VITB12", "FOL", "VITD"];
        await AddTargetsAsync([.. baseCodes.Select(c => (c, "RDA", 100m)), ("NACL_EQ", "DG_MAX", 7.0m)]);
        await AddIntakeAsync(Day, "dinner", baseCodes.ToDictionary(c => c, _ => 80m).Concat([KeyValuePair.Create("NACL_EQ", 5m)]).ToDictionary());

        var d = await GetAsync();

        Assert.Equal(80, d.Scores.Base.Value);
        Assert.Equal("ACHIEVED", Row(d, "NACL_EQ").Status);
    }

    [Fact]
    public async Task Scores_WithoutTargets_AreNull_ButMuscleStillComputed()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day, "dinner", new() { ["PROT"] = 91m, ["ENERGY"] = 2155m });

        var d = await GetAsync();

        Assert.Null(d.Scores.Beauty.Value); // T栄養目標が空（Q-2）→ 目標のない栄養素は除外され、対象が無ければ出さない
        Assert.Null(d.Scores.Base.Value);
        Assert.Equal(new ScoreDto(100, "GOOD", false, false), d.Scores.Muscle); // 目標はプロフィールから算出される
        Assert.Equal("NO_TARGET", Row(d, "VITC").Status);
        Assert.Null(Row(d, "VITC").AchievementRate);
    }

    // ---- 状態 ----
    [Fact]
    public async Task AC_SCORE_006_OverUl_IsFlagged_WhileAchievementStaysCapped()
    {
        await PutProfileAsync();
        await AddTargetsAsync(("VITC", "RDA", 100m), ("VITE_ATOC", "RDA", 6m), ("VITA_RAE", "RDA", 700m), ("ZN", "RDA", 10m), ("ZN", "UL", 35m));
        await AddIntakeAsync(Day, "dinner", new() { ["VITC"] = 100m, ["VITE_ATOC"] = 6m, ["VITA_RAE"] = 700m, ["ZN"] = 40m });

        var d = await GetAsync();

        var zn = Row(d, "ZN");
        Assert.Equal(("OVER_UL", 1.0m, 35m), (zn.Status, zn.AchievementRate, zn.Target.Ul));
        Assert.Equal(100, d.Scores.Beauty.Value); // スコアの達成率は 1.0 で頭打ちのまま
    }

    [Fact]
    public async Task Status_UsesAiAndDgMinWhenNoRda_AndPrefersRda()
    {
        await PutProfileAsync();
        await AddTargetsAsync(("K", "AI", 2000m), ("FIBTG", "DG_MIN", 20m), ("CA", "RDA", 800m), ("CA", "AI", 500m));
        await AddIntakeAsync(Day, "dinner", new() { ["K"] = 1000m, ["FIBTG"] = 20m, ["CA"] = 400m });

        var d = await GetAsync();

        Assert.Equal(("AI", 0.5m, "DEFICIENT"), (Row(d, "K").Target.Kind, Row(d, "K").AchievementRate, Row(d, "K").Status));
        Assert.Equal(("DG_MIN", 1.0m, "ACHIEVED"), (Row(d, "FIBTG").Target.Kind, Row(d, "FIBTG").AchievementRate, Row(d, "FIBTG").Status));
        Assert.Equal(("RDA", 800m, 0.5m), (Row(d, "CA").Target.Kind, Row(d, "CA").Target.Amount, Row(d, "CA").AchievementRate)); // RDA 優先
    }

    [Fact]
    public async Task Targets_UseSexAndAge_AndPerKgBasis()
    {
        await PutProfileAsync();
        await Fixture.WithDbAsync(async db =>
        {
            var ids = await db.Nutrients.ToDictionaryAsync(n => n.Code, n => n.Id);
            db.NutrientTargets.AddRange(
                new NutrientTarget { NutrientId = ids["CA"], Sex = "female", AgeMin = 0, AgeMax = 150, Kind = "RDA", Amount = 650m, Basis = "PER_DAY" }, // 性別が違う
                new NutrientTarget { NutrientId = ids["CA"], Sex = "male", AgeMin = 18, AgeMax = 29, Kind = "RDA", Amount = 800m, Basis = "PER_DAY" }, // 年齢が違う（30 歳）
                new NutrientTarget { NutrientId = ids["CA"], Sex = "male", AgeMin = 30, AgeMax = 49, Kind = "RDA", Amount = 750m, Basis = "PER_DAY" },
                new NutrientTarget { NutrientId = ids["MG"], Sex = "all", AgeMin = 0, AgeMax = 150, Kind = "RDA", Amount = 5m, Basis = "PER_KG_BW" }, // 65kg → 325
                new NutrientTarget { NutrientId = ids["FAT"], Sex = "all", AgeMin = 0, AgeMax = 150, Kind = "DG_MIN", Amount = 20m, Basis = "PCT_ENERGY" }); // 未対応
            await db.SaveChangesAsync();
        });

        var d = await GetAsync();

        Assert.Equal(750m, Row(d, "CA").Target.Amount);
        Assert.Equal(325m, Row(d, "MG").Target.Amount);
        Assert.Equal("NO_TARGET", Row(d, "FAT").Status);
    }

    [Fact]
    public async Task EnergyAndProtein_UseProfileTargets_WithOverride()
    {
        await Fixture.Client.PutAsJsonAsync("/api/profile", new ProfileInput("male", new DateOnly(1996, 4, 1), 170.0m, 65.0m, 2, "muscle", 2500));
        await AddIntakeAsync(Day, "dinner", new() { ["ENERGY"] = 1250m, ["PROT"] = 52m });

        var d = await GetAsync();

        Assert.Equal(new EnergySummaryDto(1250, 2500, 0.5m), d.Energy); // 上書きを優先
        Assert.Equal(("CALCULATED", 104.0m, 0.5m), (Row(d, "PROT").Target.Kind, Row(d, "PROT").Target.Amount, Row(d, "PROT").AchievementRate)); // 65 × 1.6
        Assert.Equal(2500m, Row(d, "ENERGY").Target.Amount);
    }

    // ---- 注記・残量 ----
    [Fact]
    public async Task AC_DASH_007_IncompleteNutrients_AreFlaggedAndNoticed_ButReferenceNeverIs()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day, "dinner", new() { ["VITC"] = 31m }, "VITC", "ASTX");

        var d = await GetAsync();

        Assert.True(Row(d, "VITC").IsIncomplete);
        Assert.False(Row(d, "ASTX").IsIncomplete); // 参考値は不完全にしない
        Assert.True(Row(d, "ASTX").IsReference);
        var notice = Assert.Single(d.Notices);
        Assert.Equal("INCOMPLETE_NUTRIENT", notice.Code);
        Assert.Equal(["VITC"], notice.NutrientCodes);
    }

    [Fact]
    public async Task RemainingMenus_ListsOnlyMenusWithLeftovers()
    {
        await PutProfileAsync();
        TestData.Golden? golden = null;
        await Fixture.WithDbAsync(async db => golden = await TestData.AddGoldenAsync(db));
        var g = golden!;
        async Task<MenuDetailDto> MenuAsync(string name) => (await (await Fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput(
            name, null, null, 2400m, 8m, "お椀", [new MenuItemInput(g.Pork.Id, null, 400m)]))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        var partly = await MenuAsync("豚汁");
        var eaten = await MenuAsync("食べきった汁");
        await MenuAsync("手つかずの汁");
        await Fixture.Client.PostAsJsonAsync("/api/meal-records", new MealRecordInput(Day, "dinner", null,
            [new MealRecordItemInput(partly.Id, null, null, "SERVING", 1.5m), new MealRecordItemInput(eaten.Id, null, null, "SERVING", 8m)]));

        var d = await GetAsync();

        Assert.Equal(["手つかずの汁", "豚汁"], d.RemainingMenus.Select(m => m.Name).ToList()); // 新しい順、食べきったものは出ない
        var soup = d.RemainingMenus.Single(m => m.MenuId == partly.Id);
        Assert.Equal((0.8125m, 6.5m, "お椀"), (soup.RemainingRatio, soup.RemainingServings, soup.ServingLabel));
        Assert.True(d.HasRecords);
    }

    // ---- エラー ----
    [Fact]
    public async Task NoProfile_Returns404()
    {
        var res = await Fixture.Client.GetAsync($"/api/dashboard/daily?date={Day:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task MissingOrInvalidDate_Returns400()
    {
        await PutProfileAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Fixture.Client.GetAsync("/api/dashboard/daily")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Fixture.Client.GetAsync("/api/dashboard/daily?date=abc")).StatusCode);
    }
}
