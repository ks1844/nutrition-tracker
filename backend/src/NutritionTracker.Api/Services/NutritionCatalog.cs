using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

/// <summary>栄養計算に必要な栄養素の情報（コード一覧・参考値）と、DB の食材 → 計算用データの変換。リクエスト内で 1 度だけ読み込む。</summary>
public class NutritionCatalog(AppDbContext db)
{
    private IReadOnlyList<Nutrient>? _nutrients;

    public async Task<IReadOnlyList<Nutrient>> NutrientsAsync() =>
        _nutrients ??= await db.Nutrients.AsNoTracking().OrderBy(n => n.DisplayOrder).ToListAsync();

    public async Task<IReadOnlyList<string>> CodesAsync() => (await NutrientsAsync()).Select(n => n.Code).ToList();

    /// <summary>参考値の栄養素（例: ASTX）。行が無くても未測定（不完全）にしない。</summary>
    public async Task<IReadOnlySet<string>> ReferenceCodesAsync() =>
        (await NutrientsAsync()).Where(n => n.IsReference).Select(n => n.Code).ToHashSet();

    /// <summary>食材 → 計算用データ。<paramref name="food"/> は Nutrients と、その Nutrient を読み込んでおくこと。</summary>
    public static FoodData ToFoodData(Food food) => new(
        food.RefuseRatePct,
        food.Nutrients.ToDictionary(n => n.Nutrient!.Code, n => n.AmountPer100g));

    /// <summary>栄養素のコード → 量（API の形）。表示順に並べる。</summary>
    public async Task<IReadOnlyList<NutrientAmountDto>> ToDtosAsync(NutrientTotals totals) =>
        (await NutrientsAsync())
            .Where(n => totals.Codes.Contains(n.Code))
            .Select(n => new NutrientAmountDto(n.Code, totals[n.Code].Amount, totals[n.Code].IsIncomplete))
            .ToList();
}
