using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController(ProfileService profiles) : ControllerBase
{
    /// <summary>API-04 プロフィール取得（目標を含む）</summary>
    [HttpGet]
    public async Task<ProfileDto> Get() => await profiles.GetAsync();

    /// <summary>API-05 プロフィール登録・更新</summary>
    [HttpPut]
    public async Task<ProfileDto> Put([FromBody] ProfileInput input) => await profiles.SaveAsync(input);
}
