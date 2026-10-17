namespace NutritionTracker.Api.Dtos;

/// <summary>API-03 の 1 件（docs/09.API仕様.md §3）。</summary>
public sealed record NutrientDto(string Code, string NameJa, string Unit, string Category, bool IsReference, int DisplayOrder);
