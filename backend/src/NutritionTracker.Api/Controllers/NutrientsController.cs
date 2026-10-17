using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// 栄養素の一覧と、栄養素ごとの寄与食材を返す API の入口です。
/// </summary>
[ApiController]
[Route("api/nutrients")]
public class NutrientsController(NutrientService nutrients, ContributionService contributions) : ControllerBase
{
    /// <summary>
    /// 栄養素の一覧を返します（GET /api/nutrients、API-03）。表示順に、名前・単位・区分を返します。
    /// </summary>
    /// <returns>24 種の栄養素。</returns>
    [HttpGet]
    public async Task<IReadOnlyList<NutrientDto>> List() => await nutrients.ListAsync();

    /// <summary>
    /// ある栄養素を、期間内にどの食べものから摂ったかを返します（GET /api/nutrients/{code}/contributions、API-19）。
    /// メニュー・単品の食材ごとの量と割合を大きい順に並べ、メニューには食材の内訳（参考値）を付けます。
    /// </summary>
    /// <param name="code">栄養素コード（例: PROT）。</param>
    /// <param name="from">期間の開始日（必須）。</param>
    /// <param name="to">期間の終了日（必須。開始日から最大 31 日）。</param>
    /// <returns>寄与食材の一覧。</returns>
    [HttpGet("{code}/contributions")]
    public async Task<ContributionsDto> Contributions(string code, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        await contributions.GetAsync(code, from, to);
}
