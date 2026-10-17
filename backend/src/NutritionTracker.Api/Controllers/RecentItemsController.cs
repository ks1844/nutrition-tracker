using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/recent-items")]
public class RecentItemsController(RecentItemService recent) : ControllerBase
{
    /// <summary>API-20 最近使った食材・メニュー</summary>
    [HttpGet]
    public async Task<RecentItemsDto> Get([FromQuery] int? limit) => await recent.GetAsync(limit);
}
