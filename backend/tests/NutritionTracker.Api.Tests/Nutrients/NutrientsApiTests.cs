using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Tests.Nutrients;

/// <summary>API-03 栄養素一覧（docs/09.API仕様.md §3）。</summary>
[Collection(ApiCollection.Name)]
public class NutrientsApiTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetNutrients_ReturnsAllTwentyFour_InDisplayOrder()
    {
        var res = await fixture.Client.GetAsync("/api/nutrients");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<NutrientDto>>();
        Assert.NotNull(list);
        Assert.Equal(NutrientCodes.All, list.Select(n => n.Code).ToList());
        Assert.Equal(Enumerable.Range(1, 24), list.Select(n => n.DisplayOrder));

        var protein = list.Single(n => n.Code == "PROT");
        Assert.Equal(new NutrientDto("PROT", "タンパク質", "g", "macro", false, 2), protein);
        Assert.True(list.Single(n => n.Code == "ASTX").IsReference); // 参考値（スコア外）
    }

    [Fact]
    public async Task GetNutrients_JsonIsCamelCase()
    {
        var json = await fixture.Client.GetStringAsync("/api/nutrients");

        Assert.Contains("\"nameJa\"", json);
        Assert.Contains("\"isReference\"", json);
        Assert.Contains("\"displayOrder\"", json);
    }

    [Fact]
    public async Task UnknownApiPath_ReturnsNotFoundInErrorFormat()
    {
        var res = await fixture.Client.GetAsync("/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("NOT_FOUND", err!.Code);
        Assert.Empty(err.Details);
    }
}
