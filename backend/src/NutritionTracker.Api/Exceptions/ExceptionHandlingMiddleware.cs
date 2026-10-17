using System.Text.Json;
using NutritionTracker.Api.Dtos;

namespace NutritionTracker.Api.Exceptions;

/// <summary>
/// API で起きた例外を、統一したエラー形式の JSON に変換します（docs/09.API仕様.md §1.2）。
/// 想定外の例外の詳細はログにだけ残し、利用者への返答には出しません。
/// </summary>
/// <param name="next">次の処理。</param>
/// <param name="logger">ログの出力先。</param>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>
    /// エラー本文を JSON にするときの設定（項目名は camelCase）。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 次の処理を実行し、例外が起きたらエラー応答に変換します。
    /// 業務例外はその内容のまま、リクエストの形式不正は 400、想定外の例外は 500（詳細なし）で返します。
    /// </summary>
    /// <param name="context">いまのリクエスト・レスポンス。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException e)
        {
            await WriteAsync(context, e.StatusCode, new ApiErrorResponse(e.Code, e.Message, e.Details));
        }
        catch (Exception e) when (e is BadHttpRequestException or JsonException)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest,
                new ApiErrorResponse("VALIDATION_ERROR", "リクエストの形式が正しくありません", []));
        }
        catch (Exception e)
        {
            logger.LogError(e, "想定外のエラー: {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                new ApiErrorResponse("INTERNAL_ERROR", "サーバーでエラーが発生しました", []));
        }
    }

    /// <summary>
    /// エラーの JSON をレスポンスに書き出します。
    /// </summary>
    /// <param name="context">いまのリクエスト・レスポンス。</param>
    /// <param name="status">HTTP ステータス。</param>
    /// <param name="body">エラー本文。</param>
    /// <returns>書き出しの完了を表すタスク。</returns>
    private static Task WriteAsync(HttpContext context, int status, ApiErrorResponse body)
    {
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
