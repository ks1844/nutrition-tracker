using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Exceptions;

namespace NutritionTracker.Api.Tests.Exceptions;

/// <summary>
/// エラー形式 { code, message, details[] } への変換のテストです（docs/09.API仕様.md §1.2）。データベースは使いません。
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    /// <summary>
    /// 指定した処理を実行する小さなテスト用のサーバーを、エラー変換の仕組みつきで起動します。
    /// </summary>
    /// <param name="endpoint">リクエストを受けたときに実行する処理（例外を投げる）。</param>
    /// <returns>そのサーバーにつながる HTTP クライアント。</returns>
    private static async Task<HttpClient> StartAsync(Func<HttpContext, Task> endpoint)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.Run(new RequestDelegate(endpoint));
                }))
            .StartAsync();
        return host.GetTestClient();
    }

    /// <summary>
    /// テスト用のサーバーを呼び、HTTP ステータスと JSON の本文を返します。
    /// </summary>
    /// <param name="endpoint">リクエストを受けたときに実行する処理。</param>
    /// <returns>HTTP ステータスと、JSON の本文。</returns>
    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(Func<HttpContext, Task> endpoint)
    {
        using var client = await StartAsync(endpoint);
        var res = await client.GetAsync("/");
        return (res.StatusCode, JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement);
    }

    /// <summary>
    /// 入力エラー（ValidationException）が 400 の VALIDATION_ERROR になり、問題のあった項目とその理由が details に入ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task ValidationException_Returns400_WithDetails()
    {
        var (status, body) = await GetAsync(_ => throw new ValidationException("items[0].quantity", "0より大きい値を指定してください"));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("VALIDATION_ERROR", body.GetProperty("code").GetString());
        Assert.Equal("items[0].quantity", body.GetProperty("details")[0].GetProperty("field").GetString());
        Assert.Equal("0より大きい値を指定してください", body.GetProperty("details")[0].GetProperty("reason").GetString());
    }

    /// <summary>
    /// 対象がないエラー（NotFoundException）が 404 の NOT_FOUND になり、details が省略されず空の配列で返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task NotFoundException_Returns404_WithEmptyDetailsArray()
    {
        var (status, body) = await GetAsync(_ => throw new NotFoundException());

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("NOT_FOUND", body.GetProperty("code").GetString());
        Assert.Equal(0, body.GetProperty("details").GetArrayLength()); // details は常に配列
    }

    /// <summary>
    /// 状態の矛盾（ConflictException）が 409 の CONFLICT になり、details に機械が読める理由コード（FOOD_INACTIVE）が入ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task ConflictException_Returns409_WithReasonCode()
    {
        var (status, body) = await GetAsync(_ => throw new ConflictException("無効な食材です", [new ErrorDetail("items[0].foodId", "FOOD_INACTIVE")]));

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("CONFLICT", body.GetProperty("code").GetString());
        Assert.Equal("FOOD_INACTIVE", body.GetProperty("details")[0].GetProperty("reason").GetString());
    }

    /// <summary>
    /// 想定外の例外が 500 の INTERNAL_ERROR になり、例外の内容（内部の情報）が利用者への返答に漏れないことを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task UnexpectedException_Returns500_WithoutLeakingInternals()
    {
        var (status, body) = await GetAsync(_ => throw new InvalidOperationException("内部の秘密: connection string"));

        Assert.Equal(HttpStatusCode.InternalServerError, status);
        Assert.Equal("INTERNAL_ERROR", body.GetProperty("code").GetString());
        Assert.DoesNotContain("秘密", body.GetRawText());
    }
}
