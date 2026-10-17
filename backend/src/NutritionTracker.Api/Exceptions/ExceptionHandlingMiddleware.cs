using System.Text.Json;
using NutritionTracker.Api.Dtos;

namespace NutritionTracker.Api.Exceptions;

/// <summary>例外をエラー形式（docs/09.API仕様.md §1.2）に変換する。想定外の例外の詳細はログのみで、本文に出さない。</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    private static Task WriteAsync(HttpContext context, int status, ApiErrorResponse body)
    {
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
