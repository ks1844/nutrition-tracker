using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data;

/// <summary>
/// データベースへの窓口（EF Core の DbContext）です。
/// テーブル名・列名は日本語（テーブルは T＋日本語名）で、Fluent API で対応づけます（docs/05.テーブル定義書.md、docs/adr/0007）。
/// </summary>
/// <param name="options">接続先などの設定。</param>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 栄養素のマスタ（T栄養素）。
    /// </summary>
    public DbSet<Nutrient> Nutrients => Set<Nutrient>();

    /// <summary>
    /// 食材のマスタ（T食材）。
    /// </summary>
    public DbSet<Food> Foods => Set<Food>();

    /// <summary>
    /// 食材の別名（T食材別名）。
    /// </summary>
    public DbSet<FoodAlias> FoodAliases => Set<FoodAlias>();

    /// <summary>
    /// 食材の栄養素ごとの含有量（T食材栄養素）。
    /// </summary>
    public DbSet<FoodNutrient> FoodNutrients => Set<FoodNutrient>();

    /// <summary>
    /// 食材の換算単位（T食材単位）。
    /// </summary>
    public DbSet<FoodUnit> FoodUnits => Set<FoodUnit>();

    /// <summary>
    /// 利用者（Tユーザー）。
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// 利用者のプロフィール（Tユーザープロフィール）。
    /// </summary>
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    /// <summary>
    /// 栄養素ごとの目標（T栄養目標）。
    /// </summary>
    public DbSet<NutrientTarget> NutrientTargets => Set<NutrientTarget>();

    /// <summary>
    /// メニュー（Tメニュー）。
    /// </summary>
    public DbSet<Menu> Menus => Set<Menu>();

    /// <summary>
    /// メニューを構成する食材（Tメニュー食材）。
    /// </summary>
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    /// <summary>
    /// 食事記録（T食事記録）。
    /// </summary>
    public DbSet<MealRecord> MealRecords => Set<MealRecord>();

    /// <summary>
    /// 食事記録で食べたもの（T食事記録項目）。
    /// </summary>
    public DbSet<MealRecordItem> MealRecordItems => Set<MealRecordItem>();

    /// <summary>
    /// 記録時点の栄養量（T食事記録項目栄養素）。
    /// </summary>
    public DbSet<MealRecordItemNutrient> MealRecordItemNutrients => Set<MealRecordItemNutrient>();

    /// <summary>
    /// テーブル・列の対応づけを、同じアセンブリにある Configurations 配下の設定からまとめて読み込みます。
    /// </summary>
    /// <param name="modelBuilder">モデルを組み立てるビルダー。</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
