using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>
/// 日次ダッシュボード API（GET /api/dashboard/daily、API-17、docs/09.API仕様.md §7）のテストです。目標値はテスト用の仮の値で、食事摂取基準の公式値ではありません。
/// </summary>
[Collection(ApiCollection.Name)]
public class DailyDashboardApiTests(ApiFixture fixture) : DashboardApiTestBase(fixture)
{

    /// <summary>
    /// 日次ダッシュボード API を呼びます。
    /// </summary>
    /// <param name="date">集計する日（省略するとテスト中の今日）。</param>
    /// <returns>その日のダッシュボード。</returns>
    private async Task<DailyDashboardDto> GetAsync(DateOnly? date = null) =>
        (await Fixture.Client.GetFromJsonAsync<DailyDashboardDto>($"/api/dashboard/daily?date={(date ?? Day):yyyy-MM-dd}"))!;

    /// <summary>
    /// 栄養素ごとの状態から、指定した栄養素を取り出します。
    /// </summary>
    /// <param name="d">日次ダッシュボード。</param>
    /// <param name="code">栄養素コード。</param>
    /// <returns>その栄養素の状態。</returns>
    private static NutrientStatusDto Row(DailyDashboardDto d, string code) => d.Nutrients.Single(n => n.Code == code);

    // ---- 摂取量・エネルギー・PFC ----
    /// <summary>
    /// その日の記録の栄養量を合計し、エネルギー・PFC・PFC 比を返すことを確認します（他の日の記録は含めない）。エネルギー 1420 kcal（目標 2155、達成率 0.658933）、P 62.0g・F 40.0g・C 180.0g、PFC 比は 19 / 27 / 54 です（AC-DASH-001、AC-CALC-012）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 記録のない日は、摂取量がすべて 0、栄養素は 24 件、スコアはすべて値なし、通知なしで返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// 美肌スコアが達成率の重みつき平均になる（ビタミン C 0.78・ビタミン E 1.0・ビタミン A 0.642857・亜鉛 0.8 で 81、良好）ことと、栄養素ごとの達成率・状態・目標が返ることを確認します（AC-SCORE-001、AC-DASH-002）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 筋肉スコアを確認します。タンパク質は間食を含めた合計 92g（目標 91.0g で頭打ち）、分配率は朝昼夕のうち 2/3（間食は分母に含めない）、エネルギー充足は 0.838167 で、合わせて 90 になります（AC-SCORE-002）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 朝昼夕の記録がなく間食だけのとき、分配率を外して重みを 0.6 : 0.2 に直して計算する（63）ことを確認します（AC-SCORE-010）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Muscle_WithoutMainMeals_DropsDistribution()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day, "snack", new() { ["PROT"] = 45.5m, ["ENERGY"] = 2155m });

        var d = await GetAsync();

        // AC-SCORE-010: 間食だけ → 分配率を除外し 0.6 : 0.2 に再正規化。タンパク質 0.5、エネルギー 1.0 → (0.6×0.5+0.2×1)÷0.8 = 0.625 → 63
        Assert.Equal(63, d.Scores.Muscle.Value);
    }

    /// <summary>
    /// 基礎スコアが、11 栄養素の達成率の平均（80）から食塩の減点（8.4g、上限 7.0g で 4 点）を引いて 76 になり、食塩が上限超過（OVER_UL）と判定されることを確認します（AC-SCORE-003）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 食塩が上限以内なら減点がなく（80）、状態は ACHIEVED になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 栄養目標が空（転記前）のとき、目標のない美肌・基礎スコアは出ず（null）、目標がプロフィールから算出される筋肉スコアは出ることを確認します。栄養素の状態は NO_TARGET です。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// 亜鉛が耐容上限量（UL）を超えると状態が OVER_UL になる一方、達成率は 1.0 で頭打ちのままで、美肌スコアも 100 になることを確認します（AC-SCORE-006）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 下限型の目標の選び方を確認します。RDA がなければ AI、なければ DG_MIN を使い、RDA と AI の両方があれば RDA を優先します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 栄養目標が、性別と年齢が合う行だけから引かれ（男性 30 歳なら 30〜49 歳の行）、体重あたりの目標は体重を掛けて 1 日あたりに換算され（5 × 65kg = 325）、エネルギー比の目標は未対応で目標なしになることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// エネルギーとタンパク質の目標が栄養目標のマスタではなくプロフィールから決まることを確認します。エネルギーは手入力の 2500 kcal を優先し、タンパク質は 65kg × 1.6 の 104.0g（種類は CALCULATED）です。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// 未測定を含む栄養素（ビタミン C）に印がつき、通知（INCOMPLETE_NUTRIENT）に載ることと、参考値の栄養素（アスタキサンチン）には印がつかないことを確認します（AC-DASH-007）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 残りのあるメニューだけが新しい順に返ることを確認します（食べきったメニューは出ない。豚汁は 1.5 杯食べて残り 0.8125、6.5 杯）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// プロフィール未登録のとき、404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task NoProfile_Returns404()
    {
        var res = await Fixture.Client.GetAsync($"/api/dashboard/daily?date={Day:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>
    /// 日付がない、または日付として読めないとき、400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task MissingOrInvalidDate_Returns400()
    {
        await PutProfileAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Fixture.Client.GetAsync("/api/dashboard/daily")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Fixture.Client.GetAsync("/api/dashboard/daily?date=abc")).StatusCode);
    }
}
