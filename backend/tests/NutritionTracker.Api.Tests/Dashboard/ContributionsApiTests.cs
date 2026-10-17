using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>栄養素の寄与食材 API（API-19、AC-DASH-005）。食材・数値は docs/03.栄養計算仕様.md §9 の正解データ（テスト用ダミー値）。</summary>
[Collection(ApiCollection.Name)]
public class ContributionsApiTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateOnly Day = ApiFixture.Today;

    private sealed record Seeded(TestData.Golden Golden, MenuDetailDto Tonjiru);

    private async Task<Seeded> SeedAsync()
    {
        TestData.Golden? golden = null;
        await fixture.WithDbAsync(async db => golden = await TestData.AddGoldenAsync(db));
        var g = golden!;
        var menu = (await (await fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput(
            "豚汁", null, null, 2400m, 8m, "お椀",
            [new MenuItemInput(g.Carrot.Id, g.CarrotUnitId, 2m), new MenuItemInput(g.Pork.Id, null, 400m), new MenuItemInput(g.Miso.Id, g.MisoUnitId, 4m)])))
            .Content.ReadFromJsonAsync<MenuDetailDto>())!;
        return new Seeded(g, menu);
    }

    private async Task RecordAsync(DateOnly date, params MealRecordItemInput[] items) =>
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/meal-records", new MealRecordInput(date, "dinner", null, items))).StatusCode);

    private async Task<HttpResponseMessage> GetRawAsync(string code, string query) => await fixture.Client.GetAsync($"/api/nutrients/{code}/contributions?{query}");

    private async Task<ContributionsDto> GetAsync(string code, DateOnly from, DateOnly to) =>
        (await fixture.Client.GetFromJsonAsync<ContributionsDto>($"/api/nutrients/{code}/contributions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"))!;

    [Fact]
    public async Task AC_DASH_005_AggregatesPerMenuAndFood_LargestFirst_WithChildren()
    {
        var s = await SeedAsync();
        await RecordAsync(Day.AddDays(-1), new MealRecordItemInput(s.Tonjiru.Id, null, null, "SERVING", 1m)); // PROT 11.4175
        await RecordAsync(Day, new MealRecordItemInput(s.Tonjiru.Id, null, null, "SERVING", 1.5m), // PROT 17.1263
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)); // PROT 30

        var c = await GetAsync("PROT", Day.AddDays(-6), Day);

        Assert.Equal(("PROT", Day.AddDays(-6), Day, 58.5m, false), (c.NutrientCode, c.From, c.To, c.TotalAmount, c.IsIncomplete)); // 58.5438
        Assert.Equal(["FOOD", "MENU"], c.Items.Select(i => i.Type).ToList()); // 寄与の大きい順（30.0 > 28.5）
        var food = c.Items[0];
        Assert.Equal((s.Golden.Pork.Id, "豚こま肉", 30.0m, 51.2m), (food.Id, food.Name, food.Amount, food.SharePct));
        Assert.Empty(food.Children);
        var menu = c.Items[1];
        Assert.Equal((s.Tonjiru.Id, "豚汁", 28.5m, 48.8m), (menu.Id, menu.Name, menu.Amount, menu.SharePct));
        // 食材内訳 = メニューの現在の構成 × 期間内の食べた割合の合計（0.3125）。豚こま肉 80×0.3125、みそ 8.64×0.3125、にんじん 2.7×0.3125
        Assert.Equal(
            [("豚こま肉", 25.0m), ("みそ", 2.7m), ("にんじん", 0.8m)],
            menu.Children.Select(ch => (ch.Name, ch.Amount)).ToList());
        Assert.Equal(s.Golden.Pork.Id, menu.Children[0].FoodId);
    }

    [Fact]
    public async Task OnlyRecordsInThePeriod_AreCounted()
    {
        var s = await SeedAsync();
        await RecordAsync(Day.AddDays(-10), new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 100m));
        await RecordAsync(Day.AddDays(-3), new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 50m));

        var c = await GetAsync("PROT", Day.AddDays(-6), Day);

        Assert.Equal(10.0m, c.TotalAmount);
        Assert.Equal(100.0m, Assert.Single(c.Items).SharePct);
    }

    [Fact]
    public async Task IncompleteNutrient_IsFlagged_AndUnmeasuredFoodHasNoChild()
    {
        var s = await SeedAsync();
        await RecordAsync(Day, new MealRecordItemInput(s.Tonjiru.Id, null, null, "SERVING", 1m));

        var c = await GetAsync("VITC", Day, Day);

        Assert.True(c.IsIncomplete); // みそが未測定
        var menu = Assert.Single(c.Items);
        Assert.Equal(3.9m, menu.Amount); // 31 × 0.125 = 3.875
        Assert.DoesNotContain(menu.Children, ch => ch.Name == "みそ"); // 未測定は 0 扱いで内訳に出さない
    }

    [Fact]
    public async Task NoRecords_ReturnsZeroTotalAndEmptyItems()
    {
        var c = await GetAsync("PROT", Day, Day);

        Assert.Equal((0m, false), (c.TotalAmount, c.IsIncomplete));
        Assert.Empty(c.Items);
    }

    [Fact]
    public async Task DeletedMenu_StillShowsItsPastContribution()
    {
        var s = await SeedAsync();
        await RecordAsync(Day, new MealRecordItemInput(s.Tonjiru.Id, null, null, "SERVING", 1m));
        await fixture.Client.DeleteAsync($"/api/menus/{s.Tonjiru.Id}");

        var c = await GetAsync("PROT", Day, Day);

        Assert.Equal("豚汁", Assert.Single(c.Items).Name); // 過去の記録は残る
    }

    [Fact]
    public async Task InvalidRequests_Return400Or404()
    {
        var q = $"from={Day:yyyy-MM-dd}&to={Day:yyyy-MM-dd}";

        Assert.Equal(HttpStatusCode.NotFound, (await GetRawAsync("NOPE", q)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await GetRawAsync("PROT", "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await GetRawAsync("PROT", $"from={Day:yyyy-MM-dd}&to={Day.AddDays(-1):yyyy-MM-dd}")).StatusCode); // from > to
        Assert.Equal(HttpStatusCode.BadRequest, (await GetRawAsync("PROT", $"from={Day.AddDays(-31):yyyy-MM-dd}&to={Day:yyyy-MM-dd}")).StatusCode); // 32 日
        Assert.Equal(HttpStatusCode.OK, (await GetRawAsync("PROT", $"from={Day.AddDays(-30):yyyy-MM-dd}&to={Day:yyyy-MM-dd}")).StatusCode); // ちょうど 31 日
    }
}
