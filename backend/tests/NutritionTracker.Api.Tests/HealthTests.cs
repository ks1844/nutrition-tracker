using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NutritionTracker.Api.Tests;

/// <summary>ヘルスチェック（docs/09.API仕様.md §1）。DB を使わない。</summary>
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("""{"status":"ok"}""", await res.Content.ReadAsStringAsync());
    }
}
