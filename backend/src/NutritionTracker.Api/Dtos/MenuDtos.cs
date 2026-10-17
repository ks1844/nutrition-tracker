namespace NutritionTracker.Api.Dtos;

/// <summary>
/// 栄養素 1 つの量です。API のあちこち（メニュー・記録・プレビュー）で共通に使います。
/// </summary>
/// <param name="Code">栄養素コード（例: PROT）。</param>
/// <param name="Amount">量。</param>
/// <param name="IsIncomplete">未測定の食材を含み、実際より低めに出ている可能性があるとき true。</param>
public sealed record NutrientAmountDto(string Code, decimal Amount, bool IsIncomplete);

/// <summary>
/// メニューの食材 1 行の入力です。
/// </summary>
/// <param name="FoodId">食材の ID。</param>
/// <param name="UnitId">単位の ID。g で入力するときは null。</param>
/// <param name="Quantity">数量（単位が null なら g、あれば単位の数）。</param>
public sealed record MenuItemInput(long? FoodId, long? UnitId, decimal? Quantity);

/// <summary>
/// メニューの作成・更新・プレビュー（POST/PUT /api/menus、POST /api/menus/preview）のリクエストです。
/// 入力漏れを見つけられるよう、すべて null を許す型で受けます。
/// </summary>
/// <param name="Name">メニューの名前（作成・更新では必須、100 文字以内）。</param>
/// <param name="CookedOn">作った日。</param>
/// <param name="Memo">メモ（500 文字以内）。</param>
/// <param name="CookedTotalWeightG">完成後の全体の重さ（g、0 より大きい）。</param>
/// <param name="Servings">全体を何杯分とするか（0 より大きい）。</param>
/// <param name="ServingLabel">1 杯の呼び方（20 文字以内。省略すると「お椀」）。</param>
/// <param name="Items">食材（1 件以上）。</param>
public sealed record MenuInput(
    string? Name,
    DateOnly? CookedOn,
    string? Memo,
    decimal? CookedTotalWeightG,
    decimal? Servings,
    string? ServingLabel,
    IReadOnlyList<MenuItemInput>? Items);

/// <summary>
/// メニュー複製（POST /api/menus/{id}/duplicate）の任意のリクエストです。
/// </summary>
/// <param name="Name">新しい名前。省略すると「元の名前のコピー」。</param>
/// <param name="CookedOn">作った日。省略すると今日。</param>
public sealed record MenuDuplicateInput(string? Name, DateOnly? CookedOn);

/// <summary>
/// メニューの残りです（1 − 食べた割合の合計）。
/// </summary>
/// <param name="Ratio">残りの割合（1 = 全量）。</param>
/// <param name="Servings">残りを杯数に換算した値。</param>
/// <param name="IsOver">記録が合計 100% を超えているとき true。</param>
public sealed record RemainingDto(decimal Ratio, decimal Servings, bool IsOver);

/// <summary>
/// メニューの食材 1 行です。
/// </summary>
/// <param name="Id">メニュー食材の ID。</param>
/// <param name="FoodId">食材の ID。</param>
/// <param name="FoodName">食材の名前。</param>
/// <param name="UnitId">入力した単位の ID。g 入力のときは null。</param>
/// <param name="UnitName">入力した単位の名前。</param>
/// <param name="Quantity">入力した数量。</param>
/// <param name="AmountG">換算した重さ（g、廃棄前）。</param>
/// <param name="SortOrder">並び順。</param>
public sealed record MenuItemDto(
    long Id,
    long FoodId,
    string FoodName,
    long? UnitId,
    string? UnitName,
    decimal Quantity,
    decimal AmountG,
    int SortOrder);

