using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NutritionTracker.Api.Tests;

/// <summary>ヘルスチェック（docs/09.API仕様.md §1）。DB を使わない。</summary>
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        // 開発用の Master フォルダに CSV があっても、起動時に DB へ取り込まない（このテストは DB を使わない）
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["Nutrition:MasterImport:Enabled"] = "false" })))
            .CreateClient();

        var res = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("""{"status":"ok"}""", await res.Content.ReadAsStringAsync());
    }
}
