using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>
/// 栄養素の寄与食材 API（GET /api/nutrients/{code}/contributions、API-19、AC-DASH-005）のテストです。食材・数値は docs/03.栄養計算仕様.md §9 の正解データ（テスト用のダミー値）です。
/// </summary>
[Collection(ApiCollection.Name)]
public class ContributionsApiTests(ApiFixture fixture) : IAsyncLifetime
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
    /// テストで基準にする日（テスト中の今日）。
    /// </summary>
    private static readonly DateOnly Day = ApiFixture.Today;

    /// <summary>
    /// テストの準備で作ったデータ（正解データの食材と、メニュー「豚汁」）。
    /// </summary>
    private sealed record Seeded(TestData.Golden Golden, MenuDetailDto Tonjiru);

    /// <summary>
    /// 正解データの食材を登録し、メニュー「豚汁」（2400g・8 杯分）を作ります。
    /// </summary>
    /// <returns>登録した食材と、作った豚汁。</returns>
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

    /// <summary>
    /// 指定した日の夕食を記録します（201 で成功したことも確かめます）。
    /// </summary>
    /// <param name="date">食べた日。</param>
    /// <param name="items">食べたもの。</param>
    /// <returns>完了を表すタスク。</returns>
    private async Task RecordAsync(DateOnly date, params MealRecordItemInput[] items) =>
        Assert.Equal(HttpStatusCode.Created, (await fixture.Client.PostAsJsonAsync("/api/meal-records", new MealRecordInput(date, "dinner", null, items))).StatusCode);

    /// <summary>
    /// 寄与食材 API を呼び、応答をそのまま返します（エラーの確認用）。
    /// </summary>
    /// <param name="code">栄養素コード。</param>
    /// <param name="query">クエリ文字列。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> GetRawAsync(string code, string query) => await fixture.Client.GetAsync($"/api/nutrients/{code}/contributions?{query}");

    /// <summary>
    /// 寄与食材 API を呼びます。
    /// </summary>
    /// <param name="code">栄養素コード。</param>
    /// <param name="from">期間の開始日。</param>
    /// <param name="to">期間の終了日。</param>
    /// <returns>寄与食材。</returns>
    private async Task<ContributionsDto> GetAsync(string code, DateOnly from, DateOnly to) =>
        (await fixture.Client.GetFromJsonAsync<ContributionsDto>($"/api/nutrients/{code}/contributions?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"))!;

    /// <summary>
    /// タンパク質の寄与が、メニュー・単品の食材ごとに集計され、量の大きい順（豚こま肉 30.0g が豚汁 28.5g より先）に、割合（51.2%、48.8%）つきで返ること、メニューには現在の構成と食べた割合の合計（0.3125）から按分した食材の内訳（豚こま肉 25.0g、みそ 2.7g、にんじん 0.8g）が付くことを確認します（AC-DASH-005）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 期間内の記録だけが集計されることを確認します（期間より前の記録は含まれない）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 未測定を含む栄養素（ビタミン C）は未測定の印がつき、未測定の食材（みそ）は内訳に出ないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 記録がないとき、合計が 0・未測定の印なし・項目が空で返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task NoRecords_ReturnsZeroTotalAndEmptyItems()
    {
        var c = await GetAsync("PROT", Day, Day);

        Assert.Equal((0m, false), (c.TotalAmount, c.IsIncomplete));
        Assert.Empty(c.Items);
    }

    /// <summary>
    /// メニューを削除しても、過去の記録の寄与には残ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task DeletedMenu_StillShowsItsPastContribution()
    {
        var s = await SeedAsync();
        await RecordAsync(Day, new MealRecordItemInput(s.Tonjiru.Id, null, null, "SERVING", 1m));
        await fixture.Client.DeleteAsync($"/api/menus/{s.Tonjiru.Id}");

        var c = await GetAsync("PROT", Day, Day);

        Assert.Equal("豚汁", Assert.Single(c.Items).Name); // 過去の記録は残る
    }

    /// <summary>
    /// 存在しない栄養素は 404、期間がない・開始日が終了日より後・32 日は 400、ちょうど 31 日は 200 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
