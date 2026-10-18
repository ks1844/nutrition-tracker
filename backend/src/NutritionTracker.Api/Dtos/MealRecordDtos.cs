namespace NutritionTracker.Api.Dtos;

/// <summary>
/// 記録項目 1 行の入力です。メニューか食材のどちらか一方だけを指定します。
/// メニューの入力方式は RATIO（割合）/ SERVING（杯数）/ GRAM（g）、食材は GRAM（g）/ UNIT（単位）です。
/// </summary>
/// <param name="MenuId">食べたメニューの ID。</param>
/// <param name="FoodId">食べた食材の ID。</param>
/// <param name="UnitId">単位の ID（食材を UNIT で入力するときだけ必須）。</param>
/// <param name="InputMode">入力方式。</param>
/// <param name="InputValue">入力した値（0 より大きい）。</param>
public sealed record MealRecordItemInput(long? MenuId, long? FoodId, long? UnitId, string? InputMode, decimal? InputValue);

/// <summary>
/// 食事記録の作成・更新（POST/PUT /api/meal-records）のリクエストです。入力漏れを見つけられるよう、すべて null を許す型で受けます。
/// </summary>
/// <param name="EatenOn">食べた日（必須）。</param>
/// <param name="MealType">食事区分（breakfast / lunch / dinner / snack）。</param>
/// <param name="Memo">メモ（500 文字以内）。</param>
/// <param name="Items">食べたもの（1 件以上）。</param>
public sealed record MealRecordInput(DateOnly? EatenOn, string? MealType, string? Memo, IReadOnlyList<MealRecordItemInput>? Items);

/// <summary>
/// 記録項目 1 つです。栄養量は記録時に保存した値（小数 4 桁に丸め済み）です。
/// </summary>
/// <param name="Id">項目の ID。</param>
/// <param name="MenuId">食べたメニューの ID。</param>
/// <param name="MenuName">メニューの名前。</param>
/// <param name="FoodId">食べた食材の ID。</param>
/// <param name="FoodName">食材の名前。</param>
/// <param name="InputMode">入力方式。</param>
/// <param name="InputValue">入力した値。</param>
/// <param name="PortionRatio">メニューを食べた割合。単品の食材では null。</param>
/// <param name="AmountG">食べた重さ（g）。</param>
/// <param name="Nutrients">記録した栄養量。</param>
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

/// <summary>
/// 食事記録 1 食分です。
/// </summary>
/// <param name="Id">食事記録の ID。</param>
/// <param name="EatenOn">食べた日。</param>
/// <param name="MealType">食事区分。</param>
/// <param name="Memo">メモ。</param>
/// <param name="Items">食べたもの。</param>
public sealed record MealRecordDto(
    long Id,
    DateOnly EatenOn,
    string MealType,
    string? Memo,
    IReadOnlyList<MealRecordItemDto> Items);

/// <summary>
/// 日別の食事記録（GET /api/meal-records）のレスポンスです。
/// </summary>
/// <param name="Date">日付。</param>
/// <param name="Records">その日の記録（朝・昼・夕・間食の順）。</param>
public sealed record MealRecordDayDto(DateOnly Date, IReadOnlyList<MealRecordDto> Records);

/// <summary>
/// メニューの記録が合計 100% を超えたときの警告です。保存は妨げません。
/// </summary>
/// <param name="Code">警告コード（MENU_OVER_100PCT）。</param>
/// <param name="MenuId">対象のメニューの ID。</param>
/// <param name="TotalPortionRatio">そのメニューの全記録の食べた割合の合計。</param>
/// <param name="Message">利用者に見せる文言。</param>
public sealed record MealRecordWarningDto(string Code, long MenuId, decimal TotalPortionRatio, string Message);

/// <summary>
/// 食事記録の作成・更新のレスポンスです。作った記録と警告を返します。
/// </summary>
/// <param name="Id">食事記録の ID。</param>
/// <param name="EatenOn">食べた日。</param>
/// <param name="MealType">食事区分。</param>
/// <param name="Memo">メモ。</param>
/// <param name="Items">食べたもの。</param>
/// <param name="Warnings">警告。</param>
public sealed record MealRecordSaveResultDto(
    long Id,
    DateOnly EatenOn,
    string MealType,
    string? Memo,
    IReadOnlyList<MealRecordItemDto> Items,
    IReadOnlyList<MealRecordWarningDto> Warnings);

/// <summary>
/// 記録のコピー（POST /api/meal-records/copy）のリクエストです。
/// </summary>
/// <param name="FromDate">コピー元の日。</param>
/// <param name="ToDate">コピー先の日。</param>
/// <param name="AllowExisting">コピー先にすでに記録があってもコピーするか（false なら 409）。</param>
public sealed record MealRecordCopyInput(DateOnly? FromDate, DateOnly? ToDate, bool? AllowExisting);

/// <summary>
/// コピーできなかった項目です。
/// </summary>
/// <param name="MenuId">スキップしたメニューの ID。</param>
/// <param name="FoodId">スキップした食材の ID。</param>
/// <param name="Reason">理由（MENU_DELETED メニューが削除済み / FOOD_INACTIVE 食材が無効）。</param>
public sealed record SkippedItemDto(long? MenuId, long? FoodId, string Reason);

/// <summary>
/// 記録のコピーのレスポンスです。
/// </summary>
/// <param name="Records">作った記録。</param>
/// <param name="Skipped">スキップした項目。</param>
public sealed record MealRecordCopyResultDto(IReadOnlyList<MealRecordDto> Records, IReadOnlyList<SkippedItemDto> Skipped);

/// <summary>
/// 最近使ったメニュー 1 件です。
/// </summary>
/// <param name="MenuId">メニューの ID。</param>
/// <param name="Name">メニューの名前。</param>
/// <param name="LastInputMode">最後に使ったときの入力方式。</param>
/// <param name="LastInputValue">最後に使ったときの入力値。</param>
/// <param name="LastUsedOn">最後に使った日。</param>
public sealed record RecentMenuDto(long MenuId, string Name, string LastInputMode, decimal LastInputValue, DateOnly LastUsedOn);

/// <summary>
/// 最近使った食材 1 件です。
/// </summary>
/// <param name="FoodId">食材の ID。</param>
/// <param name="Name">食材の名前。</param>
/// <param name="LastInputMode">最後に使ったときの入力方式。</param>
/// <param name="LastInputValue">最後に使ったときの入力値。</param>
/// <param name="UnitId">最後に使った単位の ID。g 入力のときは null。</param>
/// <param name="LastUsedOn">最後に使った日。</param>
public sealed record RecentFoodDto(long FoodId, string Name, string LastInputMode, decimal LastInputValue, long? UnitId, DateOnly LastUsedOn);

/// <summary>
/// 最近使った食材・メニュー（GET /api/recent-items）のレスポンスです。
/// </summary>
/// <param name="Menus">最近使ったメニュー。</param>
/// <param name="Foods">最近使った食材。</param>
public sealed record RecentItemsDto(IReadOnlyList<RecentMenuDto> Menus, IReadOnlyList<RecentFoodDto> Foods);
