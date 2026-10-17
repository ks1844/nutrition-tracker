using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/foods")]
public class FoodsController(FoodService foods) : ControllerBase
{
    /// <summary>API-01 食材検索</summary>
    [HttpGet]
    public async Task<FoodSearchResultDto> Search([FromQuery] string? q, [FromQuery] int? limit, [FromQuery] int? offset) =>
        await foods.SearchAsync(q, limit, offset);

    /// <summary>API-02 食材詳細</summary>
    [HttpGet("{id:long}")]
    public async Task<FoodDetailDto> Get(long id) => await foods.GetAsync(id);
}
