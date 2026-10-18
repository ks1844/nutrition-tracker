using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NutritionTracker.Api.Tests;

/// <summary>
/// ヘルスチェックのテストです（docs/09.API仕様.md §1）。データベースは使いません。
/// </summary>
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// GET /health が 200 で {"status":"ok"} を返すことを確認します（アプリが起動しているかを見るための入口）。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
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
