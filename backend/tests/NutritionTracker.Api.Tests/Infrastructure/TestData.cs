using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Tests.Infrastructure;

/// <summary>
/// テスト用データを作る補助です。
/// <b>食材の値はテスト用のダミーで、実際の成分表の値ではありません</b>（docs/03.栄養計算仕様.md §9 の正解データと同じ値を含みます）。
/// </summary>
public static class TestData
{
    /// <summary>
    /// テスト用の換算単位の指定です。
    /// </summary>
    /// <param name="Name">単位の名前。</param>
    /// <param name="GramsPerUnit">1 単位あたりの重さ（g）。</param>
    /// <param name="IsDefault">既定の単位か。</param>
    public sealed record UnitSpec(string Name, decimal GramsPerUnit, bool IsDefault = false);

    /// <summary>
    /// 食材をデータベースに登録します。
    /// </summary>
    /// <param name="db">データベースへの窓口。</param>
    /// <param name="code">食品コード。</param>
    /// <param name="name">名前。</param>
    /// <param name="kana">読み仮名。</param>
    /// <param name="refusePct">廃棄率（%）。</param>
    /// <param name="isActive">有効か。</param>
    /// <param name="aliases">別名。</param>
    /// <param name="units">換算単位。</param>
    /// <param name="nutrients">栄養素コード → 100g あたりの含有量（null は未測定）。</param>
    /// <param name="traces">微量（Tr）とする栄養素コード。</param>
    /// <returns>登録した食材。</returns>
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

    /// <summary>
    /// にんじんを登録します（食品コード F1、廃棄率 10%、1 本 = 150g。タンパク質 1.0・ビタミン C 10・ビタミン A 700 / 100g）。
    /// </summary>
    /// <param name="db">データベースへの窓口。</param>
    /// <returns>登録した食材。</returns>
    public static Task<Food> AddCarrotAsync(AppDbContext db) => AddFoodAsync(
        db, "F1", "にんじん", "にんじん", refusePct: 10m, aliases: ["人参", "ニンジン"],
        units: [new UnitSpec("本", 150m, true)],
        nutrients: new() { ["PROT"] = 1.0m, ["VITC"] = 10m, ["VITA_RAE"] = 700m });

    /// <summary>
    /// 豚こま肉を登録します（食品コード F2、廃棄率 0。タンパク質 20.0・ビタミン C 1・ビタミン A 10 / 100g）。
    /// </summary>
    /// <param name="db">データベースへの窓口。</param>
    /// <returns>登録した食材。</returns>
    public static Task<Food> AddPorkAsync(AppDbContext db) => AddFoodAsync(
        db, "F2", "豚こま肉", "ぶたこまにく", aliases: ["豚肉"],
        nutrients: new() { ["PROT"] = 20.0m, ["VITC"] = 1m, ["VITA_RAE"] = 10m });

    /// <summary>
    /// みそを登録します（食品コード F3、大さじ 1 = 18g。タンパク質 12.0・ビタミン A 0 / 100g で、ビタミン C は未測定）。
    /// </summary>
    /// <param name="db">データベースへの窓口。</param>
    /// <returns>登録した食材。</returns>
    public static Task<Food> AddMisoAsync(AppDbContext db) => AddFoodAsync(
        db, "F3", "みそ", "みそ", aliases: ["味噌"],
        units: [new UnitSpec("大さじ", 18m, true)],
        nutrients: new() { ["PROT"] = 12.0m, ["VITC"] = null, ["VITA_RAE"] = 0m });

    /// <summary>
    /// 正解データの 3 食材（にんじん・豚こま肉・みそ）です。
    /// </summary>
    /// <param name="Carrot">にんじん。</param>
    /// <param name="Pork">豚こま肉。</param>
    /// <param name="Miso">みそ。</param>
    public sealed record Golden(Food Carrot, Food Pork, Food Miso)
    {
        /// <summary>
        /// にんじんの単位「本」の ID。
        /// </summary>
        public long CarrotUnitId => Carrot.Units[0].Id;

        /// <summary>
        /// みその単位「大さじ」の ID。
        /// </summary>
        public long MisoUnitId => Miso.Units[0].Id;
    }

    /// <summary>
    /// 正解データの 3 食材（にんじん・豚こま肉・みそ）をまとめて登録します。
    /// </summary>
    /// <param name="db">データベースへの窓口。</param>
    /// <returns>登録した 3 食材。</returns>
    public static async Task<Golden> AddGoldenAsync(AppDbContext db) =>
        new(await AddCarrotAsync(db), await AddPorkAsync(db), await AddMisoAsync(db));
}

