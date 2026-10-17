using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/meal-records")]
public class MealRecordsController(MealRecordService records) : ControllerBase
{
    /// <summary>API-14 食事記録の作成</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MealRecordInput input)
    {
        var created = await records.CreateAsync(input);
        return Created($"/api/meal-records?date={created.EatenOn:yyyy-MM-dd}", created);
    }
}
