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
/// 栄養素 1 つの日次の状態。<paramref name="Status"/> は DEFICIENT / ACHIEVED / OVER_UL / NO_TARGET。
/// <paramref name="AchievementRate"/> は下限型の目標がある場合だけ（100% で頭打ち）。
/// </summary>
public sealed record DailyNutrientDto(
    string Code,
    string NameJa,
    string Unit,
    decimal Intake,
    NutrientTargetViewDto Target,
    decimal? AchievementRate,
    string Status,
    bool IsIncomplete,
    bool IsReference);

public sealed record RemainingMenuDto(long MenuId, string Name, decimal RemainingRatio, decimal RemainingServings, string ServingLabel);

/// <summary>画面に注記を出すための通知。Code は INCOMPLETE_NUTRIENT など。</summary>
public sealed record DashboardNoticeDto(string Code, IReadOnlyList<string> NutrientCodes);

public sealed record DailyDashboardDto(
    DateOnly Date,
    bool HasRecords,
    EnergySummaryDto Energy,
    PfcSummaryDto Pfc,
    ScoresDto Scores,
    IReadOnlyList<DailyNutrientDto> Nutrients,
    IReadOnlyList<RemainingMenuDto> RemainingMenus,
    IReadOnlyList<DashboardNoticeDto> Notices);
