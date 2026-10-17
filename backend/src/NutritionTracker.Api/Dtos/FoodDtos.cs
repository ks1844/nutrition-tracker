namespace NutritionTracker.Api.Dtos;

/// <summary>換算単位。<paramref name="GramsPerUnit"/> は廃棄部込みの g。</summary>
public sealed record FoodUnitDto(long Id, string UnitName, decimal GramsPerUnit, bool IsDefault);

/// <summary>API-01 の 1 件。</summary>
public sealed record FoodSummaryDto(
    long Id,
    string FoodCode,
    string Name,
    string NameKana,
    string? FoodGroup,
    decimal RefuseRatePct,
    IReadOnlyList<FoodUnitDto> Units);

public sealed record FoodSearchResultDto(IReadOnlyList<FoodSummaryDto> Items, int Total);

/// <summary><paramref name="Per100g"/> が null = 未測定（0 ではない）。</summary>
public sealed record FoodNutrientDto(string Code, decimal? Per100g, bool IsTrace, string? SourceNote);

/// <summary>API-02。</summary>
public sealed record FoodDetailDto(
    long Id,
    string FoodCode,
    string Name,
    string? OfficialName,
    string NameKana,
    string? FoodGroup,
    decimal RefuseRatePct,
    string SourceVersion,
    bool IsActive,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<FoodUnitDto> Units,
    IReadOnlyList<FoodNutrientDto> Nutrients);
