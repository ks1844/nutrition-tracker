using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>API-17 日次ダッシュボード</summary>
    [HttpGet("daily")]
    public async Task<DailyDashboardDto> Daily([FromQuery] DateOnly? date) => await dashboard.DailyAsync(date);

    /// <summary>API-18 直近 7 日ダッシュボード</summary>
    [HttpGet("weekly")]
    public async Task<WeeklyDashboardDto> Weekly([FromQuery] DateOnly? endDate) => await dashboard.WeeklyAsync(endDate);
}
