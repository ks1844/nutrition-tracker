using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data;

/// <summary>
/// DbContext。テーブル名・列名は日本語（テーブルは <c>T</c> ＋日本語名）で、Fluent API でマッピングする
/// （docs/05.テーブル定義書.md、docs/adr/0007）。
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Nutrient> Nutrients => Set<Nutrient>();

    public DbSet<Food> Foods => Set<Food>();

    public DbSet<FoodAlias> FoodAliases => Set<FoodAlias>();

    public DbSet<FoodNutrient> FoodNutrients => Set<FoodNutrient>();

    public DbSet<FoodUnit> FoodUnits => Set<FoodUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
