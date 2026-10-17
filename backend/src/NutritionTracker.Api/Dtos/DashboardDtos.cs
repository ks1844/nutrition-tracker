using System.Text.Json.Serialization;

namespace NutritionTracker.Api.Dtos;

public sealed record EnergySummaryDto(int IntakeKcal, int TargetKcal, decimal AchievementRate);

/// <summary>PFC 比の整数表示（合計が 100 になる最大剰余法。暫定）。</summary>
public sealed record PfcRatioPctDto(int P, int F, int C);

public sealed record PfcSummaryDto(decimal ProteinG, decimal FatG, decimal CarbG, PfcRatioPctDto RatioPct);

/// <summary>
/// スコア 1 つ。<paramref name="Value"/> が null のときは、記録が無い・目標が無い・データ不足（<paramref name="InsufficientData"/>）のいずれか。
/// <paramref name="Band"/> は GOOD / FAIR / LOW。
/// </summary>
public sealed record ScoreDto(int? Value, string? Band, bool InsufficientData, bool HasIncomplete);

public sealed record ScoresDto(ScoreDto Beauty, ScoreDto Muscle, ScoreDto Base);

/// <summary>目標の表示用。<paramref name="Kind"/>/<paramref name="Amount"/> は下限型（RDA/AI/DG_MIN、エネルギー・タンパク質は CALCULATED）。</summary>
public sealed record NutrientTargetViewDto(string? Kind, decimal? Min, decimal? Max, decimal? Ul, string Basis, decimal? Amount);

/// <summary>
/// 栄養素 1 つの状態（日次・7 日平均で共通）。<paramref name="Status"/> は DEFICIENT / ACHIEVED / OVER_UL / NO_TARGET。
/// <paramref name="AchievementRate"/> は下限型の目標がある場合だけ（100% で頭打ち）。
/// <paramref name="UlExceededDays"/> は 7 日平均のときだけ付く、期間内に上限を超えた日数。
/// </summary>
public sealed record NutrientStatusDto(
    string Code,
    string NameJa,
    string Unit,
    decimal Intake,
    NutrientTargetViewDto Target,
    decimal? AchievementRate,
    string Status,
    bool IsIncomplete,
    bool IsReference,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? UlExceededDays = null);

public sealed record RemainingMenuDto(long MenuId, string Name, decimal RemainingRatio, decimal RemainingServings, string ServingLabel);

/// <summary>画面に注記を出すための通知。Code は INCOMPLETE_NUTRIENT など。</summary>
public sealed record DashboardNoticeDto(string Code, IReadOnlyList<string> NutrientCodes);

public sealed record DailyDashboardDto(
    DateOnly Date,
    bool HasRecords,
    EnergySummaryDto Energy,
    PfcSummaryDto Pfc,
    ScoresDto Scores,
    IReadOnlyList<NutrientStatusDto> Nutrients,
    IReadOnlyList<RemainingMenuDto> RemainingMenus,
    IReadOnlyList<DashboardNoticeDto> Notices);

/// <summary>7 日平均のエネルギー（kcal は整数）。</summary>
public sealed record AverageEnergyDto(int IntakeKcal, int TargetKcal);

/// <summary>
/// 記録のある日だけで平均した値。記録日が 0 のときは <paramref name="Energy"/> と <paramref name="Pfc"/> が null、
/// <paramref name="Nutrients"/> は空。
/// </summary>
public sealed record WeeklyAveragesDto(AverageEnergyDto? Energy, PfcSummaryDto? Pfc, IReadOnlyList<NutrientStatusDto> Nutrients);

public sealed record SeriesScoresDto(int? Beauty, int? Muscle, int? Base);

/// <summary>窓の 1 日。記録の無い日は <paramref name="HasRecords"/> が false で、kcal とスコアは null（0 にしない）。</summary>
public sealed record SeriesPointDto(DateOnly Date, bool HasRecords, int? IntakeKcal, SeriesScoresDto? Scores);

public sealed record WeeklyDashboardDto(
    DateOnly EndDate,
    DateOnly WindowStart,
    int RecordDays,
    bool InsufficientData,
    WeeklyAveragesDto Averages,
    ScoresDto Scores,
    IReadOnlyList<SeriesPointDto> Series,
    IReadOnlyList<RemainingMenuDto> RemainingMenus,
    IReadOnlyList<DashboardNoticeDto> Notices);

/// <summary>メニューの食材内訳（参考値）。メニューの<b>現在の構成</b>から按分する。</summary>
public sealed record ContributionChildDto(long FoodId, string Name, decimal Amount);

/// <summary>寄与した記録項目（メニューまたは単品食材）。<paramref name="Type"/> は MENU / FOOD。</summary>
public sealed record ContributionItemDto(
    string Type,
    long Id,
    string Name,
    decimal Amount,
    decimal SharePct,
    IReadOnlyList<ContributionChildDto> Children);

/// <summary>API-19 のレスポンス。<paramref name="Items"/> は寄与の大きい順。</summary>
public sealed record ContributionsDto(
    string NutrientCode,
    DateOnly From,
    DateOnly To,
    decimal TotalAmount,
    bool IsIncomplete,
    IReadOnlyList<ContributionItemDto> Items);
