using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.MealRecords;

/// <summary>
/// 最近使った食材・メニュー API（GET /api/recent-items、API-20、AC-REC-009）のテストです。
/// </summary>
[Collection(ApiCollection.Name)]
public class RecentItemsApiTests(ApiFixture fixture) : IAsyncLifetime
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
    /// 正解データの 3 食材をデータベースに登録します。
    /// </summary>
    /// <returns>登録した 3 食材。</returns>
    private async Task<TestData.Golden> SeedAsync()
    {
        TestData.Golden? golden = null;
        await fixture.WithDbAsync(async db => golden = await TestData.AddGoldenAsync(db));
        return golden!;
    }

    /// <summary>
    /// 豚こま肉 200g だけのメニュー（1000g、4 杯分）を作ります。
    /// </summary>
    /// <param name="g">正解データの 3 食材。</param>
    /// <param name="name">メニューの名前。</param>
    /// <returns>作ったメニュー。</returns>
    private async Task<MenuDetailDto> CreateMenuAsync(TestData.Golden g, string name) =>
        (await (await fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput(
            name, null, null, 1000m, 4m, null, [new MenuItemInput(g.Pork.Id, null, 200m)]))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

    /// <summary>
    /// 指定した日の夕食を記録します（201 で成功したことも確かめます）。
    /// </summary>
    /// <param name="date">食べた日。</param>
    /// <param name="items">食べたもの。</param>
    /// <returns>完了を表すタスク。</returns>
    private async Task RecordAsync(DateOnly date, params MealRecordItemInput[] items)
    {
        var res = await fixture.Client.PostAsJsonAsync("/api/meal-records", new MealRecordInput(date, "dinner", null, items));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    /// <summary>
    /// 最近使った食材・メニュー API を呼びます。
    /// </summary>
    /// <param name="query">クエリ文字列（例: ?limit=2）。</param>
    /// <returns>最近使ったメニューと食材。</returns>
    private async Task<RecentItemsDto> GetAsync(string query = "") =>
        (await fixture.Client.GetFromJsonAsync<RecentItemsDto>($"/api/recent-items{query}"))!;

    /// <summary>
    /// メニューと食材が、使った日の新しい順に、重複なしで返り、最後に使ったときの入力方式・入力値・日付が入ることを確認します（AC-REC-009）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task AC_REC_009_ReturnsMostRecentFirst_WithoutDuplicates_AndLastInput()
    {
        var g = await SeedAsync();
        var soup = await CreateMenuAsync(g, "豚汁");
        var stew = await CreateMenuAsync(g, "シチュー");
        await RecordAsync(Day.AddDays(-3), new MealRecordItemInput(soup.Id, null, null, "SERVING", 1m));
        await RecordAsync(Day.AddDays(-2), new MealRecordItemInput(stew.Id, null, null, "RATIO", 0.25m),
            new MealRecordItemInput(null, g.Carrot.Id, g.CarrotUnitId, "UNIT", 2m));
        await RecordAsync(Day.AddDays(-1), new MealRecordItemInput(soup.Id, null, null, "SERVING", 1.5m),
            new MealRecordItemInput(null, g.Miso.Id, null, "GRAM", 18m));
        await RecordAsync(Day.AddDays(-1), new MealRecordItemInput(null, g.Carrot.Id, null, "GRAM", 100m));

        var recent = await GetAsync();

        Assert.Equal(["豚汁", "シチュー"], recent.Menus.Select(m => m.Name).ToList()); // 重複なし、使った順
        Assert.Equal(("SERVING", 1.5m, Day.AddDays(-1)), (recent.Menus[0].LastInputMode, recent.Menus[0].LastInputValue, recent.Menus[0].LastUsedOn));
        Assert.Equal(["にんじん", "みそ"], recent.Foods.Select(f => f.Name).ToList());
        var carrot = recent.Foods[0];
        Assert.Equal(("GRAM", 100m, (long?)null, Day.AddDays(-1)), (carrot.LastInputMode, carrot.LastInputValue, carrot.UnitId, carrot.LastUsedOn)); // 直近の入力
    }

    /// <summary>
    /// 削除済みのメニューと無効な食材は、最近使ったものに含まれないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task ExcludesDeletedMenusAndInactiveFoods()
    {
        var g = await SeedAsync();
        var gone = await CreateMenuAsync(g, "削除する汁");
        var alive = await CreateMenuAsync(g, "残す汁");
        await RecordAsync(Day, new MealRecordItemInput(gone.Id, null, null, "SERVING", 1m),
            new MealRecordItemInput(alive.Id, null, null, "SERVING", 1m),
            new MealRecordItemInput(null, g.Pork.Id, null, "GRAM", 100m),
            new MealRecordItemInput(null, g.Miso.Id, null, "GRAM", 10m));
        await fixture.Client.DeleteAsync($"/api/menus/{gone.Id}");
        await fixture.WithDbAsync(async db =>
        {
            (await db.Foods.SingleAsync(f => f.Id == g.Pork.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var recent = await GetAsync();

        Assert.Equal(["残す汁"], recent.Menus.Select(m => m.Name).ToList());
        Assert.Equal(["みそ"], recent.Foods.Select(f => f.Name).ToList());
    }

    /// <summary>
    /// 件数の指定を確認します。指定なしは 10 件、limit=2 は 2 件、大きな値は上限 30 件までで、メニュー・食材それぞれに適用されます。limit=0 は 400 です。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Limit_DefaultsTo10_CapsAt30_AndAppliesToEachList()
    {
        var g = await SeedAsync();
        for (var i = 0; i < 12; i++)
        {
            var menu = await CreateMenuAsync(g, $"汁{i:00}");
            await RecordAsync(Day.AddDays(-i), new MealRecordItemInput(menu.Id, null, null, "SERVING", 1m));
        }

        Assert.Equal(10, (await GetAsync()).Menus.Count);
        Assert.Equal(2, (await GetAsync("?limit=2")).Menus.Count);
        Assert.Equal(12, (await GetAsync("?limit=100")).Menus.Count); // 上限 30 まで（12 件しか無い）
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/recent-items?limit=0")).StatusCode);
    }

    /// <summary>
    /// 記録がないとき、メニューも食材も空の一覧で返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task NoRecords_ReturnsEmptyLists()
    {
        var recent = await GetAsync();

        Assert.Empty(recent.Menus);
        Assert.Empty(recent.Foods);
    }
}
