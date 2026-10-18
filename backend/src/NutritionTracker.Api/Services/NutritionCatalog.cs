using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 栄養計算に必要な栄養素の情報（コード一覧・参考値）の取得と、DB の食材を計算用データへ変換する補助です。
/// 栄養素の一覧は 1 回のリクエストの中で一度だけ読み込みます。
/// </summary>
/// <param name="db">データベースへの窓口。</param>
public class NutritionCatalog(AppDbContext db)
{
    /// <summary>
    /// 読み込み済みの栄養素（表示順）。まだ読んでいなければ null。
    /// </summary>
    private IReadOnlyList<Nutrient>? _nutrients;

    /// <summary>
    /// 栄養素のマスタを表示順に返します（初回だけ DB から読み、以降は読み込み済みの値を使います）。
    /// </summary>
    /// <returns>栄養素の一覧。</returns>
    public async Task<IReadOnlyList<Nutrient>> NutrientsAsync() =>
        _nutrients ??= await db.Nutrients.AsNoTracking().OrderBy(n => n.DisplayOrder).ToListAsync();

    /// <summary>
    /// 栄養素コードの一覧（表示順）を返します。
    /// </summary>
    /// <returns>栄養素コードの一覧。</returns>
    public async Task<IReadOnlyList<string>> CodesAsync() => (await NutrientsAsync()).Select(n => n.Code).ToList();

    /// <summary>
    /// 参考値の栄養素（例: アスタキサンチン）のコードを返します。これらは行がなくても未測定（不完全）の印を立てません。
    /// </summary>
    /// <returns>参考値の栄養素コードの集合。</returns>
    public async Task<IReadOnlySet<string>> ReferenceCodesAsync() =>
        (await NutrientsAsync()).Where(n => n.IsReference).Select(n => n.Code).ToHashSet();

    /// <summary>
    /// DB の食材を、栄養計算に使うデータ（廃棄率と栄養素ごとの 100g あたり含有量）に変換します。
    /// </summary>
    /// <param name="food">栄養素（とその栄養素マスタ）を読み込んだ食材。</param>
    /// <returns>計算用の食材データ。</returns>
    public static FoodData ToFoodData(Food food) => new(
        food.RefuseRatePct,
        food.Nutrients.ToDictionary(n => n.Nutrient!.Code, n => n.AmountPer100g));

    /// <summary>
    /// 計算結果の栄養量を、API で返す形（栄養素の表示順）に変換します。
    /// </summary>
    /// <param name="totals">栄養素コードごとの量。</param>
    /// <returns>栄養素ごとの量と未測定の印の一覧。</returns>
    public async Task<IReadOnlyList<NutrientAmountDto>> ToDtosAsync(NutrientTotals totals) =>
        (await NutrientsAsync())
            .Where(n => totals.Codes.Contains(n.Code))
            .Select(n => new NutrientAmountDto(n.Code, totals[n.Code].Amount, totals[n.Code].IsIncomplete))
            .ToList();
}
