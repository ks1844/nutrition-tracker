using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Menus;

/// <summary>
/// メニュー API（API-06〜API-12、docs/09.API仕様.md §5）のテストです。食材と数値は docs/03.栄養計算仕様.md §9 の正解データ（テスト用のダミー値）です。
/// </summary>
[Collection(ApiCollection.Name)]
public class MenusApiTests(ApiFixture fixture) : IAsyncLifetime
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
    /// メニュー「豚汁」の入力を作ります（にんじん 2 本・豚こま肉 400g・みそ大さじ 4。完成後 2400g、8 杯分、お椀）。
    /// </summary>
    /// <param name="g">正解データの 3 食材。</param>
    /// <param name="name">メニューの名前。</param>
    /// <returns>メニューの入力。</returns>
    private static MenuInput Tonjiru(TestData.Golden g, string name = "豚汁") => new(
        name, new DateOnly(2026, 10, 14), null, 2400m, 8m, "お椀",
        [
            new MenuItemInput(g.Carrot.Id, g.CarrotUnitId, 2m),
            new MenuItemInput(g.Pork.Id, null, 400m),
            new MenuItemInput(g.Miso.Id, g.MisoUnitId, 4m),
        ]);

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
    /// メニュー作成 API を呼びます。
    /// </summary>
    /// <param name="input">メニューの入力。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> PostAsync(MenuInput input) => await fixture.Client.PostAsJsonAsync("/api/menus", input);

    /// <summary>
    /// メニュー全体の栄養量から、指定した栄養素を取り出します。
    /// </summary>
    /// <param name="m">メニューの詳細。</param>
    /// <param name="code">栄養素コード。</param>
    /// <returns>その栄養素の量。</returns>
    private static NutrientAmountDto Nutrient(MenuDetailDto m, string code) => m.Nutrients.Single(n => n.Code == code);

    // ---- API-07 作成 ----
    /// <summary>
    /// メニューを作ると、単位入力が重さに換算され（にんじん 2 本 = 300g、みそ大さじ 4 = 72g）、メニュー全体の栄養量が返ることを確認します。タンパク質 91.34g、ビタミン A 1930μg、ビタミン C 31mg（みそが未測定のため未測定の印あり）です（AC-MENU-001、AC-CALC-004、008）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 1 杯の重さ（2400g ÷ 8 杯 = 300g）と 1 杯あたりの栄養量（タンパク質 11.4175g）が返り、記録がないので残りは全量（8 杯）であることを確認します（AC-MENU-004、AC-CALC-005）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// g で入力した食材は、数量と重さが同じ値（400g）でデータベースに保存され、メニュー食材が 3 行できることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 1 杯の呼び方を省略すると「お椀」になり、完成後の重さと杯数が小数 1 桁に四捨五入される（2400.05 → 2400.1、8.04 → 8.0）ことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 名前が空白、重さと杯数が 0、1 杯の呼び方が長すぎる、食材が 0 件のとき、項目別の 400 になり、メニューは保存されないことを確認します（AC-MENU-009）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 必須の項目（名前・完成後の重さ・杯数・食材）がすべて抜けているとき、それぞれが 400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Create_MissingFields_Returns400()
    {
        var res = await PostAsync(new MenuInput(null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var fields = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!.Details.Select(d => d.Field).ToList();
        Assert.Equal(["name", "cookedTotalWeightG", "servings", "items"], fields);
    }

    /// <summary>
    /// 食材の不正（存在しない食材、数量が 0 以下、その食材にない単位）が、何番目の食材かを示す項目名（items[n].…）つきの 400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 無効な食材を使うと、409 の CONFLICT になり、details に理由コード FOOD_INACTIVE が入ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// メニュー詳細（GET /api/menus/{id}）が、作成時の内容と同じ（名前・食材・栄養量）で返ることを確認します（API-08）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 存在しないメニュー、または削除済みのメニューの詳細が 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// メニューを指定の割合だけ食べた記録を、データベースに直接作ります（残りの計算や、記録が残ることの確認に使います）。
    /// </summary>
    /// <param name="menuId">メニューの ID。</param>
    /// <param name="ratio">食べた割合。</param>
    /// <returns>完了を表すタスク。</returns>
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

    /// <summary>
    /// 残りが「1 − 記録した割合の合計」になることを確認します。1.5 杯（0.1875）と 1 杯（0.125）を食べた豚汁は、残り 0.6875（5.5 杯分）です（AC-MENU-008、AC-CALC-013）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 記録が合計 100% を超えると、残りが負になり、食べすぎの印（isOver）が立つことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// メニュー一覧（GET /api/menus）が、削除していないメニューだけを新しい順に、1 杯の重さと残りつきで返すことを確認します（API-06）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// メニューがないとき、一覧が空の配列で返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task List_Empty_ReturnsEmptyArray()
    {
        var list = (await fixture.Client.GetFromJsonAsync<List<MenuSummaryDto>>("/api/menus"))!;

        Assert.Empty(list);
    }

    // ---- API-12 プレビュー ----
    /// <summary>
    /// メニュー プレビュー API を呼びます。
    /// </summary>
    /// <param name="input">メニューの入力。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> PreviewAsync(MenuInput input) => await fixture.Client.PostAsJsonAsync("/api/menus/preview", input);

    /// <summary>
    /// プレビュー（POST /api/menus/preview）が、保存せずに合計の栄養量（作成時と同じ値）と 1 杯あたりの栄養量を返し、データベースにメニューが作られないことを確認します（API-12、AC-MENU-002）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// プレビューが食材ごとの重さ・可食部の重さ・栄養量を返すことを確認します。にんじんは 300g、可食部 270g（廃棄率 10%）、タンパク質 2.7g です。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 名前・完成後の重さ・杯数を省略しても受け付けられ、1 杯の重さと 1 杯あたりの栄養量が null になる（全体の栄養量は返る）ことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 無効な食材は、プレビューでは 409 にならず、警告（FOOD_INACTIVE）として返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// プレビューでも、不正な入力（完成後の重さが 0、存在しない食材）は項目別の 400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// メニュー更新 API を呼びます。
    /// </summary>
    /// <param name="id">メニューの ID。</param>
    /// <param name="input">更新後のメニューの入力。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> PutAsync(long id, MenuInput input) => await fixture.Client.PutAsJsonAsync($"/api/menus/{id}", input);

    /// <summary>
    /// メニューの更新（PUT /api/menus/{id}）が、名前・メモ・杯数・食材をすべて置き換え、栄養量と 1 杯の重さを計算し直すことを確認します。古い食材の行は消え、メニューの数は増えません（API-09）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// メニューを更新しても、すでにある記録の栄養量（スナップショット）が変わらず、記録も残ることを確認します（AC-MENU-005）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 不正な入力での更新が項目別の 400 になり、元のメニューが変わらないことを確認します（AC-MENU-009）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 無効な食材を含めて更新すると 409 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 存在しないメニュー、または削除済みのメニューの更新が 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    // ---- API-10 削除 ----
    /// <summary>
    /// メニューの削除（DELETE /api/menus/{id}）が 204 で、論理削除（行は残り削除日時が入る）であり、一覧と詳細には出なくなり、記録済みの食事記録は残ることを確認します（API-10、AC-MENU-007）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task AC_MENU_007_Delete_IsLogical_KeepsRecords_AndHidesMenu()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        var other = (await (await PostAsync(Tonjiru(g, "別の汁"))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await AddRecordedPortionAsync(created.Id, 0.125m);

        var res = await fixture.Client.DeleteAsync($"/api/menus/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var list = (await fixture.Client.GetFromJsonAsync<List<MenuSummaryDto>>("/api/menus"))!;
        Assert.Equal([other.Id], list.Select(m => m.Id).ToList()); // 一覧に出ない
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.GetAsync($"/api/menus/{created.Id}")).StatusCode);
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync(i => i.MenuId == created.Id))); // 記録は残る
        var row = await fixture.WithDbAsync(db => db.Menus.AsNoTracking().SingleAsync(m => m.Id == created.Id));
        Assert.NotNull(row.DeletedAt); // 行は物理削除されない
        Assert.Equal(3, await fixture.WithDbAsync(db => db.MenuItems.CountAsync(i => i.MenuId == created.Id)));
    }

    /// <summary>
    /// 存在しないメニュー、または削除済みのメニューの削除が 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Delete_MissingOrAlreadyDeleted_Returns404()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await fixture.Client.DeleteAsync($"/api/menus/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync($"/api/menus/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync("/api/menus/999999")).StatusCode);
    }

    // ---- API-11 複製 ----
    /// <summary>
    /// メニューの複製（POST /api/menus/{id}/duplicate）が、食材・完成後の重さ・杯数・メモを写した新しいメニュー（名前は「豚汁のコピー」、作った日は今日）を作り、記録は複製しないことを確認します（API-11、AC-MENU-006）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task AC_MENU_006_Duplicate_CopiesMenuWithoutRecords()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g) with { Memo = "白だし少なめ" })).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await AddRecordedPortionAsync(created.Id, 0.125m);

        var res = await fixture.Client.PostAsync($"/api/menus/{created.Id}/duplicate", null);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var copy = (await res.Content.ReadFromJsonAsync<MenuDetailDto>())!;
        Assert.NotEqual(created.Id, copy.Id);
        Assert.Equal($"/api/menus/{copy.Id}", res.Headers.Location!.OriginalString);
        Assert.Equal("豚汁のコピー", copy.Name);
        Assert.Equal(ApiFixture.Today, copy.CookedOn); // 既定は当日
        Assert.Equal(("白だし少なめ", 2400m, 8m, "お椀"), (copy.Memo, copy.CookedTotalWeightG, copy.Servings, copy.ServingLabel));
        Assert.Equal(created.Items.Select(i => (i.FoodId, i.UnitId, i.Quantity, i.AmountG)), copy.Items.Select(i => (i.FoodId, i.UnitId, i.Quantity, i.AmountG)));
        Assert.Equal(created.Nutrients, copy.Nutrients);
        Assert.Equal(new RemainingDto(1.000000m, 8.00m, false), copy.Remaining); // 記録は複製されない
        Assert.Equal(0, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync(i => i.MenuId == copy.Id)));
        Assert.Equal(1, await fixture.WithDbAsync(db => db.MealRecordItems.CountAsync())); // 元の記録はそのまま
    }

    /// <summary>
    /// 複製のときに名前と作った日を指定すると、それが使われる（名前の前後の空白は除く）ことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Duplicate_WithNameAndCookedOn_UsesThem()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        var res = await fixture.Client.PostAsJsonAsync($"/api/menus/{created.Id}/duplicate", new MenuDuplicateInput(" 豚汁（来週） ", new DateOnly(2026, 10, 24)));

        var copy = (await res.Content.ReadFromJsonAsync<MenuDetailDto>())!;
        Assert.Equal(("豚汁（来週）", new DateOnly(2026, 10, 24)), (copy.Name, copy.CookedOn));
    }

    /// <summary>
    /// 複製の名前が空白だと 400 になり、メニューが増えないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Duplicate_BlankName_Returns400()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;

        var res = await fixture.Client.PostAsJsonAsync($"/api/menus/{created.Id}/duplicate", new MenuDuplicateInput("  ", null));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(1, await fixture.WithDbAsync(db => db.Menus.CountAsync()));
    }

    /// <summary>
    /// 存在しないメニュー、または削除済みのメニューの複製が 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Duplicate_MissingOrDeletedMenu_Returns404()
    {
        var g = await SeedAsync();
        var created = (await (await PostAsync(Tonjiru(g))).Content.ReadFromJsonAsync<MenuDetailDto>())!;
        await fixture.Client.DeleteAsync($"/api/menus/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsync($"/api/menus/{created.Id}/duplicate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsync("/api/menus/999999/duplicate", null)).StatusCode);
    }
}
