using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/menus")]
public class MenusController(MenuService menus) : ControllerBase
{
    /// <summary>API-07 メニュー作成</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MenuInput input)
    {
        var created = await menus.CreateAsync(input);
        return Created($"/api/menus/{created.Id}", created);
    }

    /// <summary>API-08 メニュー詳細</summary>
    [HttpGet("{id:long}")]
    public async Task<MenuDetailDto> Get(long id) => await menus.GetAsync(id);
}
