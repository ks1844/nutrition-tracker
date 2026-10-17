using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/nutrients")]
public class NutrientsController(NutrientService nutrients) : ControllerBase
{
    /// <summary>API-03 栄養素一覧</summary>
    [HttpGet]
    public async Task<IReadOnlyList<NutrientDto>> List() => await nutrients.ListAsync();
}
