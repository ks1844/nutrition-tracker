namespace NutritionTracker.Api.Dtos;

/// <summary>
/// エラーの項目別の詳細です。
/// </summary>
/// <param name="Field">問題のあった項目（JSON パス。例: items[0].quantity）。</param>
/// <param name="Reason">問題の理由（409 では理由コード。例: FOOD_INACTIVE）。</param>
public sealed record ErrorDetail(string Field, string Reason);

/// <summary>
/// エラー時にすべての API が返す共通の形です（docs/09.API仕様.md §1.2）。
/// </summary>
/// <param name="Code">エラーコード（VALIDATION_ERROR / NOT_FOUND / CONFLICT / INTERNAL_ERROR）。</param>
/// <param name="Message">利用者に見せる日本語の文言。</param>
/// <param name="Details">項目ごとの詳細。なくても空の配列で返します。</param>
public sealed record ApiErrorResponse(string Code, string Message, IReadOnlyList<ErrorDetail> Details);
