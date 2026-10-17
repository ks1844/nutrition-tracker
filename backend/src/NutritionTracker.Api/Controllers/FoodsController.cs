using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// 食材マスタを検索・参照する API の入口です。
/// </summary>
[ApiController]
[Route("api/foods")]
public class FoodsController(FoodService foods) : ControllerBase
{
    /// <summary>
    /// 食材を名前・読み仮名・別名で検索します（GET /api/foods、API-01）。
    /// ひらがな・カタカナ・全角半角の違いを区別せず、有効な食材だけを返します。名前が前方一致するものを先に並べます。
    /// </summary>
    /// <param name="q">検索語（必須。前後の空白を除いて 1〜50 文字）。</param>
    /// <param name="limit">返す最大件数（既定 20、最大 50）。</param>
    /// <param name="offset">読み飛ばす件数（既定 0）。</param>
    /// <returns>条件に合う食材と総件数。</returns>
    [HttpGet]
    public async Task<FoodSearchResultDto> Search([FromQuery] string? q, [FromQuery] int? limit, [FromQuery] int? offset) =>
        await foods.SearchAsync(q, limit, offset);

    /// <summary>
    /// 食材 1 件の詳細を返します（GET /api/foods/{id}、API-02）。
    /// 全栄養素の含有量（未測定は null）、換算単位、別名、出典の版を含みます。無効な食材も取得できます。
    /// </summary>
    /// <param name="id">食材の ID。</param>
    /// <returns>食材の詳細。存在しなければ 404。</returns>
    [HttpGet("{id:long}")]
    public async Task<FoodDetailDto> Get(long id) => await foods.GetAsync(id);
}
