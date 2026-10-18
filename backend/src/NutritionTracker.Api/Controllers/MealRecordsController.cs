using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// 食事記録（朝昼夕などで食べたもの）の登録・取得・更新・削除・コピーの API の入口です。
/// </summary>
[ApiController]
[Route("api/meal-records")]
public class MealRecordsController(MealRecordService records) : ControllerBase
{
    /// <summary>
    /// 指定した日の食事記録を返します（GET /api/meal-records、API-13）。
    /// 朝・昼・夕・間食の順に並べ、栄養は記録した時点の値（スナップショット）をそのまま返します。
    /// </summary>
    /// <param name="date">取得する日（必須）。</param>
    /// <returns>その日の食事記録の一覧。記録がなければ空。</returns>
    [HttpGet]
    public async Task<MealRecordDayDto> GetByDate([FromQuery] DateOnly? date) => await records.GetByDateAsync(date);

    /// <summary>
    /// 食事を記録します（POST /api/meal-records、API-14）。
    /// メニュー（割合・杯数・g）と単品の食材（g・単位）を 1 つの食事にまとめて入れられ、サーバーが食べた割合・重さと栄養量を計算して保存します。
    /// メニューの記録が合計 100% を超えたときは、保存したうえで警告を返します。
    /// </summary>
    /// <param name="input">食事日・食事区分・メモ・食べたもの。</param>
    /// <returns>201 と、作った記録（警告つき）。</returns>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MealRecordInput input)
    {
        var created = await records.CreateAsync(input);
        return Created($"/api/meal-records?date={created.EatenOn:yyyy-MM-dd}", created);
    }

    /// <summary>
    /// ある日の記録を別の日にコピーします（POST /api/meal-records/copy、API-21。「昨日と同じ」）。
    /// 現在のメニュー・食材の内容で栄養量を計算し直します。削除済みメニューと無効な食材はスキップして理由を返します。
    /// </summary>
    /// <param name="input">コピー元の日・コピー先の日・コピー先に記録があっても続けるか。</param>
    /// <returns>201 と、作った記録およびスキップした項目。</returns>
    [HttpPost("copy")]
    public async Task<IActionResult> Copy([FromBody] MealRecordCopyInput input)
    {
        var result = await records.CopyAsync(input);
        return Created($"/api/meal-records?date={input.ToDate:yyyy-MM-dd}", result);
    }

    /// <summary>
    /// 食事記録を更新します（PUT /api/meal-records/{id}、API-15。全項目の置き換え）。
    /// 入力が変わった項目だけ栄養量を計算し直し、入力が同じ項目は前の値を残します。
    /// </summary>
    /// <param name="id">更新する食事記録の ID。</param>
    /// <param name="input">更新後の内容。</param>
    /// <returns>更新後の記録（警告つき）。存在しなければ 404。</returns>
    [HttpPut("{id:long}")]
    public async Task<MealRecordSaveResultDto> Update(long id, [FromBody] MealRecordInput input) => await records.UpdateAsync(id, input);

    /// <summary>
    /// 食事記録を削除します（DELETE /api/meal-records/{id}、API-16）。項目と栄養量も一緒に消え、メニューの残りに戻ります。
    /// </summary>
    /// <param name="id">削除する食事記録の ID。</param>
    /// <returns>204（本文なし）。存在しなければ 404。</returns>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await records.DeleteAsync(id);
        return NoContent();
    }
}
