using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Tests.Infrastructure;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Tests.Database;

/// <summary>
/// マイグレーション V1〜V4 を実データベース（MySQL 8.4）に適用した結果と、設計書の制約が効くことのテストです（docs/05.テーブル定義書.md）。
/// </summary>
[Collection(ApiCollection.Name)]
public class DatabaseTests(ApiFixture fixture) : IAsyncLifetime
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
    /// マイグレーションで 13 のテーブルができ、初期データが入ることを確認します。
    /// 栄養素 24 件（設計書の順序、アスタキサンチンは参考値）と、固定ユーザー（ID=1）が入ります。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task Migrations_CreateAllThirteenTables_WithSeedData()
    {
        await fixture.WithDbAsync(async db =>
        {
            var tables = await db.Database
                .SqlQueryRaw<string>("SELECT TABLE_NAME AS Value FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME LIKE 'T%'")
                .ToListAsync();
            Assert.Equal(13, tables.Count);

            var nutrients = await db.Nutrients.OrderBy(n => n.DisplayOrder).ToListAsync();
            Assert.Equal(24, nutrients.Count);
            Assert.Equal(NutrientCodes.All, nutrients.Select(n => n.Code).ToList()); // 栄養素コード一覧の順序
            Assert.True(nutrients.Single(n => n.Code == NutrientCodes.Astaxanthin).IsReference);

            Assert.Equal(User.FixedUserId, (await db.Users.SingleAsync()).Id); // 固定ユーザー（D-1）
        });
    }

    /// <summary>
    /// 1 つの食材に既定の換算単位を 2 つ付けようとすると、データベースの一意制約で拒否されることを確認します（既定でない単位は何件でも可）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task OnlyOneDefaultUnit_IsAllowedPerFood()
    {
        await fixture.WithDbAsync(async db =>
        {
            var food = new Food { FoodCode = "T001", Name = "にんじん", NameKana = "にんじん", SourceVersion = "テスト" };
            food.Units.Add(new FoodUnit { UnitName = "本", GramsPerUnit = 150m, IsDefault = true });
            food.Units.Add(new FoodUnit { UnitName = "個", GramsPerUnit = 100m, IsDefault = false }); // 既定でない単位は何件でも可
            db.Foods.Add(food);
            await db.SaveChangesAsync();

            food.Units.Add(new FoodUnit { UnitName = "束", GramsPerUnit = 300m, IsDefault = true }); // 2 件目の既定 → UNIQUE 違反
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    /// <summary>
    /// データベースの CHECK 制約が不正な行を拒否することを確認します。
    /// 微量（Tr）の印がついているのに含有量が 0 でない行は登録できません。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task CheckConstraints_RejectInvalidRows()
    {
        await fixture.WithDbAsync(async db =>
        {
            // Tr は 0 + 微量フラグ。微量フラグが立っているのに 0 以外は不正（CK_食材栄養素_微量）
            var food = new Food { FoodCode = "T002", Name = "ほうれんそう", NameKana = "ほうれんそう", SourceVersion = "テスト" };
            db.Foods.Add(food);
            await db.SaveChangesAsync();

            db.FoodNutrients.Add(new FoodNutrient { FoodId = food.Id, NutrientId = 1, AmountPer100g = 5m, IsTrace = true });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    /// <summary>
    /// 作成日時・更新日時が、データベースの既定値（現在時刻）で自動的に入ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task UpdatedAt_IsRefreshedOnUpdate_ByTheDatabase()
    {
        await fixture.WithDbAsync(async db =>
        {
            var food = new Food { FoodCode = "T003", Name = "卵", NameKana = "たまご", SourceVersion = "テスト" };
            db.Foods.Add(food);
            await db.SaveChangesAsync();
            Assert.NotEqual(default, food.CreatedAt); // 既定値（CURRENT_TIMESTAMP）が入る
            Assert.NotEqual(default, food.UpdatedAt);
        });
    }
}
