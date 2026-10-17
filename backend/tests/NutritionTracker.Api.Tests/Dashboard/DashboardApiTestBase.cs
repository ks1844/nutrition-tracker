using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Dashboard;

/// <summary>ダッシュボード API テストの共通部品。摂取量は保存済みスナップショットを直接作って与える。</summary>
public abstract class DashboardApiTestBase(ApiFixture fixture) : IAsyncLifetime
{
    protected ApiFixture Fixture { get; } = fixture;

    protected static readonly DateOnly Day = ApiFixture.Today;

    public Task InitializeAsync() => Fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>男性・30 歳・170cm・65kg・活動レベル 2 → エネルギー目標 2155 kcal、目的 both → タンパク質目標 91.0 g</summary>
    protected async Task PutProfileAsync(string goal = "both") =>
        Assert.Equal(HttpStatusCode.OK, (await Fixture.Client.PutAsJsonAsync(
            "/api/profile", new ProfileInput("male", new DateOnly(1996, 4, 1), 170.0m, 65.0m, 2, goal, null))).StatusCode);

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

    /// <summary>1 食分の記録を直接作る（単品食材の 1 項目に、与えた量のスナップショットを持たせる）。</summary>
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
