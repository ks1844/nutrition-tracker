using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/meal-records")]
public class MealRecordsController(MealRecordService records) : ControllerBase
{
    /// <summary>API-13 日別の食事記録</summary>
    [HttpGet]
    public async Task<MealRecordDayDto> GetByDate([FromQuery] DateOnly? date) => await records.GetByDateAsync(date);

    /// <summary>API-14 食事記録の作成</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MealRecordInput input)
    {
        var created = await records.CreateAsync(input);
        return Created($"/api/meal-records?date={created.EatenOn:yyyy-MM-dd}", created);
    }

    /// <summary>API-15 食事記録の更新（全項目置換）</summary>
    [HttpPut("{id:long}")]
    public async Task<MealRecordSaveResultDto> Update(long id, [FromBody] MealRecordInput input) => await records.UpdateAsync(id, input);
}
