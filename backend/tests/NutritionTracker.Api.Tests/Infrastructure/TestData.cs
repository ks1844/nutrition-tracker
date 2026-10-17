using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Tests.Infrastructure;

/// <summary>
/// テスト用データの作成。<b>食材の値はテスト用のダミーで、実際の成分表の値ではない</b>
/// （docs/03.栄養計算仕様.md §9 の正解データと同じ値を含む）。
/// </summary>
public static class TestData
{
    public sealed record UnitSpec(string Name, decimal GramsPerUnit, bool IsDefault = false);

    public static async Task<Food> AddFoodAsync(
        AppDbContext db,
        string code,
        string name,
        string kana,
        decimal refusePct = 0m,
        bool isActive = true,
        string[]? aliases = null,
        UnitSpec[]? units = null,
        Dictionary<string, decimal?>? nutrients = null,
        HashSet<string>? traces = null)
    {
        var food = new Food
        {
            FoodCode = code, Name = name, NameKana = kana, RefuseRatePct = refusePct, SourceVersion = "テスト（ダミー）", IsActive = isActive,
            FoodGroup = "テスト",
        };
        foreach (var alias in aliases ?? [])
        {
            food.Aliases.Add(new FoodAlias { Alias = alias });
        }
        foreach (var u in units ?? [])
        {
            food.Units.Add(new FoodUnit { UnitName = u.Name, GramsPerUnit = u.GramsPerUnit, IsDefault = u.IsDefault });
        }
        var nutrientIds = await db.Nutrients.ToDictionaryAsync(n => n.Code, n => n.Id);
        foreach (var (nutrientCode, per100g) in nutrients ?? [])
        {
            food.Nutrients.Add(new FoodNutrient
            {
                NutrientId = nutrientIds[nutrientCode], AmountPer100g = per100g, IsTrace = traces?.Contains(nutrientCode) ?? false,
            });
        }
        db.Foods.Add(food);
        await db.SaveChangesAsync();
        return food;
    }

    /// <summary>F1 にんじん（廃棄率 10%、1 本 = 150g）</summary>
    public static Task<Food> AddCarrotAsync(AppDbContext db) => AddFoodAsync(
        db, "F1", "にんじん", "にんじん", refusePct: 10m, aliases: ["人参", "ニンジン"],
        units: [new UnitSpec("本", 150m, true)],
        nutrients: new() { ["PROT"] = 1.0m, ["VITC"] = 10m, ["VITA_RAE"] = 700m });

    /// <summary>F2 豚こま肉（廃棄率 0）</summary>
    public static Task<Food> AddPorkAsync(AppDbContext db) => AddFoodAsync(
        db, "F2", "豚こま肉", "ぶたこまにく", aliases: ["豚肉"],
        nutrients: new() { ["PROT"] = 20.0m, ["VITC"] = 1m, ["VITA_RAE"] = 10m });

    /// <summary>F3 みそ（ビタミン C は未測定。大さじ = 18g）</summary>
    public static Task<Food> AddMisoAsync(AppDbContext db) => AddFoodAsync(
        db, "F3", "みそ", "みそ", aliases: ["味噌"],
        units: [new UnitSpec("大さじ", 18m, true)],
        nutrients: new() { ["PROT"] = 12.0m, ["VITC"] = null, ["VITA_RAE"] = 0m });

    /// <summary>正解データ（にんじん・豚こま肉・みそ）。</summary>
    public sealed record Golden(Food Carrot, Food Pork, Food Miso)
    {
        public long CarrotUnitId => Carrot.Units[0].Id;

        public long MisoUnitId => Miso.Units[0].Id;
    }

    public static async Task<Golden> AddGoldenAsync(AppDbContext db) =>
        new(await AddCarrotAsync(db), await AddPorkAsync(db), await AddMisoAsync(db));
}

