using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// 最近使った食材・メニューを返す API の入口です。
/// </summary>
[ApiController]
[Route("api/recent-items")]
public class RecentItemsController(RecentItemService recent) : ControllerBase
{
    /// <summary>
    /// 最近記録に使った食材とメニューを返します（GET /api/recent-items、API-20）。
    /// 記録画面で、いつも食べるものをすばやく選べるようにするためのものです。
    /// </summary>
    /// <param name="limit">メニュー・食材それぞれの最大件数（既定 10、最大 30）。</param>
    /// <returns>最近使ったメニューと食材（重複なし、新しい順）。</returns>
    [HttpGet]
    public async Task<RecentItemsDto> Get([FromQuery] int? limit) => await recent.GetAsync(limit);
}
