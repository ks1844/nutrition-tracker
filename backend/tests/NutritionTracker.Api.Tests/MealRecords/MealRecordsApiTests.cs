using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.MealRecords;

/// <summary>食事記録 API（API-13〜16, 20, 21、docs/09.API仕様.md §6）。数値は docs/03.栄養計算仕様.md §9 の正解データ（テスト用ダミー値）。</summary>
[Collection(ApiCollection.Name)]
public class MealRecordsApiTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateOnly Day = ApiFixture.Today;

    private sealed record Seeded(TestData.Golden Golden, MenuDetailDto Tonjiru);

    /// <summary>『豚汁』（2400g、8 杯）と正解データの食材を用意する。</summary>
    private async Task<Seeded> SeedAsync()
    {
        TestData.Golden? golden = null;
        await fixture.WithDbAsync(async db => golden = await TestData.AddGoldenAsync(db));
        var g = golden!;
        var res = await fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput(
            "豚汁", new DateOnly(2026, 10, 14), null, 2400m, 8m, "お椀",
            [
                new MenuItemInput(g.Carrot.Id, g.CarrotUnitId, 2m),
                new MenuItemInput(g.Pork.Id, null, 400m),
                new MenuItemInput(g.Miso.Id, g.MisoUnitId, 4m),
            ]));
        return new Seeded(g, (await res.Content.ReadFromJsonAsync<MenuDetailDto>())!);
    }

    private static MealRecordInput Dinner(params MealRecordItemInput[] items) => new(Day, "dinner", null, items);

    private static MealRecordItemInput MenuItem(long menuId, string mode, decimal value) => new(menuId, null, null, mode, value);

    private async Task<HttpResponseMessage> PostAsync(MealRecordInput input) => await fixture.Client.PostAsJsonAsync("/api/meal-records", input);

    private async Task<MealRecordSaveResultDto> CreateAsync(MealRecordInput input)
    {
        var res = await PostAsync(input);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<MealRecordSaveResultDto>())!;
    }

    private static NutrientAmountDto Nutrient(MealRecordItemDto item, string code) => item.Nutrients.Single(n => n.Code == code);

    // ---- API-14 作成（メニュー） ----
    [Theory]
    [InlineData("RATIO", 0.25, 0.25, 22.8350)] // AC-REC-001 / AC-CALC-009
    [InlineData("SERVING", 1.5, 0.1875, 17.1263)] // AC-REC-002 / AC-CALC-006（17.12625 を HALF_UP）
    [InlineData("GRAM", 300, 0.125, 11.4175)] // AC-REC-003 / AC-CALC-005
    [InlineData("GRAM", 800, 0.333333, 30.4466)] // AC-CALC-007
    public async Task CreateMenuRecord_ComputesPortionRatioAndSnapshot(string mode, double value, double ratio, double prot)
    {
        var s = await SeedAsync();

        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, mode, (decimal)value)));

        var item = Assert.Single(created.Items);
        Assert.Equal((decimal)ratio, item.PortionRatio);
        Assert.Equal(new NutrientAmountDto("PROT", (decimal)prot, false), Nutrient(item, "PROT"));
        Assert.True(Nutrient(item, "VITC").IsIncomplete); // みそが未測定 → 不完全が伝わる
        Assert.Equal(("豚汁", s.Tonjiru.Id, null, null), (item.MenuName, item.MenuId, item.FoodId, item.FoodName));
        Assert.Equal(mode == "GRAM" ? (decimal)value : null, item.AmountG); // メニューの GRAM は食べた g を保持
    }

    [Fact]
    public async Task CreateMenuRecord_SavesRecordAndOneSnapshotRowPerNutrient()
    {
        var s = await SeedAsync();

        var created = await CreateAsync(new MealRecordInput(Day, "lunch", "作り置き", [MenuItem(s.Tonjiru.Id, "SERVING", 1m)]));

        Assert.Equal(("lunch", "作り置き", Day), (created.MealType, created.Memo, created.EatenOn));
        var rows = await fixture.WithDbAsync(db => db.MealRecordItemNutrients.AsNoTracking().ToListAsync());
        Assert.Equal(24, rows.Count); // 栄養素の数だけ縦持ちで保存
        var prot = await fixture.WithDbAsync(async db =>
        {
            var id = await db.Nutrients.Where(n => n.Code == "PROT").Select(n => n.Id).SingleAsync();
            return await db.MealRecordItemNutrients.AsNoTracking().SingleAsync(n => n.NutrientId == id);
        });
        Assert.Equal(11.4175m, prot.Amount);
    }

    // ---- API-14 作成（単品食材） ----
    [Fact]
    public async Task AC_REC_004_CreateFoodRecord_Gram_UsesAmountNotPortionRatio()
    {
        var s = await SeedAsync();

        var created = await CreateAsync(Dinner(
            new MealRecordItemInput(null, s.Golden.Carrot.Id, null, "GRAM", 100m),
            new MealRecordItemInput(null, s.Golden.Miso.Id, null, "GRAM", 18m)));

        var carrot = created.Items[0];
        Assert.Null(carrot.PortionRatio);
        Assert.Equal(100m, carrot.AmountG);
        Assert.Equal(new NutrientAmountDto("PROT", 0.9m, false), Nutrient(carrot, "PROT")); // AC-CALC-011
        Assert.Equal(new NutrientAmountDto("VITC", 9m, false), Nutrient(carrot, "VITC"));
        Assert.Equal(new NutrientAmountDto("VITA_RAE", 630m, false), Nutrient(carrot, "VITA_RAE"));
        var miso = created.Items[1];
        Assert.Equal(new NutrientAmountDto("PROT", 2.16m, false), Nutrient(miso, "PROT"));
        Assert.Equal(new NutrientAmountDto("VITC", 0m, true), Nutrient(miso, "VITC")); // 未測定は 0 + 不完全
        Assert.Equal(new NutrientAmountDto("VITA_RAE", 0m, false), Nutrient(miso, "VITA_RAE"));
    }

    [Fact]
    public async Task AC_REC_004_CreateFoodRecord_Unit_ConvertsToGrams()
    {
        var s = await SeedAsync();

        var created = await CreateAsync(Dinner(new MealRecordItemInput(null, s.Golden.Carrot.Id, s.Golden.CarrotUnitId, "UNIT", 1m)));

        var item = Assert.Single(created.Items);
        Assert.Equal(150m, item.AmountG); // 1 本 = 150g
        Assert.Equal(("UNIT", 1m), (item.InputMode, item.InputValue));
        Assert.Equal(1.35m, Nutrient(item, "PROT").Amount); // 150g × 0.9 ÷ 100 × 1.0
        var stored = await fixture.WithDbAsync(db => db.MealRecordItems.AsNoTracking().SingleAsync());
        Assert.Equal(s.Golden.CarrotUnitId, stored.UnitId);
    }

    [Fact]
    public async Task AC_REC_005_MenuAndFood_InOneMeal_AreOneRecordWithTwoItems()
    {
        var s = await SeedAsync();

        var created = await CreateAsync(Dinner(
            MenuItem(s.Tonjiru.Id, "SERVING", 1m),
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)));

        Assert.Equal(2, created.Items.Count);
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecords.CountAsync()));
        Assert.Equal(2, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync()));
        Assert.Equal(30m, Nutrient(created.Items[1], "PROT").Amount); // 150g × 20 ÷ 100
    }

    // ---- 100% 超過の警告 ----
    [Fact]
    public async Task AC_REC_008_OverHundredPercent_IsSavedWithWarning()
    {
        var s = await SeedAsync();
        var first = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 0.9m)));
        Assert.Empty(first.Warnings);

        var second = await CreateAsync(new MealRecordInput(Day, "snack", null, [MenuItem(s.Tonjiru.Id, "RATIO", 0.2m)]));

        var warning = Assert.Single(second.Warnings);
        Assert.Equal(("MENU_OVER_100PCT", s.Tonjiru.Id, 1.1m), (warning.Code, warning.MenuId, warning.TotalPortionRatio));
        Assert.Equal("豚汁の記録が合計100%を超えています", warning.Message);
        Assert.Equal(2, await fixture.WithDbAsync(db => db.MealRecords.CountAsync())); // 警告でも保存される
    }

    [Fact]
    public async Task ExactlyHundredPercent_HasNoWarning()
    {
        var s = await SeedAsync();
        await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 0.5m)));

        var second = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 4m)));

        Assert.Empty(second.Warnings);
    }

    // ---- 入力エラー ----
    [Fact]
    public async Task Create_MissingFields_Returns400WithFieldDetails()
    {
        var res = await PostAsync(new MealRecordInput(null, "brunch", null, []));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.Equal("VALIDATION_ERROR", err.Code);
        Assert.Equal(["eatenOn", "mealType", "items"], err.Details.Select(d => d.Field).ToList());
        Assert.Equal(0, await fixture.WithDbAsync(db => db.MealRecords.CountAsync()));
    }

    [Fact]
    public async Task Create_InvalidItems_Returns400WithItemFields()
    {
        var s = await SeedAsync();
        var res = await PostAsync(Dinner(
            new MealRecordItemInput(s.Tonjiru.Id, s.Golden.Pork.Id, null, "GRAM", 10m), // 両方指定
            new MealRecordItemInput(null, null, null, "GRAM", 10m), // どちらも無し
            MenuItem(s.Tonjiru.Id, "UNIT", 1m), // メニューに UNIT は不可
            MenuItem(s.Tonjiru.Id, "GRAM", 0m), // 0 以下
            new MealRecordItemInput(null, s.Golden.Carrot.Id, null, "UNIT", 1m), // 単位なし
            new MealRecordItemInput(null, s.Golden.Carrot.Id, s.Golden.MisoUnitId, "UNIT", 1m), // 他の食材の単位
            MenuItem(999_999, "RATIO", 0.1m), // 存在しないメニュー
            new MealRecordItemInput(null, 999_999, null, "GRAM", 10m))); // 存在しない食材

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(
            ["items[0].menuId", "items[1].menuId", "items[2].inputMode", "items[3].inputValue", "items[4].unitId", "items[5].unitId", "items[6].menuId", "items[7].foodId"],
            fields);
    }

    [Fact]
    public async Task Create_DeletedMenu_Returns400()
    {
        var s = await SeedAsync();
        await fixture.Client.DeleteAsync($"/api/menus/{s.Tonjiru.Id}");

        var res = await PostAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m))); // AC-MENU-007: 削除済みは新規記録の対象外

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var detail = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single();
        Assert.Equal("items[0].menuId", detail.Field);
    }

    [Fact]
    public async Task Create_InactiveFood_Returns409()
    {
        var s = await SeedAsync();
        await fixture.WithDbAsync(async db =>
        {
            (await db.Foods.SingleAsync(f => f.Id == s.Golden.Pork.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var res = await PostAsync(Dinner(new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 100m)));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(new ErrorDetail("items[0].foodId", "FOOD_INACTIVE"), (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single());
        Assert.Equal(0, await fixture.WithDbAsync(db => db.MealRecords.CountAsync()));
    }

    [Fact]
    public async Task Create_TooSmallAmount_Returns400_NotServerError()
    {
        var s = await SeedAsync();

        var res = await PostAsync(Dinner(MenuItem(s.Tonjiru.Id, "GRAM", 0.0005m))); // 割合が 6 桁で 0 になる

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
