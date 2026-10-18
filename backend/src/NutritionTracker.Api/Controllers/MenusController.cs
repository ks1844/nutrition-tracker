using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// メニュー（作り置きの料理）の作成・参照・更新・削除・複製と、保存前の栄養計算の API の入口です。
/// </summary>
[ApiController]
[Route("api/menus")]
public class MenusController(MenuService menus) : ControllerBase
{
    /// <summary>
    /// メニューの一覧を返します（GET /api/menus、API-06）。
    /// 削除していないメニューを新しい順に、1 杯の重さと残りの量つきで返します。
    /// </summary>
    /// <returns>メニューの一覧。</returns>
    [HttpGet]
    public async Task<IReadOnlyList<MenuSummaryDto>> List() => await menus.ListAsync();

    /// <summary>
    /// メニューを作成します（POST /api/menus、API-07）。
    /// 食材は g か単位（本・大さじ など）で指定でき、サーバーが重さに換算して保存し、メニュー全体と 1 杯あたりの栄養量を返します。
    /// </summary>
    /// <param name="input">名前・完成後の重さ・杯数・食材など。</param>
    /// <returns>201 と、作ったメニューの詳細。</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MenuInput input)
    {
        var created = await menus.CreateAsync(input);
        return Created($"/api/menus/{created.Id}", created);
    }

    /// <summary>
    /// 保存する前のメニューの栄養量を計算します（POST /api/menus/preview、API-12）。データベースは更新しません。
    /// 食材を入力するたびにリアルタイムで栄養を確かめる画面が使います。
    /// </summary>
    /// <param name="input">名前・完成後の重さ・杯数は省略できます。食材は必須。</param>
    /// <returns>食材ごとと全体の栄養量、1 杯あたりの栄養量、警告。</returns>
    [HttpPost("preview")]
    public async Task<MenuPreviewDto> Preview([FromBody] MenuInput input) => await menus.PreviewAsync(input);

    /// <summary>
    /// メニュー 1 件の詳細を返します（GET /api/menus/{id}、API-08）。
    /// 食材、メニュー全体と 1 杯あたりの栄養量、残りの量を含みます。
    /// </summary>
    /// <param name="id">メニューの ID。</param>
    /// <returns>メニューの詳細。存在しない、または削除済みなら 404。</returns>
    [HttpGet("{id:long}")]
    public async Task<MenuDetailDto> Get(long id) => await menus.GetAsync(id);

    /// <summary>
    /// メニューを更新します（PUT /api/menus/{id}、API-09。全項目の置き換え）。
    /// 過去の記録には栄養量が保存済みなので、更新しても変わりません。
    /// </summary>
    /// <param name="id">更新するメニューの ID。</param>
    /// <param name="input">更新後の内容。</param>
    /// <returns>更新後のメニューの詳細。</returns>
    [HttpPut("{id:long}")]
    public async Task<MenuDetailDto> Update(long id, [FromBody] MenuInput input) => await menus.UpdateAsync(id, input);

    /// <summary>
    /// メニューを削除します（DELETE /api/menus/{id}、API-10）。実際には削除日時を入れるだけ（論理削除）で、過去の記録は残ります。
    /// </summary>
    /// <param name="id">削除するメニューの ID。</param>
    /// <returns>204（本文なし）。存在しない、または削除済みなら 404。</returns>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await menus.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// メニューを複製します（POST /api/menus/{id}/duplicate、API-11）。
    /// 食材・完成後の重さ・杯数・メモを写した新しいメニューを作ります。記録は複製しません。
    /// </summary>
    /// <param name="id">複製元のメニューの ID。</param>
    /// <param name="input">新しい名前と作った日（省略可）。省略すると「〜のコピー」と今日になります。</param>
    /// <returns>201 と、複製したメニューの詳細。</returns>
    [HttpPost("{id:long}/duplicate")]
    public async Task<IActionResult> Duplicate(long id, [FromBody] MenuDuplicateInput? input = null)
    {
        var created = await menus.DuplicateAsync(id, input);
        return Created($"/api/menus/{created.Id}", created);
    }
}
