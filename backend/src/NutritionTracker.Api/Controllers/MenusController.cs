using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/menus")]
public class MenusController(MenuService menus) : ControllerBase
{
    /// <summary>API-06 メニュー一覧（残量つき）</summary>
    [HttpGet]
    public async Task<IReadOnlyList<MenuSummaryDto>> List() => await menus.ListAsync();

    /// <summary>API-07 メニュー作成</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MenuInput input)
    {
        var created = await menus.CreateAsync(input);
        return Created($"/api/menus/{created.Id}", created);
    }

    /// <summary>API-12 未保存メニューの栄養計算（DB 更新なし）</summary>
    [HttpPost("preview")]
    public async Task<MenuPreviewDto> Preview([FromBody] MenuInput input) => await menus.PreviewAsync(input);

    /// <summary>API-08 メニュー詳細</summary>
    [HttpGet("{id:long}")]
    public async Task<MenuDetailDto> Get(long id) => await menus.GetAsync(id);

    /// <summary>API-09 メニュー更新（全項目置換）</summary>
    [HttpPut("{id:long}")]
    public async Task<MenuDetailDto> Update(long id, [FromBody] MenuInput input) => await menus.UpdateAsync(id, input);

    /// <summary>API-10 メニュー削除（論理削除）</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await menus.DeleteAsync(id);
        return NoContent();
    }
}
