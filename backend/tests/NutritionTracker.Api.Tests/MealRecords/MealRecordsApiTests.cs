using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.MealRecords;

/// <summary>
/// 食事記録 API（API-13〜16、21、docs/09.API仕様.md §6）のテストです。数値は docs/03.栄養計算仕様.md §9 の正解データ（テスト用のダミー値）です。
/// </summary>
[Collection(ApiCollection.Name)]
public class MealRecordsApiTests(ApiFixture fixture) : IAsyncLifetime
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
    /// テストで記録する日（テスト中の今日）。
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
        var res = await fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput(
            "豚汁", new DateOnly(2026, 10, 14), null, 2400m, 8m, "お椀",
            [
                new MenuItemInput(g.Carrot.Id, g.CarrotUnitId, 2m),
                new MenuItemInput(g.Pork.Id, null, 400m),
                new MenuItemInput(g.Miso.Id, g.MisoUnitId, 4m),
            ]));
        return new Seeded(g, (await res.Content.ReadFromJsonAsync<MenuDetailDto>())!);
    }

    /// <summary>
    /// 夕食の記録の入力を作ります（日付はテスト中の今日）。
    /// </summary>
    /// <param name="items">食べたもの。</param>
    /// <returns>食事記録の入力。</returns>
    private static MealRecordInput Dinner(params MealRecordItemInput[] items) => new(Day, "dinner", null, items);

    /// <summary>
    /// メニューを食べた項目の入力を作ります。
    /// </summary>
    /// <param name="menuId">メニューの ID。</param>
    /// <param name="mode">入力方式（RATIO / SERVING / GRAM）。</param>
    /// <param name="value">入力値。</param>
    /// <returns>記録項目の入力。</returns>
    private static MealRecordItemInput MenuItem(long menuId, string mode, decimal value) => new(menuId, null, null, mode, value);

    /// <summary>
    /// 食事記録の作成 API を呼びます。
    /// </summary>
    /// <param name="input">食事記録の入力。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> PostAsync(MealRecordInput input) => await fixture.Client.PostAsJsonAsync("/api/meal-records", input);

    /// <summary>
    /// 食事記録を作成し、201 で成功したことを確かめて結果を返します。
    /// </summary>
    /// <param name="input">食事記録の入力。</param>
    /// <returns>作った記録（警告つき）。</returns>
    private async Task<MealRecordSaveResultDto> CreateAsync(MealRecordInput input)
    {
        var res = await PostAsync(input);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<MealRecordSaveResultDto>())!;
    }

    /// <summary>
    /// 記録項目の栄養量から、指定した栄養素を取り出します。
    /// </summary>
    /// <param name="item">記録項目。</param>
    /// <param name="code">栄養素コード。</param>
    /// <returns>その栄養素の量。</returns>
    private static NutrientAmountDto Nutrient(MealRecordItemDto item, string code) => item.Nutrients.Single(n => n.Code == code);

    // ---- API-14 作成（メニュー） ----
    /// <summary>
    /// メニューを入力方式（割合・杯数・g）ごとに記録すると、食べた割合が決まり、栄養量（タンパク質）が期待どおり保存されることを確認します。0.25 割合は 22.835g、1.5 杯は 17.1263g（17.12625 を四捨五入）、300g は 11.4175g、800g は 0.333333 で 30.4466g です。ビタミン C は未測定の印が引き継がれ、g 入力のときは食べた重さも保存されます（AC-REC-001〜003、AC-CALC-005〜007、009）。
    /// </summary>
    /// <param name="mode">入力方式。</param>
    /// <param name="value">入力値。</param>
    /// <param name="ratio">期待する食べた割合。</param>
    /// <param name="prot">期待するタンパク質の量（g）。</param>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 記録を保存すると、栄養素の数（24）だけ栄養量の行が縦持ちで保存され、メモと食事区分も保存されることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// 単品の食材を g で記録すると、食べた割合は使わず重さから栄養量が決まることを確認します。にんじん 100g はタンパク質 0.9g・ビタミン C 9mg・ビタミン A 630μg、みそ 18g はタンパク質 2.16g でビタミン C は未測定です（AC-REC-004、AC-CALC-011）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 単品の食材を単位で記録すると、重さに換算されて保存される（にんじん 1 本 = 150g、タンパク質 1.35g）ことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task AC_REC_004_CreateFoodRecord_Unit_ConvertsToGrams()
    {
        var s = await SeedAsync();

        var created = await CreateAsync(Dinner(new MealRecordItemInput(null, s.Golden.Carrot.Id, s.Golden.CarrotUnitId, "UNIT", 1m)));

        var item = Assert.Single(created.Items);
        Assert.Equal(150m, item.AmountG); // 1 本 = 150g
        Assert.Equal(("UNIT", 1m), (item.InputMode, item.InputValue));
        Assert.Equal(s.Golden.CarrotUnitId, item.UnitId); // 編集画面で入力を再表示するための単位 ID
        Assert.Equal(1.35m, Nutrient(item, "PROT").Amount); // 150g × 0.9 ÷ 100 × 1.0
        var stored = await fixture.WithDbAsync(db => db.MealRecordItems.AsNoTracking().SingleAsync());
        Assert.Equal(s.Golden.CarrotUnitId, stored.UnitId);
    }

    /// <summary>
    /// 同じ夕食で豚汁 1 杯とごはん（豚こま肉 150g）を記録すると、1 件の食事記録に 2 項目が入ることを確認します（AC-REC-005）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
    /// <summary>
    /// 豚汁の記録が合計 100% を超えたとき（0.9 の後に 0.2 を記録）、保存されたうえで警告（MENU_OVER_100PCT、合計 1.1）が返ることを確認します（AC-REC-008）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 合計がちょうど 100% のときは警告が出ないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task ExactlyHundredPercent_HasNoWarning()
    {
        var s = await SeedAsync();
        await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 0.5m)));

        var second = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 4m)));

        Assert.Empty(second.Warnings);
    }

    // ---- 入力エラー ----
    /// <summary>
    /// 食事日がない、食事区分が不正、項目が 0 件のとき、項目別の 400 になり、何も保存されないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 記録項目の不正（メニューと食材の両方を指定、どちらも指定しない、メニューに UNIT を指定、入力値が 0、単位の指定がない、他の食材の単位、存在しないメニューや食材）が、何番目の項目かを示す項目名つきの 400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 削除済みのメニューは新しく記録できず、400（menuId の項目）になることを確認します（AC-MENU-007）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 無効な食材を記録しようとすると 409（FOOD_INACTIVE）になり、何も保存されないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 丸めると食べた割合が 0 になるほど小さい値（0.0005g）が、データベースの制約違反（500）ではなく 400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Create_TooSmallAmount_Returns400_NotServerError()
    {
        var s = await SeedAsync();

        var res = await PostAsync(Dinner(MenuItem(s.Tonjiru.Id, "GRAM", 0.0005m))); // 割合が 6 桁で 0 になる

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---- API-13 日別取得 ----
    /// <summary>
    /// 日別の食事記録 API を呼びます。
    /// </summary>
    /// <param name="date">取得する日。</param>
    /// <returns>その日の食事記録。</returns>
    private async Task<MealRecordDayDto> GetDayAsync(DateOnly date) =>
        (await fixture.Client.GetFromJsonAsync<MealRecordDayDto>($"/api/meal-records?date={date:yyyy-MM-dd}"))!;

    /// <summary>
    /// 日別の取得（GET /api/meal-records）が、その日の記録だけを朝・昼・夕・間食の順に返し、メニュー名・食材名・栄養量（24 栄養素）が入ることを確認します（API-13、AC-REC-005）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 記録のない日の取得が、空の一覧で返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task GetByDate_NoRecords_ReturnsEmptyList()
    {
        var day = await GetDayAsync(Day);

        Assert.Empty(day.Records);
    }

    /// <summary>
    /// メニューを後から書き換えても、取得される栄養量は記録したときの値（11.4175g）のままであることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 日付がない、または日付として読めないとき、400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task GetByDate_MissingOrInvalidDate_Returns400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/meal-records")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync("/api/meal-records?date=2026-13-40")).StatusCode);
    }

    // ---- API-15 更新 ----
    /// <summary>
    /// 食事記録の更新 API を呼びます。
    /// </summary>
    /// <param name="id">食事記録の ID。</param>
    /// <param name="input">更新後の食事記録の入力。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> PutAsync(long id, MealRecordInput input) => await fixture.Client.PutAsJsonAsync($"/api/meal-records/{id}", input);

    /// <summary>
    /// 更新（PUT /api/meal-records/{id}）で、入力が変わった項目は現在のメニューの内容で栄養量が計算し直され（古い値は残らない）、入力が同じ項目は同じ ID・同じ栄養量のまま残ることを確認します（API-15、AC-REC-006）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 更新で、食事日・食事区分・メモが変わり、リクエストから外した項目が消える（元の日の記録も移る）ことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// メニューが削除済みでも、入力が変わらない既存の項目は更新後も残せるが、削除済みメニューを新しく加えることはできない（400）ことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 不正な入力での更新が 400 になり、元の記録が変わらないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 存在しない食事記録の更新が 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Update_MissingRecord_Returns404()
    {
        var s = await SeedAsync();

        var res = await PutAsync(999_999, Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    /// <summary>
    /// 更新後にメニューの記録が合計 100% を超えたとき、警告が返ることを確認します。古い記録分（0.5）は合計に含まれず、置き換えた後の 1.2 になります。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Update_OverHundredPercent_ReturnsWarning()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 0.5m)));

        var updated = (await (await PutAsync(created.Id, Dinner(MenuItem(s.Tonjiru.Id, "RATIO", 1.2m)))).Content.ReadFromJsonAsync<MealRecordSaveResultDto>())!;

        Assert.Equal(1.2m, Assert.Single(updated.Warnings).TotalPortionRatio); // 置き換えた後の合計（古い 0.5 は含まない）
    }

    // ---- API-16 削除 ----
    /// <summary>
    /// 削除（DELETE /api/meal-records/{id}）が 204 で、記録・項目・栄養量が消え、メニューの残りに食べた分（1.5 杯）が戻ることを確認します（API-16、AC-REC-007）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 存在しない食事記録の削除が 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Delete_MissingRecord_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync("/api/meal-records/999999")).StatusCode);
    }

    /// <summary>
    /// 同じ記録を 2 回削除すると、2 回目は 404 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Delete_Twice_SecondReturns404()
    {
        var s = await SeedAsync();
        var created = await CreateAsync(Dinner(MenuItem(s.Tonjiru.Id, "SERVING", 1m)));
        await fixture.Client.DeleteAsync($"/api/meal-records/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.DeleteAsync($"/api/meal-records/{created.Id}")).StatusCode);
    }

    // ---- API-21 コピー ----
    /// <summary>
    /// コピーのテストで、コピー元にする日（テスト中の今日の前日）。
    /// </summary>
    private static readonly DateOnly Yesterday = Day.AddDays(-1);

    /// <summary>
    /// 食事記録のコピー API を呼びます。
    /// </summary>
    /// <param name="from">コピー元の日。</param>
    /// <param name="to">コピー先の日。</param>
    /// <param name="allowExisting">コピー先に記録があってもコピーするか。</param>
    /// <returns>API の応答。</returns>
    private async Task<HttpResponseMessage> CopyAsync(DateOnly? from, DateOnly? to, bool? allowExisting = null) =>
        await fixture.Client.PostAsJsonAsync("/api/meal-records/copy", new MealRecordCopyInput(from, to, allowExisting));

    /// <summary>
    /// コピー（POST /api/meal-records/copy）が、元の日の食事（区分・メモ・項目）をコピー先に作り、現在のメニューの内容で栄養量を計算し直す（元の記録の値ではない）こと、コピー元は変わらないことを確認します（API-21、AC-REC-010）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// コピー先にすでに記録があるとき、許可がなければ 409（TARGET_DATE_HAS_RECORDS）で何も作られず、許可すれば既存の記録を残したままコピーされることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// 削除済みのメニューと無効な食材がスキップされて理由（MENU_DELETED、FOOD_INACTIVE）が返り、全項目がスキップされた食事は作られないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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

    /// <summary>
    /// コピー元・コピー先の日付がない、同じ日、コピー元に記録がない、のいずれも 400 になることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
