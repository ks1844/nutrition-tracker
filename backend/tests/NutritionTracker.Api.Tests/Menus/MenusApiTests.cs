using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Menus;

/// <summary>メニュー API（API-06〜API-12、docs/09.API仕様.md §5）。食材・数値は docs/03.栄養計算仕様.md §9 の正解データ（テスト用ダミー値）。</summary>
[Collection(ApiCollection.Name)]
public class MenusApiTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>『豚汁』: にんじん 2 本、豚こま肉 400g、みそ 大さじ 4。完成後重量 2400g、8 杯分（お椀）</summary>
    private static MenuInput Tonjiru(TestData.Golden g, string name = "豚汁") => new(
        name, new DateOnly(2026, 10, 14), null, 2400m, 8m, "お椀",
        [
            new MenuItemInput(g.Carrot.Id, g.CarrotUnitId, 2m),
            new MenuItemInput(g.Pork.Id, null, 400m),
            new MenuItemInput(g.Miso.Id, g.MisoUnitId, 4m),
        ]);

    private async Task<TestData.Golden> SeedAsync()
    {
        TestData.Golden? golden = null;
        await fixture.WithDbAsync(async db => golden = await TestData.AddGoldenAsync(db));
        return golden!;
    }

    private async Task<HttpResponseMessage> PostAsync(MenuInput input) => await fixture.Client.PostAsJsonAsync("/api/menus", input);

    private static NutrientAmountDto Nutrient(MenuDetailDto m, string code) => m.Nutrients.Single(n => n.Code == code);

    // ---- API-07 作成 ----
    [Fact]
    public async Task AC_MENU_001_Create_ConvertsUnitsToGrams_AndReturnsTotals()
    {
        var g = await SeedAsync();

        var res = await PostAsync(Tonjiru(g));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var m = (await res.Content.ReadFromJsonAsync<MenuDetailDto>())!;
        Assert.Equal($"/api/menus/{m.Id}", res.Headers.Location!.OriginalString);
        Assert.Equal([300m, 400m, 72m], m.Items.Select(i => i.AmountG).ToList()); // AC-CALC-008: 2 本=300g、大さじ 4=72g
        Assert.Equal(["にんじん", "豚こま肉", "みそ"], m.Items.Select(i => i.FoodName).ToList());
        Assert.Equal(["本", null, "大さじ"], m.Items.Select(i => i.UnitName).ToList());
        // AC-CALC-004: メニュー合計
        Assert.Equal(new NutrientAmountDto("PROT", 91.34m, false), Nutrient(m, "PROT"));
        Assert.Equal(new NutrientAmountDto("VITC", 31m, true), Nutrient(m, "VITC")); // みそが未測定 → 不完全
        Assert.Equal(new NutrientAmountDto("VITA_RAE", 1930m, false), Nutrient(m, "VITA_RAE"));
    }

    [Fact]
    public async Task AC_MENU_004_Create_ReturnsServingWeightAndPerServingNutrients()
    {
        var g = await SeedAsync();

        var m = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        Assert.Equal(300.0m, m.ServingWeightG); // 2400 ÷ 8
        Assert.Equal(new NutrientAmountDto("PROT", 11.4175m, false), m.PerServing.Single(n => n.Code == "PROT")); // AC-CALC-005
        Assert.True(m.PerServing.Single(n => n.Code == "VITC").IsIncomplete);
        Assert.Equal(new RemainingDto(1.000000m, 8.00m, false), m.Remaining); // 記録がなければ全量が残っている
    }

    [Fact]
    public async Task Create_GramInput_IsStoredAsEnteredAmount()
    {
        var g = await SeedAsync();
        var m = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        var stored = await fixture.WithDbAsync(db => db.MenuItems.AsNoTracking().OrderBy(i => i.SortOrder).ToListAsync());

        Assert.Equal(3, stored.Count);
        Assert.All(stored, i => Assert.Equal(m.Id, i.MenuId));
        Assert.Null(stored[1].UnitId);
        Assert.Equal((400m, 400m), (stored[1].Quantity, stored[1].AmountG)); // g 入力は数量 = 重量
    }

    [Fact]
    public async Task Create_Defaults_ServingLabel_AndRoundsInputs()
    {
        var g = await SeedAsync();
        var input = Tonjiru(g) with { ServingLabel = null, CookedTotalWeightG = 2400.05m, Servings = 8.04m };

        var m = (await (await PostAsync(input)).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        Assert.Equal("お椀", m.ServingLabel);
        Assert.Equal(2400.1m, m.CookedTotalWeightG); // 小数 1 桁（HALF_UP）
        Assert.Equal(8.0m, m.Servings);
    }

    [Fact]
    public async Task AC_MENU_009_Create_InvalidInput_Returns400WithFieldDetails()
    {
        var res = await PostAsync(new MenuInput("  ", null, null, 0m, 0m, new string('あ', 21), []));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.Equal("VALIDATION_ERROR", err.Code);
        Assert.Equal(["name", "cookedTotalWeightG", "servings", "servingLabel", "items"], err.Details.Select(d => d.Field).ToList());
        Assert.Equal(0, await fixture.WithDbAsync(db => db.Menus.CountAsync())); // 保存されない
    }

    [Fact]
    public async Task Create_MissingFields_Returns400()
    {
        var res = await PostAsync(new MenuInput(null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["name", "cookedTotalWeightG", "servings", "items"], fields);
    }

    [Fact]
    public async Task Create_InvalidItems_Return400WithIndexedFields()
    {
        var g = await SeedAsync();
        var input = Tonjiru(g) with
        {
            Items =
            [
                new MenuItemInput(999999, null, 100m), // 存在しない食材
                new MenuItemInput(g.Pork.Id, null, 0m), // 数量 0
                new MenuItemInput(g.Pork.Id, g.CarrotUnitId, 1m), // 他の食材の単位
            ],
        };

        var res = await PostAsync(input);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["items[0].foodId", "items[1].quantity", "items[2].unitId"], fields);
    }

    [Fact]
    public async Task Create_InactiveFood_Returns409WithReasonCode()
    {
        var g = await SeedAsync();
        await fixture.WithDbAsync(async db =>
        {
            var food = await db.Foods.SingleAsync(f => f.Id == g.Pork.Id);
            food.IsActive = false;
            await db.SaveChangesAsync();
        });

        var res = await PostAsync(Tonjiru(g));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var err = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.Equal("CONFLICT", err.Code);
        Assert.Equal(new ErrorDetail("items[1].foodId", "FOOD_INACTIVE"), err.Details.Single());
    }

    // ---- API-08 詳細 ----
    [Fact]
    public async Task Get_ReturnsSameContentAsCreated()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        var got = (await fixture.Client.GetFromJsonAsync<MenuDetailDto>($"/api/menus/{created.Id}"))!;

        Assert.Equal(created.Items, got.Items);
        Assert.Equal(created.Nutrients, got.Nutrients);
        Assert.Equal(created.PerServing, got.PerServing);
        Assert.Equal(("豚汁", new DateOnly(2026, 10, 14), 2400.0m, 8.0m), (got.Name, got.CookedOn, got.CookedTotalWeightG, got.Servings));
    }

    [Fact]
    public async Task Get_UnknownOrDeletedMenu_Returns404()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await fixture.WithDbAsync(async db =>
        {
            var menu = await db.Menus.SingleAsync(m => m.Id == created.Id);
            menu.DeletedAt = DateTime.UtcNow; // 論理削除
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/menus/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync("/api/menus/999999")).StatusCode);
    }

    /// <summary>メニューに対する食事記録（食べた割合）を直接 DB に作る。記録 API ができる前でも残量を確認できる。</summary>
    private async Task AddRecordedPortionAsync(long menuId, decimal ratio)
    {
        await fixture.WithDbAsync(async db =>
        {
            var record = new NutritionTracker.Api.Entities.MealRecord { UserId = 1, EatenOn = ApiFixture.Today, MealType = "dinner" };
            record.Items.Add(new NutritionTracker.Api.Entities.MealRecordItem
            {
                MenuId = menuId, InputMode = "RATIO", InputValue = ratio, PortionRatio = ratio,
            });
            db.MealRecords.Add(record);
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task AC_MENU_008_Remaining_IsOneMinusRecordedRatios()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await AddRecordedPortionAsync(created.Id, 0.1875m); // 1.5 杯
        await AddRecordedPortionAsync(created.Id, 0.125m); // 1 杯

        var got = (await fixture.Client.GetFromJsonAsync<MenuDetailDto>($"/api/menus/{created.Id}"))!;

        Assert.Equal(new RemainingDto(0.6875m, 5.5m, false), got.Remaining); // AC-CALC-013
    }

    [Fact]
    public async Task Remaining_IsOver_WhenRecordedMoreThanWhole()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await AddRecordedPortionAsync(created.Id, 0.75m);
        await AddRecordedPortionAsync(created.Id, 0.5m);

        var got = (await fixture.Client.GetFromJsonAsync<MenuDetailDto>($"/api/menus/{created.Id}"))!;

        Assert.True(got.Remaining.IsOver);
        Assert.Equal(-0.25m, got.Remaining.Ratio);
    }

    // ---- API-06 一覧 ----
    [Fact]
    public async Task List_ReturnsActiveMenus_NewestFirst_WithRemaining()
    {
        var g = await SeedAsync();
        var first = (await (await PostAsync(Tonjiru(g, "豚汁"))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        var second = (await (await PostAsync(Tonjiru(g, "豚汁（薄味）"))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        var deleted = (await (await PostAsync(Tonjiru(g, "削除する汁"))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await fixture.WithDbAsync(async db =>
        {
            (await db.Menus.SingleAsync(m => m.Id == deleted.Id)).DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        await AddRecordedPortionAsync(first.Id, 0.1875m);
        await AddRecordedPortionAsync(first.Id, 0.125m);

        var list = (await fixture.Client.GetFromJsonAsync<List<MenuSummaryDto>>("/api/menus"))!;

        Assert.Equal(["豚汁（薄味）", "豚汁"], list.Select(m => m.Name).ToList()); // 論理削除は出ない。新しい順
        Assert.Equal(new RemainingDto(1.000000m, 8.00m, false), list[0].Remaining);
        Assert.Equal(new RemainingDto(0.6875m, 5.5m, false), list[1].Remaining); // AC-MENU-008
        Assert.Equal((300.0m, 8.0m, "お椀"), (list[1].ServingWeightG, list[1].Servings, list[1].ServingLabel));
        Assert.Equal(second.Id, list[0].Id);
    }

    [Fact]
    public async Task List_Empty_ReturnsEmptyArray()
    {
        var list = (await fixture.Client.GetFromJsonAsync<List<MenuSummaryDto>>("/api/menus"))!;

        Assert.Empty(list);
    }

    // ---- API-12 プレビュー ----
    private async Task<HttpResponseMessage> PreviewAsync(MenuInput input) => await fixture.Client.PostAsJsonAsync("/api/menus/preview", input);

    [Fact]
    public async Task AC_MENU_002_Preview_ReturnsTotals_WithoutWritingToDb()
    {
        var g = await SeedAsync();

        var res = await PreviewAsync(Tonjiru(g));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var p = (await res.Content.ReadFromJsonAsync<MenuPreviewDto>())!;
        Assert.Equal(new NutrientAmountDto("PROT", 91.34m, false), p.Nutrients.Single(n => n.Code == "PROT")); // AC-CALC-004 と一致
        Assert.Equal(new NutrientAmountDto("VITC", 31m, true), p.Nutrients.Single(n => n.Code == "VITC"));
        Assert.Equal(new NutrientAmountDto("VITA_RAE", 1930m, false), p.Nutrients.Single(n => n.Code == "VITA_RAE"));
        Assert.Equal(300.0m, p.ServingWeightG);
        Assert.Equal(new NutrientAmountDto("PROT", 11.4175m, false), p.PerServing!.Single(n => n.Code == "PROT"));
        Assert.Empty(p.Warnings);
        Assert.Equal(0, await fixture.WithDbAsync(db => db.Menus.CountAsync())); // DB は更新しない
        Assert.Equal(0, await fixture.WithDbAsync(db => db.MenuItems.CountAsync()));
    }

    [Fact]
    public async Task Preview_ReturnsPerItemAmountEdibleAndNutrients()
    {
        var g = await SeedAsync();

        var p = (await (await PreviewAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuPreviewDto>())!;

        Assert.Equal(["にんじん", "豚こま肉", "みそ"], p.Items.Select(i => i.FoodName).ToList());
        Assert.Equal([300m, 400m, 72m], p.Items.Select(i => i.AmountG).ToList());
        Assert.Equal(270m, p.Items[0].EdibleG); // 廃棄率 10%
        Assert.Equal(2.7m, p.Items[0].Nutrients.Single(n => n.Code == "PROT").Amount);
    }

    [Fact]
    public async Task Preview_WithoutCookedWeightAndServings_ReturnsNullPerServing()
    {
        var g = await SeedAsync();
        var input = Tonjiru(g) with { Name = null, CookedTotalWeightG = null, Servings = null };

        var res = await PreviewAsync(input);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var p = (await res.Content.ReadFromJsonAsync<MenuPreviewDto>())!;
        Assert.Null(p.ServingWeightG);
        Assert.Null(p.PerServing);
        Assert.Equal(91.34m, p.Nutrients.Single(n => n.Code == "PROT").Amount);
    }

    [Fact]
    public async Task Preview_InactiveFood_ReturnsWarningInsteadOf409()
    {
        var g = await SeedAsync();
        await fixture.WithDbAsync(async db =>
        {
            (await db.Foods.SingleAsync(f => f.Id == g.Pork.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var res = await PreviewAsync(Tonjiru(g));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var p = (await res.Content.ReadFromJsonAsync<MenuPreviewDto>())!;
        var warning = Assert.Single(p.Warnings);
        Assert.Equal(("FOOD_INACTIVE", g.Pork.Id), (warning.Code, warning.FoodId));
    }

    [Fact]
    public async Task Preview_InvalidInput_Returns400()
    {
        var g = await SeedAsync();
        var input = Tonjiru(g) with { CookedTotalWeightG = 0m, Items = [new MenuItemInput(999_999, null, 10m)] };

        var res = await PreviewAsync(input);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["cookedTotalWeightG", "items[0].foodId"], fields);
    }

    // ---- API-09 更新 ----
    private async Task<HttpResponseMessage> PutAsync(long id, MenuInput input) => await fixture.Client.PutAsJsonAsync($"/api/menus/{id}", input);

    [Fact]
    public async Task Update_ReplacesAllFieldsAndItems_AndRecalculates()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        var input = new MenuInput(
            "豚汁（具だくさん）", new DateOnly(2026, 10, 15), "ねぎを追加", 1200m, 4m, "どんぶり",
            [new MenuItemInput(g.Pork.Id, null, 200m), new MenuItemInput(g.Carrot.Id, g.CarrotUnitId, 1m)]);

        var res = await PutAsync(created.Id, input);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var m = (await res.Content.ReadFromJsonAsync<MenuDetailDto>())!;
        Assert.Equal(created.Id, m.Id);
        Assert.Equal(("豚汁（具だくさん）", "ねぎを追加", "どんぶり"), (m.Name, m.Memo, m.ServingLabel));
        Assert.Equal([200m, 150m], m.Items.Select(i => i.AmountG).ToList());
        Assert.Equal(["豚こま肉", "にんじん"], m.Items.Select(i => i.FoodName).ToList());
        Assert.Equal(300.0m, m.ServingWeightG); // 1200 ÷ 4
        Assert.Equal(2, await fixture.WithDbAsync(db => db.MenuItems.CountAsync(i => i.MenuId == created.Id))); // 古い 3 行は消える
        Assert.Equal(1, await fixture.WithDbAsync(db => db.Menus.CountAsync()));
    }

    [Fact]
    public async Task AC_MENU_005_Update_DoesNotChangeExistingRecordSnapshots()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await AddRecordedPortionAsync(created.Id, 0.125m);
        await fixture.WithDbAsync(async db =>
        {
            var item = await db.MealRecordItems.SingleAsync(i => i.MenuId == created.Id);
            var prot = await db.Nutrients.SingleAsync(n => n.Code == "PROT");
            db.MealRecordItemNutrients.Add(new NutritionTracker.Api.Entities.MealRecordItemNutrient
            {
                MealRecordItemId = item.Id, NutrientId = prot.Id, Amount = 11.4175m, IsIncomplete = false,
            });
            await db.SaveChangesAsync();
        });
        var before = await fixture.WithDbAsync(db => db.MealRecordItemNutrients.AsNoTracking().OrderBy(n => n.NutrientId).ToListAsync());

        var res = await PutAsync(created.Id, Tonjiru(g) with { Items = [new MenuItemInput(g.Pork.Id, null, 100m)] });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var after = await fixture.WithDbAsync(db => db.MealRecordItemNutrients.AsNoTracking().OrderBy(n => n.NutrientId).ToListAsync());
        Assert.Equal(before.Select(n => (n.MealRecordItemId, n.NutrientId, n.Amount, n.IsIncomplete)), after.Select(n => (n.MealRecordItemId, n.NutrientId, n.Amount, n.IsIncomplete)));
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync(i => i.MenuId == created.Id))); // 記録は残る
    }

    [Fact]
    public async Task AC_MENU_009_Update_InvalidInput_Returns400_AndKeepsOriginal()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        var res = await PutAsync(created.Id, new MenuInput(" ", null, null, 2400m, 0m, null, []));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["name", "servings", "items"], fields);
        var current = (await fixture.Client.GetFromJsonAsync<MenuDetailDto>($"/api/menus/{created.Id}"))!;
        Assert.Equal(("豚汁", 3), (current.Name, current.Items.Count)); // 変更されない
    }

    [Fact]
    public async Task Update_InactiveFood_Returns409()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await fixture.WithDbAsync(async db =>
        {
            (await db.Foods.SingleAsync(f => f.Id == g.Miso.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });

        var res = await PutAsync(created.Id, Tonjiru(g));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Update_MissingOrDeletedMenu_Returns404()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await fixture.WithDbAsync(async db =>
        {
            (await db.Menus.SingleAsync(m => m.Id == created.Id)).DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(created.Id, Tonjiru(g))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(999_999, Tonjiru(g))).StatusCode);
    }
}
