using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>
/// ダッシュボード API のテストに共通する部品です。摂取量は、保存済みの栄養量（スナップショット）を直接作って与えます。
/// </summary>
public abstract class DashboardApiTestBase(ApiFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// テスト用の実データベースと Web アプリ。
    /// </summary>
    protected ApiFixture Fixture { get; } = fixture;

    /// <summary>
    /// テストで基準にする日（テスト中の今日）。
    /// </summary>
    protected static readonly DateOnly Day = ApiFixture.Today;

    /// <summary>
    /// 各テストの前に、データを初期状態に戻します。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public Task InitializeAsync() => Fixture.ResetAsync();

    /// <summary>
    /// 各テストの後の片づけはありません。
    /// </summary>
    /// <returns>完了済みのタスク。</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// プロフィール（男性・30 歳・170cm・65kg・活動レベル 2）を登録します。エネルギー目標は 2155 kcal、目的が both ならタンパク質目標は 91.0g です。
    /// </summary>
    /// <param name="goal">目的（既定は both）。</param>
    /// <returns>完了を表すタスク。</returns>
    protected async Task PutProfileAsync(string goal = "both") =>
        Assert.Equal(HttpStatusCode.OK, (await Fixture.Client.PutAsJsonAsync(
            "/api/profile", new ProfileInput("male", new DateOnly(1996, 4, 1), 170.0m, 65.0m, 2, goal, null))).StatusCode);

    /// <summary>
    /// 栄養目標（性別は all、年齢は 0〜150、1 日あたり）をデータベースに登録します。テスト用の仮の値です。
    /// </summary>
    /// <param name="targets">栄養素コード・種類・目標量の組。</param>
    /// <returns>完了を表すタスク。</returns>
    protected async Task AddTargetsAsync(params (string Code, string Kind, decimal Amount)[] targets)
    {
        await Fixture.WithDbAsync(async db =>
        {
            var ids = await db.Nutrients.ToDictionaryAsync(n => n.Code, n => n.Id);
            foreach (var (code, kind, amount) in targets)
            {
                db.NutrientTargets.Add(new NutrientTarget
                {
                    NutrientId = ids[code], Sex = "all", AgeMin = 0, AgeMax = 150, Kind = kind, Amount = amount, Basis = "PER_DAY",
                });
            }
            await db.SaveChangesAsync();
        });
    }

    /// <summary>
    /// 1 食分の記録を直接作ります。単品の食材の 1 項目に、与えた量の栄養量（与えていない栄養素は 0）を持たせます。
    /// </summary>
    /// <param name="date">食べた日。</param>
    /// <param name="mealType">食事区分。</param>
    /// <param name="amounts">栄養素コード → 栄養量。</param>
    /// <param name="incomplete">未測定の印をつける栄養素コード。</param>
    /// <returns>完了を表すタスク。</returns>
    protected async Task AddIntakeAsync(DateOnly date, string mealType, Dictionary<string, decimal> amounts, params string[] incomplete)
    {
        await Fixture.WithDbAsync(async db =>
        {
            var food = await db.Foods.FirstOrDefaultAsync() ?? await TestData.AddCarrotAsync(db);
            var record = new MealRecord { UserId = User.FixedUserId, EatenOn = date, MealType = mealType };
            var item = new MealRecordItem { FoodId = food.Id, InputMode = "GRAM", InputValue = 100m, AmountG = 100m };
            foreach (var nutrient in await db.Nutrients.ToListAsync())
            {
                item.Nutrients.Add(new MealRecordItemNutrient
                {
                    NutrientId = nutrient.Id, Amount = amounts.GetValueOrDefault(nutrient.Code), IsIncomplete = incomplete.Contains(nutrient.Code),
                });
            }
            record.Items.Add(item);
            db.MealRecords.Add(record);
            await db.SaveChangesAsync();
        });
    }
}
