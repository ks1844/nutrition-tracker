using NutritionTracker.Api.Dtos;

namespace NutritionTracker.Api.Exceptions;

/// <summary>API の業務例外。<see cref="ExceptionHandlingMiddleware"/> が {code, message, details[]} に変換する。</summary>
public abstract class ApiException(int statusCode, string code, string message, IReadOnlyList<ErrorDetail>? details = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;

    public IReadOnlyList<ErrorDetail> Details { get; } = details ?? [];
}

/// <summary>400。入力不正（形式・範囲・必須・列挙値）。</summary>
public sealed class ValidationException(IReadOnlyList<ErrorDetail> details)
    : ApiException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "入力内容に誤りがあります", details)
{
    public ValidationException(string field, string reason) : this([new ErrorDetail(field, reason)])
    {
    }
}

/// <summary>404。対象が存在しない（論理削除済みを含む）。</summary>
public sealed class NotFoundException(string message = "対象が見つかりません")
    : ApiException(StatusCodes.Status404NotFound, "NOT_FOUND", message);

/// <summary>409。現在の状態と矛盾する操作。<c>details[].reason</c> に機械可読の理由コード（例: FOOD_INACTIVE）。</summary>
public sealed class ConflictException(string message, IReadOnlyList<ErrorDetail> details)
    : ApiException(StatusCodes.Status409Conflict, "CONFLICT", message, details);
