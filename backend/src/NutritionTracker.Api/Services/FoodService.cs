using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Text;

namespace NutritionTracker.Api.Services;

public class FoodService(AppDbContext db)
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;
    private const int MaxQueryLength = 50;

    /// <summary>
    /// API-01 食材検索。検索語を正規化（かな・カナ・全角半角）し、名称・読み仮名・別名の部分一致で探す。
    /// 有効な食材だけを返し、名前の前方一致を先頭に並べる。
    /// </summary>
    public async Task<FoodSearchResultDto> SearchAsync(string? q, int? limit, int? offset)
    {
        var trimmed = q?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            throw new ValidationException("q", "検索語を入力してください");
        }
        if (trimmed.Length > MaxQueryLength)
        {
            throw new ValidationException("q", $"{MaxQueryLength}文字以内で入力してください");
        }
        if (limit is < 1)
        {
            throw new ValidationException("limit", "1以上の値を指定してください");
        }
        if (offset is < 0)
        {
            throw new ValidationException("offset", "0以上の値を指定してください");
        }
        var pageSize = Math.Min(limit ?? DefaultLimit, MaxLimit);
        var skip = offset ?? 0;

        var candidates = SearchTextNormalizer.Candidates(trimmed);
        var ids = new HashSet<long>();
        foreach (var candidate in candidates)
        {
            var like = $"%{EscapeLike(candidate)}%";
            ids.UnionWith(await db.Foods
                .Where(f => f.IsActive
                    && (EF.Functions.Like(f.Name, like)
                        || EF.Functions.Like(f.NameKana, like)
                        || f.Aliases.Any(a => EF.Functions.Like(a.Alias, like))))
                .Select(f => f.Id)
                .ToListAsync());
        }

        var foods = await db.Foods.AsNoTracking()
            .Include(f => f.Units)
            .Include(f => f.Aliases)
            .Where(f => ids.Contains(f.Id))
            .ToListAsync();

        var ordered = foods
            .OrderByDescending(f => IsPrefixMatch(f, candidates) ? 1 : 0)
            .ThenBy(f => f.Id)
            .ToList();
        var items = ordered.Skip(skip).Take(pageSize).Select(ToSummary).ToList();
        return new FoodSearchResultDto(items, ordered.Count);
    }

    private static FoodSummaryDto ToSummary(Food f) =>
        new(f.Id, f.FoodCode, f.Name, f.NameKana, f.FoodGroup, f.RefuseRatePct, ToUnits(f));

    private static IReadOnlyList<FoodUnitDto> ToUnits(Food f) => f.Units
        .OrderByDescending(u => u.IsDefault).ThenBy(u => u.Id)
        .Select(u => new FoodUnitDto(u.Id, u.UnitName, u.GramsPerUnit, u.IsDefault))
        .ToList();

    private static bool IsPrefixMatch(Food f, IReadOnlyList<string> candidates) =>
        candidates.Any(c =>
            f.Name.StartsWith(c, StringComparison.OrdinalIgnoreCase)
            || f.NameKana.StartsWith(c, StringComparison.OrdinalIgnoreCase)
            || f.Aliases.Any(a => a.Alias.StartsWith(c, StringComparison.OrdinalIgnoreCase)));

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
