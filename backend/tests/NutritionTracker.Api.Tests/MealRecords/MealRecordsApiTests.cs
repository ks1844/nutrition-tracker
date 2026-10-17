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

    // ---- API-13 日別取得 ----
    private async Task<MealRecordDayDto> GetDayAsync(DateOnly date) =>
        (await fixture.Client.GetFromJsonAsync<MealRecordDayDto>($"/api/meal-records?date={date:yyyy-MM-dd}"))!;

    [Fact]
    public async Task AC_REC_005_GetByDate_ReturnsRecordsInMealTypeOrder_OnlyForThatDate()
    {
        var s = await SeedAsync();
        await CreateAsync(new MealRecordInput(Day, "snack", null, [MenuItem(s.Tonjiru.Id, "RATIO", 0.1m)]));
        await CreateAsync(new MealRecordInput(Day, "breakfast", "朝", [MenuItem(s.Tonjiru.Id, "RATIO", 0.1m)]));
        await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m), new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)));
        await CreateAsync(new MealRecordInput(Day.AddDays(-1), "lunch", null, [MenuItem(s.Tonjiru.Id, "RATIO", 0.1m)]));

        var day = await GetDayAsync(Day);

        Assert.Equal(Day, day.Date);
        Assert.Equal(["breakfast", "dinner", "snack"], day.Records.Select(r => r.MealType).ToList());
        Assert.Equal("朝", day.Records[0].Memo);
        var dinner = day.Records[1];
        Assert.Equal([s.Tonjiru.Id, null], dinner.Items.Select(i => i.MenuId).ToList());
        Assert.Equal(["豚汁", null], dinner.Items.Select(i => i.MenuName).ToList());
        Assert.Equal([null, "豚こま肉"], dinner.Items.Select(i => i.FoodName).ToList());
        Assert.Equal(24, dinner.Items[0].Nutrients.Count);
    }

    [Fact]
    public async Task GetByDate_NoRecords_ReturnsEmptyList()
    {
        var day = await GetDayAsync(Day);

        Assert.Empty(day.Records);
    }

    [Fact]
    public async Task GetByDate_ReturnsSavedSnapshot_EvenAfterMenuIsEdited()
    {
        var s = await SeedAsync();
        await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));
        await fixture.Client.PutAsJsonAsync($"/api/menus/{s.Tonjiru.Id}", new MenuInput(
            "豚汁", null, null, 2400m, 8m, "お椀", [new MenuItemInput(s.Golden.Pork.Id, null, 100m)]));

        var item = (await GetDayAsync(Day)).Records.Single().Items.Single();

        Assert.Equal(11.4175m, Nutrient(item, "PROT").Amount); // 保存時の値（メニュー編集の影響を受けない）
    }

    [Fact]
    public async Task GetByDate_MissingOrInvalidDate_Returns400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/meal-records")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/meal-records?date=2026-13-40")).StatusCode);
    }

    // ---- API-15 更新 ----
    private async Task<HttpResponseMessage> PutAsync(long id, MealRecordInput input) => await fixture.Client.PutAsJsonAsync($"/api/meal-records/{id}", input);

    [Fact]
    public async Task AC_REC_006_Update_ChangedItemIsRecalculated_UnchangedItemIsKept()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(
            MenuItem(s.Tonjiru.Id, "SERVING", 1m),
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)));
        var keptItemId = created.Items[1].Id;
        // メニューを書き換える。変更した項目はこの内容で再計算され、同じ入力の項目は変わらない（暫定仕様 §8）
        await fixture.Client.PutAsJsonAsync($"/api/menus/{s.Tonjiru.Id}", new MenuInput(
            "豚汁", null, null, 2400m, 8m, "お椀", [new MenuItemInput(s.Golden.Pork.Id, null, 800m)]));

        var res = await PutAsync(created.Id, Dinner(
            MenuItem(s.Tonjiru.Id, "SERVING", 1.5m),
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = (await res.Content.ReadFromJsonAsync<MealRecordSaveResultDto>())!;
        Assert.Equal(created.Id, updated.Id);
        var menuItem = updated.Items.Single(i => i.MenuId == s.Tonjiru.Id);
        Assert.Equal(0.1875m, menuItem.PortionRatio);
        Assert.Equal(1.5m, menuItem.InputValue);
        Assert.Equal(30m, Nutrient(menuItem, "PROT").Amount); // 変更後のメニュー（豚こま肉 800g = PROT 160）× 0.1875。更新前の 11.4175 ではない
        var kept = updated.Items.Single(i => i.FoodId == s.Golden.Pork.Id);
        Assert.Equal(keptItemId, kept.Id); // 同じ項目のまま
        Assert.Equal(30m, Nutrient(kept, "PROT").Amount);
        Assert.Equal(2, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync()));
        Assert.Equal(48, await fixture.WithDbAsync(db => db.MealRecordItemNutrients.CountAsync())); // 古いスナップショットは残らない
    }

    [Fact]
    public async Task Update_ChangesMealTypeDateMemo_AndRemovesDroppedItems()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(
            MenuItem(s.Tonjiru.Id, "SERVING", 1m),
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)));

        var res = await PutAsync(created.Id, new MealRecordInput(Day.AddDays(1), "lunch", "お弁当", [MenuItem(s.Tonjiru.Id, "SERVING", 1m)]));

        var updated = (await res.Content.ReadFromJsonAsync<MealRecordSaveResultDto>())!;
        Assert.Equal((Day.AddDays(1), "lunch", "お弁当"), (updated.EatenOn, updated.MealType, updated.Memo));
        Assert.Single(updated.Items);
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync())); // 外した項目は消える
        Assert.Empty((await GetDayAsync(Day)).Records);
    }

    [Fact]
    public async Task Update_KeepsUnchangedItem_EvenWhenMenuWasDeleted_ButRejectsNewDeletedMenu()
    {
        var s = await SeedAsync();
        var other = (await (await fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput(
            "別の汁", null, null, 1000m, 4m, null, [new MenuItemInput(s.Golden.Pork.Id, null, 100m)]))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));
        await fixture.Client.DeleteAsync($"/api/menus/{s.Tonjiru.Id}");
        await fixture.Client.DeleteAsync($"/api/menus/{other.Id}");

        var keep = await PutAsync(created.Id, new MealRecordInput(Day, "lunch", null, [MenuItem(s.Tonjiru.Id, "SERVING", 1m)]));
        var addDeleted = await PutAsync(created.Id, Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m), MenuItem(other.Id, "RATIO", 0.1m)));

        Assert.Equal(HttpStatusCode.OK, keep.StatusCode); // 既存項目は削除済みメニューでも保持できる
        Assert.Equal(HttpStatusCode.BadRequest, addDeleted.StatusCode);
        Assert.Equal("items[1].menuId", (await addDeleted.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single().Field);
    }

    [Fact]
    public async Task Update_InvalidInput_Returns400_AndKeepsOriginal()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));

        var res = await PutAsync(created.Id, new MealRecordInput(Day, "dinner", null, [MenuItem(s.Tonjiru.Id, "SERVING", 0m)]));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var item = (await GetDayAsync(Day)).Records.Single().Items.Single();
        Assert.Equal(1m, item.InputValue);
    }

    [Fact]
    public async Task Update_MissingRecord_Returns404()
    {
        var s = await SeedAsync();

        var res = await PutAsync(999_999, Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Update_OverHundredPercent_ReturnsWarning()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 0.5m)));

        var updated = (await (await PutAsync(created.Id, Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 1.2m)))).Content.ReadFromJsonAsync<MealRecordSaveResultDto>())!;

        Assert.Equal(1.2m, Assert.Single(updated.Warnings).TotalPortionRatio); // 置き換えた後の合計（古い 0.5 は含まない）
    }

    // ---- API-16 削除 ----
    [Fact]
    public async Task AC_REC_007_Delete_RemovesItemsAndSnapshots_AndRestoresMenuRemaining()
    {
        var s = await SeedAsync();
        var kept = await CreateAsync(new MealRecordInput(Day, "lunch", null, [MenuItem(s.Tonjiru.Id, "SERVING", 1m)]));
        var target = await CreateAsync(Dinner(
            MenuItem(s.Tonjiru.Id, "SERVING", 1.5m),
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)));
        var before = (await fixture.Client.GetFromJsonAsync<MenuDetailDto>($"/api/menus/{s.Tonjiru.Id}"))!.Remaining;
        Assert.Equal(0.6875m, before.Ratio);

        var res = await fixture.Client.DeleteAsync($"/api/meal-records/{target.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecords.CountAsync()));
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync())); // 項目もカスケードで消える
        Assert.Equal(24, await fixture.WithDbAsync(db => db.MealRecordItemNutrients.CountAsync())); // スナップショットも消える
        Assert.Equal([kept.Id], (await GetDayAsync(Day)).Records.Select(r => r.Id).ToList());
        var after = (await fixture.Client.GetFromJsonAsync<MenuDetailDto>($"/api/menus/{s.Tonjiru.Id}"))!.Remaining;
        Assert.Equal(new RemainingDto(0.875m, 7m, false), after); // 1.5 杯分が戻る
    }

    [Fact]
    public async Task Delete_MissingRecord_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync("/api/meal-records/999999")).StatusCode);
    }

    [Fact]
    public async Task Delete_Twice_SecondReturns404()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));
        await fixture.Client.DeleteAsync($"/api/meal-records/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync($"/api/meal-records/{created.Id}")).StatusCode);
    }

    // ---- API-21 コピー ----
    private static readonly DateOnly Yesterday = Day.AddDays(-1);

    private async Task<HttpResponseMessage> CopyAsync(DateOnly? from, DateOnly? to, bool? allowExisting = null) =>
        await fixture.Client.PostAsJsonAsync("/api/meal-records/copy", new MealRecordCopyInput(from, to, allowExisting));

    [Fact]
    public async Task AC_REC_010_Copy_RecreatesRecordsWithCurrentMenuContent()
    {
        var s = await SeedAsync();
        await CreateAsync(new MealRecordInput(Yesterday, "lunch", "お弁当", [new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 150m)]));
        await CreateAsync(new MealRecordInput(Yesterday, "dinner", null,
            [MenuItem(s.Tonjiru.Id, "SERVING", 1m), new MealRecordItemInput(null, s.Golden.Carrot.Id, s.Golden.CarrotUnitId, "UNIT", 1m)]));
        // メニューを書き換えると、コピーは現在の内容で再計算される
        await fixture.Client.PutAsJsonAsync($"/api/menus/{s.Tonjiru.Id}", new MenuInput(
            "豚汁", null, null, 2400m, 8m, "お椀", [new MenuItemInput(s.Golden.Pork.Id, null, 800m)]));

        var res = await CopyAsync(Yesterday, Day);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var result = (await res.Content.ReadFromJsonAsync<MealRecordCopyResultDto>())!;
        Assert.Empty(result.Skipped);
        Assert.Equal(["lunch", "dinner"], result.Records.Select(r => r.MealType).ToList());
        Assert.All(result.Records, r => Assert.Equal(Day, r.EatenOn));
        Assert.Equal("お弁当", result.Records[0].Memo);
        var soup = result.Records[1].Items[0];
        Assert.Equal((1m, 0.125m), (soup.InputValue, soup.PortionRatio));
        Assert.Equal(20m, Nutrient(soup, "PROT").Amount); // 現在のメニュー（豚こま肉 800g）× 0.125。元の記録の 11.4175 ではない
        var carrot = result.Records[1].Items[1];
        Assert.Equal((150m, s.Golden.CarrotUnitId), (carrot.AmountG, (long)(await fixture.WithDbAsync(db => db.MealRecordItems.AsNoTracking().SingleAsync(i => i.Id == carrot.Id))).UnitId!));
        Assert.Equal(2, (await GetDayAsync(Yesterday)).Records.Count); // コピー元はそのまま
        Assert.Equal(2, (await GetDayAsync(Day)).Records.Count);
    }

    [Fact]
    public async Task Copy_TargetDateHasRecords_Returns409UnlessAllowExisting()
    {
        var s = await SeedAsync();
        await CreateAsync(new MealRecordInput(Yesterday, "dinner", null, [MenuItem(s.Tonjiru.Id, "SERVING", 1m)]));
        await CreateAsync(new MealRecordInput(Day, "breakfast", null, [MenuItem(s.Tonjiru.Id, "RATIO", 0.1m)]));

        var conflict = await CopyAsync(Yesterday, Day);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(new ErrorDetail("toDate", "TARGET_DATE_HAS_RECORDS"), (await conflict.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single());
        Assert.Single((await GetDayAsync(Day)).Records); // 何も作られない

        var allowed = await CopyAsync(Yesterday, Day, allowExisting: true);

        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal(["breakfast", "dinner"], (await GetDayAsync(Day)).Records.Select(r => r.MealType).ToList()); // 既存は残る
    }

    [Fact]
    public async Task Copy_SkipsDeletedMenusAndInactiveFoods_AndReportsThem()
    {
        var s = await SeedAsync();
        await CreateAsync(new MealRecordInput(Yesterday, "dinner", null,
        [
            MenuItem(s.Tonjiru.Id, "SERVING", 1m),
            new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 100m),
            new MealRecordItemInput(null, s.Golden.Miso.Id, null, "GRAM", 18m),
        ]));
        await CreateAsync(new MealRecordInput(Yesterday, "snack", null, [new MealRecordItemInput(null, s.Golden.Pork.Id, null, "GRAM", 50m)]));
        await fixture.Client.DeleteAsync($"/api/menus/{s.Tonjiru.Id}");
        await fixture.WithDbAsync(async db =>
        {
            (await db.Foods.SingleAsync(f => f.Id == s.Golden.Pork.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var res = await CopyAsync(Yesterday, Day);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var result = (await res.Content.ReadFromJsonAsync<MealRecordCopyResultDto>())!;
        Assert.Equal(
            [new SkippedItemDto(s.Tonjiru.Id, null, "MENU_DELETED"), new SkippedItemDto(null, s.Golden.Pork.Id, "FOOD_INACTIVE"), new SkippedItemDto(null, s.Golden.Pork.Id, "FOOD_INACTIVE")],
            result.Skipped);
        var record = Assert.Single(result.Records); // 全項目がスキップされた間食の記録は作らない
        Assert.Equal("dinner", record.MealType);
        Assert.Equal(["みそ"], record.Items.Select(i => i.FoodName).ToList());
    }

    [Fact]
    public async Task Copy_InvalidRequest_Returns400()
    {
        var s = await SeedAsync();
        await CreateAsync(new MealRecordInput(Yesterday, "dinner", null, [MenuItem(s.Tonjiru.Id, "SERVING", 1m)]));

        Assert.Equal(HttpStatusCode.BadRequest, (await CopyAsync(null, Day)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CopyAsync(Yesterday, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CopyAsync(Yesterday, Yesterday)).StatusCode); // 同じ日
        var empty = await CopyAsync(Day.AddDays(-5), Day); // コピー元に記録なし
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("fromDate", (await empty.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Single().Field);
        Assert.Empty((await GetDayAsync(Day)).Records);
    }
}
