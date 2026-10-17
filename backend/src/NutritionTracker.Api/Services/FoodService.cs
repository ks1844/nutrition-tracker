using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Text;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 食材マスタの検索と詳細取得のサービスです。
/// </summary>
public class FoodService(AppDbContext db)
{
    /// <summary>
    /// 検索で返す件数の既定値（20 件）。
    /// </summary>
    private const int DefaultLimit = 20;
    /// <summary>
    /// 検索で一度に返せる件数の上限（50 件）。
    /// </summary>
    private const int MaxLimit = 50;
    /// <summary>
    /// 検索語の最大文字数（50 文字）。
    /// </summary>
    private const int MaxQueryLength = 50;

    /// <summary>
    /// 食材を検索します（API-01）。
    /// 検索語を正規化（ひらがな・カタカナ・全角半角）し、名前・読み仮名・別名のどれかに部分一致する、有効な食材を探します。
    /// 名前などが前方一致するものを先頭に並べ、その後は ID 順です。
    /// </summary>
    /// <param name="q">検索語（前後の空白を除いて 1〜50 文字。空や長すぎるものは 400）。</param>
    /// <param name="limit">返す最大件数（既定 20、最大 50。1 未満は 400）。</param>
    /// <param name="offset">読み飛ばす件数（既定 0。負の値は 400）。</param>
    /// <returns>該当する食材の一覧と、該当した総件数。</returns>
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

    /// <summary>
    /// 食材 1 件の詳細を返します（API-02）。無効な食材も取得できます。
    /// </summary>
    /// <param name="id">食材の ID。</param>
    /// <returns>全栄養素の含有量（表示順）、換算単位（既定を先頭に）、別名、出典を含む詳細。存在しなければ 404。</returns>
    public async Task<FoodDetailDto> GetAsync(long id)
    {
        var food = await db.Foods.AsNoTracking()
            .Include(f => f.Units)
            .Include(f => f.Aliases)
            .Include(f => f.Nutrients).ThenInclude(n => n.Nutrient)
            .FirstOrDefaultAsync(f => f.Id == id)
            ?? throw new NotFoundException();

        return new FoodDetailDto(
            food.Id, food.FoodCode, food.Name, food.OfficialName, food.NameKana, food.FoodGroup, food.RefuseRatePct,
            food.SourceVersion, food.IsActive,
            food.Aliases.Select(a => a.Alias).OrderBy(a => a, StringComparer.Ordinal).ToList(),
            ToUnits(food),
            food.Nutrients
                .OrderBy(n => n.Nutrient!.DisplayOrder)
                .Select(n => new FoodNutrientDto(n.Nutrient!.Code, n.AmountPer100g, n.IsTrace, n.SourceNote))
                .ToList());
    }

    /// <summary>
    /// 食材を検索結果の 1 件の形に変換します。
    /// </summary>
    /// <param name="f">換算単位を読み込んだ食材。</param>
    /// <returns>検索結果の 1 件。</returns>
    private static FoodSummaryDto ToSummary(Food f) =>
        new(f.Id, f.FoodCode, f.Name, f.NameKana, f.FoodGroup, f.RefuseRatePct, ToUnits(f));

    /// <summary>
    /// 食材の換算単位を、既定の単位を先頭にして並べ、返却用の形に変換します。
    /// </summary>
    /// <param name="f">換算単位を読み込んだ食材。</param>
    /// <returns>換算単位の一覧。</returns>
    private static IReadOnlyList<FoodUnitDto> ToUnits(Food f) => f.Units
        .OrderByDescending(u => u.IsDefault).ThenBy(u => u.Id)
        .Select(u => new FoodUnitDto(u.Id, u.UnitName, u.GramsPerUnit, u.IsDefault))
        .ToList();

    /// <summary>
    /// 名前・読み仮名・別名のどれかが、検索語の候補のどれかで始まっているか（前方一致）を判定します。
    /// 前方一致する食材を検索結果の先頭に並べるために使います。
    /// </summary>
    /// <param name="f">別名を読み込んだ食材。</param>
    /// <param name="candidates">検索語の候補（原文・ひらがな・カタカナ）。</param>
    /// <returns>前方一致するとき true。</returns>
    private static bool IsPrefixMatch(Food f, IReadOnlyList<string> candidates) =>
        candidates.Any(c =>
            f.Name.StartsWith(c, StringComparison.OrdinalIgnoreCase)
            || f.NameKana.StartsWith(c, StringComparison.OrdinalIgnoreCase)
            || f.Aliases.Any(a => a.Alias.StartsWith(c, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// LIKE 検索で特別な意味を持つ文字（\ % _）を、文字そのものとして扱えるよう打ち消します。
    /// </summary>
    /// <param name="value">検索語。</param>
    /// <returns>打ち消し済みの検索語。</returns>
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
