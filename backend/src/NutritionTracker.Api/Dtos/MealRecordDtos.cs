namespace NutritionTracker.Api.Dtos;

/// <summary>
/// 記録項目 1 行の入力。<paramref name="MenuId"/> か <paramref name="FoodId"/> のどちらか一方のみ指定する。
/// メニューの <paramref name="InputMode"/> は RATIO / SERVING / GRAM、食材は GRAM / UNIT。
/// </summary>
public sealed record MealRecordItemInput(long? MenuId, long? FoodId, long? UnitId, string? InputMode, decimal? InputValue);

/// <summary>API-14 / API-15 のリクエスト。未指定を検出するため nullable で受ける。</summary>
public sealed record MealRecordInput(DateOnly? EatenOn, string? MealType, string? Memo, IReadOnlyList<MealRecordItemInput>? Items);

/// <summary>記録項目。<paramref name="Nutrients"/> は保存済みスナップショット（4 桁に丸め済み）。</summary>
public sealed record MealRecordItemDto(
    long Id,
    long? MenuId,
    string? MenuName,
    long? FoodId,
    string? FoodName,
    string InputMode,
    decimal InputValue,
    decimal? PortionRatio,
    decimal? AmountG,
    IReadOnlyList<NutrientAmountDto> Nutrients);

public sealed record MealRecordDto(
    long Id,
    DateOnly EatenOn,
    string MealType,
    string? Memo,
    IReadOnlyList<MealRecordItemDto> Items);

/// <summary>API-13 のレスポンス。その日の記録を食事区分順に並べる。</summary>
public sealed record MealRecordDayDto(DateOnly Date, IReadOnlyList<MealRecordDto> Records);

/// <summary>メニューの記録が合計 100% を超えた警告（保存は妨げない。FR-034）。</summary>
public sealed record MealRecordWarningDto(string Code, long MenuId, decimal TotalPortionRatio, string Message);

/// <summary>API-14 / API-15 のレスポンス（記録 1 件 + 警告）。</summary>
public sealed record MealRecordSaveResultDto(
    long Id,
    DateOnly EatenOn,
    string MealType,
    string? Memo,
    IReadOnlyList<MealRecordItemDto> Items,
    IReadOnlyList<MealRecordWarningDto> Warnings);

public sealed record RecentMenuDto(long MenuId, string Name, string LastInputMode, decimal LastInputValue, DateOnly LastUsedOn);

public sealed record RecentFoodDto(long FoodId, string Name, string LastInputMode, decimal LastInputValue, long? UnitId, DateOnly LastUsedOn);

/// <summary>API-20 のレスポンス。直近の使用順で重複を除いた、メニューと食材。</summary>
public sealed record RecentItemsDto(IReadOnlyList<RecentMenuDto> Menus, IReadOnlyList<RecentFoodDto> Foods);
