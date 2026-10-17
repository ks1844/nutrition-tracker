using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController(ProfileService profiles) : ControllerBase
{
    /// <summary>API-05 プロフィール登録・更新</summary>
    [HttpPut]
    public async Task<ProfileDto> Put([FromBody] ProfileInput input) => await profiles.SaveAsync(input);
}
