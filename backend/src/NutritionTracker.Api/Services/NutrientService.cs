using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;

namespace NutritionTracker.Api.Services;

public class NutrientService(AppDbContext db)
{
    /// <summary>栄養素の一覧（表示順）。</summary>
    public async Task<IReadOnlyList<NutrientDto>> ListAsync() =>
        await db.Nutrients.AsNoTracking()
            .OrderBy(n => n.DisplayOrder)
            .Select(n => new NutrientDto(n.Code, n.Name, n.Unit, n.Category, n.IsReference, n.DisplayOrder))
            .ToListAsync();
}
