using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>
/// 直近 7 日ダッシュボード API（API-18、docs/09.API仕様.md §7）。窓は 2026-10-11〜2026-10-17。
/// <b>目標値はテスト用の仮の値で、食事摂取基準の公式値ではない</b>。
/// </summary>
[Collection(ApiCollection.Name)]
public class WeeklyDashboardApiTests(ApiFixture fixture) : DashboardApiTestBase(fixture)
{
    private async Task<WeeklyDashboardDto> GetAsync(DateOnly? end = null) =>
        (await Fixture.Client.GetFromJsonAsync<WeeklyDashboardDto>($"/api/dashboard/weekly?endDate={(end ?? Day):yyyy-MM-dd}"))!;

    private static NutrientStatusDto Row(WeeklyDashboardDto d, string code) => d.Averages.Nutrients.Single(n => n.Code == code);

    [Fact]
    public async Task AC_SCORE_011_Average_DividesByRecordDays_NotBySeven()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day.AddDays(-6), "dinner", new() { ["ENERGY"] = 1000m, ["PROT"] = 40m });
        await AddIntakeAsync(Day.AddDays(-4), "dinner", new() { ["ENERGY"] = 2000m, ["PROT"] = 60m });
        await AddIntakeAsync(Day.AddDays(-1), "breakfast", new() { ["ENERGY"] = 500m, ["PROT"] = 20m });
        await AddIntakeAsync(Day.AddDays(-1), "dinner", new() { ["ENERGY"] = 1000m, ["PROT"] = 20m }); // 同じ日の 2 件は日合計にする
        await AddIntakeAsync(Day, "lunch", new() { ["ENERGY"] = 1500m, ["PROT"] = 80m });

        var d = await GetAsync();

        Assert.Equal((Day, Day.AddDays(-6), 4, false), (d.EndDate, d.WindowStart, d.RecordDays, d.InsufficientData));
        Assert.Equal(new AverageEnergyDto(1500, 2155), d.Averages.Energy); // (1000+2000+1500+1500) ÷ 4
        Assert.Equal(55.0m, Row(d, "PROT").Intake); // (40+60+40+80) ÷ 4
        Assert.Equal(55.0m, d.Averages.Pfc!.ProteinG);
    }

    [Fact]
    public async Task AC_DASH_006_Series_HasSevenPoints_WithNullsForDaysWithoutRecords()
    {
        await PutProfileAsync();
        await AddTargetsAsync(("VITC", "RDA", 100m), ("VITE_ATOC", "RDA", 6m), ("VITA_RAE", "RDA", 700m), ("ZN", "RDA", 10m));
        await AddIntakeAsync(Day.AddDays(-5), "dinner", new() { ["ENERGY"] = 1850m, ["VITC"] = 78m, ["VITE_ATOC"] = 9m, ["VITA_RAE"] = 450m, ["ZN"] = 8m });
        await AddIntakeAsync(Day, "dinner", new() { ["ENERGY"] = 2155m, ["VITC"] = 100m, ["VITE_ATOC"] = 6m, ["VITA_RAE"] = 700m, ["ZN"] = 10m });
        await AddIntakeAsync(Day.AddDays(-7), "dinner", new() { ["ENERGY"] = 9999m }); // 窓の外
        await AddIntakeAsync(Day.AddDays(1), "dinner", new() { ["ENERGY"] = 9999m }); // 窓の外

        var d = await GetAsync();

        Assert.Equal(Enumerable.Range(0, 7).Select(i => Day.AddDays(i - 6)).ToList(), d.Series.Select(s => s.Date).ToList());
        Assert.Equal([false, true, false, false, false, false, true], d.Series.Select(s => s.HasRecords).ToList());
        var empty = d.Series[0];
        Assert.Equal((null, null), (empty.IntakeKcal, empty.Scores)); // 無記録日は 0 にしない
        Assert.Equal(1850, d.Series[1].IntakeKcal);
        Assert.Equal(81, d.Series[1].Scores!.Beauty); // 日ごとのスコア（その日の摂取量で計算）
        Assert.Equal(100, d.Series[6].Scores!.Beauty);
        Assert.Equal(2, d.RecordDays);
    }

    [Fact]
    public async Task AC_SCORE_005_TwoRecordDays_IsInsufficientData_ThreeDaysComputes()
    {
        await PutProfileAsync();
        await AddTargetsAsync(("VITC", "RDA", 100m), ("VITE_ATOC", "RDA", 6m), ("VITA_RAE", "RDA", 700m), ("ZN", "RDA", 10m));
        Dictionary<string, decimal> intake = new() { ["VITC"] = 78m, ["VITE_ATOC"] = 9m, ["VITA_RAE"] = 450m, ["ZN"] = 8m };
        await AddIntakeAsync(Day.AddDays(-2), "dinner", intake);
        await AddIntakeAsync(Day.AddDays(-1), "dinner", intake);

        var two = await GetAsync();

        Assert.Equal((2, true), (two.RecordDays, two.InsufficientData)); // AC-DASH-008: データ不足（記録 2 日）
        var none = new ScoreDto(null, null, true, false);
        Assert.Equal(new ScoresDto(none, none, none), two.Scores);
        Assert.Equal(78m, Row(two, "VITC").Intake); // 平均自体は返す

        await AddIntakeAsync(Day, "dinner", intake);
        var three = await GetAsync();

        Assert.Equal((3, false), (three.RecordDays, three.InsufficientData));
        Assert.Equal(new ScoreDto(81, "GOOD", false, false), three.Scores.Beauty);
    }

    [Fact]
    public async Task Muscle_UsesAverageIntake_AndDistributionAcrossWholeWindow()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day.AddDays(-2), "dinner", new() { ["PROT"] = 25m, ["ENERGY"] = 2155m });
        await AddIntakeAsync(Day.AddDays(-1), "dinner", new() { ["PROT"] = 10m, ["ENERGY"] = 2155m });
        await AddIntakeAsync(Day, "lunch", new() { ["PROT"] = 30m, ["ENERGY"] = 2155m });

        var d = await GetAsync();

        // タンパク質 平均 21.667 ÷ 目標 91.0 = 0.238095、分配率 2/3（25g・30g が 20g 以上）、エネルギー充足 1.0
        // 0.6×0.238095 + 0.2×0.666667 + 0.2×1.0 = 0.47619 → 48
        Assert.Equal(48, d.Scores.Muscle.Value);
        Assert.Equal("LOW", d.Scores.Muscle.Band);
    }

    [Fact]
    public async Task AC_SCORE_006_CountsUlExceededDays_AndKeepsAverageStatus()
    {
        await PutProfileAsync();
        await AddTargetsAsync(("ZN", "RDA", 10m), ("ZN", "UL", 35m));
        await AddIntakeAsync(Day.AddDays(-3), "dinner", new() { ["ZN"] = 40m });
        await AddIntakeAsync(Day.AddDays(-2), "dinner", new() { ["ZN"] = 10m });
        await AddIntakeAsync(Day.AddDays(-1), "dinner", new() { ["ZN"] = 50m });
        await AddIntakeAsync(Day, "dinner", new() { ["ZN"] = 35m }); // ちょうど UL は超過ではない

        var d = await GetAsync();

        var zn = Row(d, "ZN");
        Assert.Equal(2, zn.UlExceededDays); // 超過した日数（日ごとに判定）
        Assert.Equal((33.8m, "ACHIEVED"), (zn.Intake, zn.Status)); // 平均 33.75 は UL 以内
        Assert.Equal(0, Row(d, "VITC").UlExceededDays); // UL の無い栄養素は 0
    }

    [Fact]
    public async Task IncompleteNutrient_AnyDayInWindow_IsNoticed()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day.AddDays(-2), "dinner", new() { ["VITC"] = 31m });
        await AddIntakeAsync(Day.AddDays(-1), "dinner", new() { ["VITC"] = 50m }, "VITC");

        var d = await GetAsync();

        Assert.True(Row(d, "VITC").IsIncomplete);
        Assert.Equal(["VITC"], Assert.Single(d.Notices).NutrientCodes);
    }

    [Fact]
    public async Task NoRecords_ReturnsEmptyAverages_AndInsufficientData()
    {
        await PutProfileAsync();

        var d = await GetAsync();

        Assert.Equal((0, true), (d.RecordDays, d.InsufficientData));
        Assert.Null(d.Averages.Energy);
        Assert.Null(d.Averages.Pfc);
        Assert.Empty(d.Averages.Nutrients);
        Assert.Equal(7, d.Series.Count);
        Assert.All(d.Series, s => Assert.False(s.HasRecords));
        Assert.True(d.Scores.Beauty.InsufficientData);
        Assert.Empty(d.Notices);
    }

    [Fact]
    public async Task EndDate_InThePast_ShiftsTheWindow()
    {
        await PutProfileAsync();
        await AddIntakeAsync(Day.AddDays(-9), "dinner", new() { ["ENERGY"] = 1200m });

        var d = await GetAsync(Day.AddDays(-8));

        Assert.Equal((Day.AddDays(-14), 1), (d.WindowStart, d.RecordDays));
        Assert.Equal(1200, d.Averages.Energy!.IntakeKcal);
    }

    [Fact]
    public async Task InvalidRequests_Return400Or404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Fixture.Client.GetAsync($"/api/dashboard/weekly?endDate={Day:yyyy-MM-dd}")).StatusCode); // プロフィール未登録
        await PutProfileAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Fixture.Client.GetAsync("/api/dashboard/weekly")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Fixture.Client.GetAsync($"/api/dashboard/weekly?endDate={Day.AddDays(1):yyyy-MM-dd}")).StatusCode); // 未来日
        Assert.Equal(HttpStatusCode.OK, (await Fixture.Client.GetAsync($"/api/dashboard/weekly?endDate={Day:yyyy-MM-dd}")).StatusCode);
    }
}
