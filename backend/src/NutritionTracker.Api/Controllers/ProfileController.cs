using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// プロフィール（身長・体重など）の参照と登録の API の入口です。
/// </summary>
[ApiController]
[Route("api/profile")]
public class ProfileController(ProfileService profiles) : ControllerBase
{
    /// <summary>
    /// プロフィールと、そこから算出した目標を返します（GET /api/profile、API-04）。
    /// </summary>
    /// <returns>プロフィールと目標。未登録なら 404。</returns>
    [HttpGet]
    public async Task<ProfileDto> Get() => await profiles.GetAsync();

    /// <summary>
    /// プロフィールを登録・更新します（PUT /api/profile、API-05。なければ作り、あれば更新）。
    /// エネルギーとタンパク質の目標を計算して返します。
    /// </summary>
    /// <param name="input">性別・生年月日・身長・体重・活動レベル・目的、エネルギー目標の手入力。</param>
    /// <returns>保存したプロフィールと目標。</returns>
    [HttpPut]
    public async Task<ProfileDto> Put([FromBody] ProfileInput input) => await profiles.SaveAsync(input);
}
