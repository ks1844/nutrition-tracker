using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 栄養素マスタを参照するサービスです。
/// </summary>
public class NutrientService(AppDbContext db)
{
    /// <summary>
    /// 栄養素の一覧を、表示順に返します（API-03）。
    /// </summary>
    /// <returns>24 種の栄養素（コード・名前・単位・区分・参考値かどうか・表示順）。</returns>
    public async Task<IReadOnlyList<NutrientDto>> ListAsync() =>
        await db.Nutrients.AsNoTracking()
            .OrderBy(n => n.DisplayOrder)
            .Select(n => new NutrientDto(n.Code, n.Name, n.Unit, n.Category, n.IsReference, n.DisplayOrder))
            .ToListAsync();
}
