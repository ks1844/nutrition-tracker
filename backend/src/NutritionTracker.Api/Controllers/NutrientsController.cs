using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/nutrients")]
public class NutrientsController(NutrientService nutrients, ContributionService contributions) : ControllerBase
{
    /// <summary>API-03 栄養素一覧</summary>
    [HttpGet]
    public async Task<IReadOnlyList<NutrientDto>> List() => await nutrients.ListAsync();

    /// <summary>API-19 栄養素の寄与食材</summary>
    [HttpGet("{code}/contributions")]
    public async Task<ContributionsDto> Contributions(string code, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        await contributions.GetAsync(code, from, to);
}
