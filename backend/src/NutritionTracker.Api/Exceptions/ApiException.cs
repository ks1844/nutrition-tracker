using NutritionTracker.Api.Dtos;

namespace NutritionTracker.Api.Exceptions;

/// <summary>
/// API が利用者に返すエラーの基本クラスです。
/// スローすると ExceptionHandlingMiddleware が捕まえて、{ code, message, details[] } の形の JSON と HTTP ステータスに変換します。
/// </summary>
/// <param name="statusCode">返す HTTP ステータス。</param>
/// <param name="code">機械が読むエラーコード（例: VALIDATION_ERROR）。</param>
/// <param name="message">利用者に見せる日本語の文言。</param>
/// <param name="details">項目ごとの詳細。なければ空。</param>
public abstract class ApiException(int statusCode, string code, string message, IReadOnlyList<ErrorDetail>? details = null)
    : Exception(message)
{
    /// <summary>
    /// 返す HTTP ステータス。
    /// </summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>
    /// 機械が読むエラーコード。
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// 項目ごとの詳細（どの項目がなぜ問題か）。なければ空の一覧。
    /// </summary>
    public IReadOnlyList<ErrorDetail> Details { get; } = details ?? [];
}

/// <summary>
/// 入力が正しくないとき（形式・範囲・必須・選択肢）の 400 エラーです。項目ごとの理由を details に入れます。
/// </summary>
/// <param name="details">問題のあった項目と理由の一覧。</param>
public sealed class ValidationException(IReadOnlyList<ErrorDetail> details)
    : ApiException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "入力内容に誤りがあります", details)
{
    /// <summary>
    /// 1 項目だけの入力エラーを作ります。
    /// </summary>
    /// <param name="field">問題のあった項目（JSON パス。例: items[0].quantity）。</param>
    /// <param name="reason">問題の理由。</param>
    public ValidationException(string field, string reason) : this([new ErrorDetail(field, reason)])
    {
    }
}

/// <summary>
/// 対象が存在しないとき（論理削除済みを含む）の 404 エラーです。
/// </summary>
/// <param name="message">利用者に見せる文言。</param>
public sealed class NotFoundException(string message = "対象が見つかりません")
    : ApiException(StatusCodes.Status404NotFound, "NOT_FOUND", message);

/// <summary>
/// いまの状態と矛盾していて操作できないとき（例: 無効な食材を使おうとした）の 409 エラーです。
/// details の reason に、機械が読める理由コード（例: FOOD_INACTIVE）を入れます。
/// </summary>
/// <param name="message">利用者に見せる文言。</param>
/// <param name="details">矛盾した項目と理由コードの一覧。</param>
public sealed class ConflictException(string message, IReadOnlyList<ErrorDetail> details)
    : ApiException(StatusCodes.Status409Conflict, "CONFLICT", message, details);
