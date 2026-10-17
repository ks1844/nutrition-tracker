namespace NutritionTracker.Api.Dtos;

/// <summary>API-05 のリクエスト。未指定を検出するため、すべて nullable で受ける。</summary>
public sealed record ProfileInput(
    string? Sex,
    DateOnly? BirthDate,
    decimal? HeightCm,
    decimal? WeightKg,
    int? ActivityLevel,
    string? Goal,
    int? EnergyTargetKcal);

public sealed record EnergyTargetDto(int Auto, int? Override, int Effective);

/// <summary>算出した目標（docs/06.スコアリング仕様.md §2）。</summary>
public sealed record ProfileTargetsDto(EnergyTargetDto EnergyKcal, decimal ProteinG, IReadOnlyList<NutrientTargetDto> Nutrients);

/// <summary>T栄養目標の 1 件（数値は Q-2 で転記するまで空）。</summary>
public sealed record NutrientTargetDto(string Code, string Kind, decimal Amount, string Basis);

/// <summary>API-04 / API-05 のレスポンス。</summary>
public sealed record ProfileDto(
    string Sex,
    DateOnly BirthDate,
    decimal HeightCm,
    decimal WeightKg,
    int ActivityLevel,
    string Goal,
    int? EnergyTargetKcal,
    ProfileTargetsDto Targets);