/// <summary>
/// メニュー一覧（GET /api/menus）の 1 件です。
/// </summary>
/// <param name="Id">メニューの ID。</param>
/// <param name="Name">メニューの名前。</param>
/// <param name="CookedOn">作った日。</param>
/// <param name="CookedTotalWeightG">完成後の全体の重さ（g）。</param>
/// <param name="Servings">全体の杯数。</param>
/// <param name="ServingLabel">1 杯の呼び方。</param>
/// <param name="ServingWeightG">1 杯の重さ（g）。</param>
/// <param name="Remaining">残りの量。</param>
public sealed record MenuSummaryDto(
    long Id,
    string Name,
    DateOnly? CookedOn,
    decimal CookedTotalWeightG,
    decimal Servings,
    string ServingLabel,
    decimal ServingWeightG,
    RemainingDto Remaining);

/// <summary>
/// メニューの詳細です（作成・取得・更新のレスポンス）。
/// </summary>
/// <param name="Id">メニューの ID。</param>
/// <param name="Name">メニューの名前。</param>
/// <param name="CookedOn">作った日。</param>
/// <param name="Memo">メモ。</param>
/// <param name="CookedTotalWeightG">完成後の全体の重さ（g）。</param>
/// <param name="Servings">全体の杯数。</param>
/// <param name="ServingLabel">1 杯の呼び方。</param>
/// <param name="ServingWeightG">1 杯の重さ（g）。</param>
/// <param name="Items">食材。</param>
/// <param name="Nutrients">メニュー全体の栄養量。</param>
/// <param name="PerServing">1 杯あたりの栄養量。</param>
/// <param name="Remaining">残りの量。</param>
public sealed record MenuDetailDto(
    long Id,
    string Name,
    DateOnly? CookedOn,
    string? Memo,
    decimal CookedTotalWeightG,
    decimal Servings,
    string ServingLabel,
    decimal ServingWeightG,
    IReadOnlyList<MenuItemDto> Items,
    IReadOnlyList<NutrientAmountDto> Nutrients,
    IReadOnlyList<NutrientAmountDto> PerServing,
    RemainingDto Remaining);

/// <summary>
/// プレビューの食材 1 行です。栄養量は参考表示用（丸める前の値を小数 4 桁にしたもの）です。
/// </summary>
/// <param name="FoodId">食材の ID。</param>
/// <param name="FoodName">食材の名前。</param>
/// <param name="AmountG">換算した重さ（g、廃棄前）。</param>
/// <param name="EdibleG">廃棄を除いた可食部の重さ（g）。</param>
/// <param name="Nutrients">この食材の栄養量。</param>
public sealed record MenuPreviewItemDto(
    long FoodId,
    string FoodName,
    decimal AmountG,
    decimal EdibleG,
    IReadOnlyList<NutrientAmountDto> Nutrients);

/// <summary>
/// プレビューの警告です（保存はしないので、エラーではなく警告にします）。
/// </summary>
/// <param name="Code">警告コード（例: FOOD_INACTIVE）。</param>
/// <param name="FoodId">警告の対象の食材の ID。</param>
/// <param name="Message">利用者に見せる文言。</param>
public sealed record PreviewWarningDto(string Code, long FoodId, string Message);

/// <summary>
/// メニュー プレビュー（POST /api/menus/preview）のレスポンスです。
/// </summary>
/// <param name="Items">食材ごとの重さと栄養量。</param>
/// <param name="Nutrients">メニュー全体の栄養量。</param>
/// <param name="ServingWeightG">1 杯の重さ。完成後の重さと杯数がないときは null。</param>
/// <param name="PerServing">1 杯あたりの栄養量。完成後の重さと杯数がないときは null。</param>
/// <param name="Warnings">警告。</param>
public sealed record MenuPreviewDto(
    IReadOnlyList<MenuPreviewItemDto> Items,
    IReadOnlyList<NutrientAmountDto> Nutrients,
    decimal? ServingWeightG,
    IReadOnlyList<NutrientAmountDto>? PerServing,
    IReadOnlyList<PreviewWarningDto> Warnings);
