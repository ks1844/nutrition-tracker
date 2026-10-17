namespace NutritionTracker.Api.Dtos;

/// <summary>エラーの項目別の詳細。<paramref name="Field"/> は JSON パス（例: items[0].quantity）。</summary>
public sealed record ErrorDetail(string Field, string Reason);

/// <summary>エラー形式（docs/09.API仕様.md §1.2）。<c>details</c> は常に配列（空でも省略しない）。</summary>
public sealed record ApiErrorResponse(string Code, string Message, IReadOnlyList<ErrorDetail> Details);
