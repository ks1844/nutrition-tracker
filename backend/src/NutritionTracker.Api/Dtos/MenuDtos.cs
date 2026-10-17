namespace NutritionTracker.Api.Dtos;

/// <summary>栄養素 1 つの量。<paramref name="IsIncomplete"/> は「未測定の食材を含む」印。</summary>
public sealed record NutrientAmountDto(string Code, decimal Amount, bool IsIncomplete);

/// <summary>メニューの食材 1 行の入力。<paramref name="UnitId"/> が null なら <paramref name="Quantity"/> は g。</summary>
public sealed record MenuItemInput(long? FoodId, long? UnitId, decimal? Quantity);

/// <summary>API-07 / API-09 / API-12 のリクエスト。未指定を検出するため nullable で受ける。</summary>
public sealed record MenuInput(
    string? Name,
    DateOnly? CookedOn,
    string? Memo,
    decimal? CookedTotalWeightG,
    decimal? Servings,
    string? ServingLabel,
    IReadOnlyList<MenuItemInput>? Items);

/// <summary>残量 = 1 − Σ食べた割合。<paramref name="IsOver"/> は記録が 100% を超えている。</summary>
public sealed record RemainingDto(decimal Ratio, decimal Servings, bool IsOver);

public sealed record MenuItemDto(
    long Id,
    long FoodId,
    string FoodName,
    long? UnitId,
    string? UnitName,
    decimal Quantity,
    decimal AmountG,
    int SortOrder);

/// <summary>API-06 の 1 件。</summary>
public sealed record MenuSummaryDto(
    long Id,
    string Name,
    DateOnly? CookedOn,
    decimal CookedTotalWeightG,
    decimal Servings,
    string ServingLabel,
    decimal ServingWeightG,
    RemainingDto Remaining);

/// <summary>API-07 / API-08 / API-09 のレスポンス。</summary>
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
