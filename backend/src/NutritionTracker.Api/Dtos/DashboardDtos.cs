using System.Text.Json.Serialization;

namespace NutritionTracker.Api.Dtos;

/// <summary>
/// その日のエネルギーのまとめです。
/// </summary>
/// <param name="IntakeKcal">摂取量（kcal、整数）。</param>
/// <param name="TargetKcal">目標（kcal）。</param>
/// <param name="AchievementRate">達成率（摂取 ÷ 目標。頭打ちしない。小数 6 桁）。</param>
public sealed record EnergySummaryDto(int IntakeKcal, int TargetKcal, decimal AchievementRate);

/// <summary>
/// PFC 比の整数表示です（合計がちょうど 100 になるよう最大剰余法で丸めます。暫定）。
/// </summary>
/// <param name="P">タンパク質の割合（%）。</param>
/// <param name="F">脂質の割合（%）。</param>
/// <param name="C">炭水化物の割合（%）。</param>
public sealed record PfcRatioPctDto(int P, int F, int C);

/// <summary>
/// タンパク質・脂質・炭水化物の量と PFC 比のまとめです。
/// </summary>
/// <param name="ProteinG">タンパク質（g、小数 1 桁）。</param>
/// <param name="FatG">脂質（g、小数 1 桁）。</param>
/// <param name="CarbG">炭水化物（g、小数 1 桁）。</param>
/// <param name="RatioPct">PFC 比（整数表示）。</param>
public sealed record PfcSummaryDto(decimal ProteinG, decimal FatG, decimal CarbG, PfcRatioPctDto RatioPct);

/// <summary>
/// スコア 1 つです。Value が null のときは、記録がない・目標がなくて出せない・データ不足のいずれかです。
/// </summary>
/// <param name="Value">スコア（整数）。</param>
/// <param name="Band">区分（GOOD / FAIR / LOW）。</param>
/// <param name="InsufficientData">記録日が少なくてスコアを出さなかったとき true。</param>
/// <param name="HasIncomplete">未測定の食材を含み、実際より低めに出ている可能性があるとき true。</param>
public sealed record ScoreDto(int? Value, string? Band, bool InsufficientData, bool HasIncomplete);

/// <summary>
/// 3 つのスコアです。
/// </summary>
/// <param name="Beauty">美肌スコア。</param>
/// <param name="Muscle">筋肉スコア。</param>
/// <param name="Base">基礎スコア。</param>
public sealed record ScoresDto(ScoreDto Beauty, ScoreDto Muscle, ScoreDto Base);

/// <summary>
/// 栄養素の目標の表示用です。Kind と Amount は下限型の目標（エネルギー・タンパク質は CALCULATED）です。
/// </summary>
/// <param name="Kind">下限型の目標の種類。</param>
/// <param name="Min">目標量の下限（DG_MIN）。</param>
/// <param name="Max">目標量の上限（DG_MAX）。</param>
/// <param name="Ul">耐容上限量（UL）。</param>
/// <param name="Basis">値の基準（常に PER_DAY）。</param>
/// <param name="Amount">下限型の目標量。</param>
public sealed record NutrientTargetViewDto(string? Kind, decimal? Min, decimal? Max, decimal? Ul, string Basis, decimal? Amount);

/// <summary>
/// 栄養素 1 つの状態（日次・7 日平均で共通）です。
/// Status は DEFICIENT（不足）/ ACHIEVED（達成）/ OVER_UL（上限超過）/ NO_TARGET（目標なし）、AchievementRate は下限型の目標があるときだけ（100% で頭打ち）です。
/// </summary>
/// <param name="Code">栄養素コード。</param>
/// <param name="NameJa">日本語名。</param>
/// <param name="Unit">単位。</param>
/// <param name="Intake">摂取量（kcal は整数、その他は小数 1 桁）。</param>
/// <param name="Target">目標。</param>
/// <param name="AchievementRate">達成率。下限型の目標がなければ null。</param>
/// <param name="Status">状態。</param>
/// <param name="IsIncomplete">未測定の食材を含むか（参考値の栄養素では常に false）。</param>
/// <param name="IsReference">参考値の栄養素か（スコアには使わない）。</param>
/// <param name="UlExceededDays">期間内に上限を超えた日数。7 日平均のときだけ付きます。</param>
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

/// <summary>
/// まだ残りがあるメニューです（ホームの残量カード用）。
/// </summary>
/// <param name="MenuId">メニューの ID。</param>
/// <param name="Name">メニューの名前。</param>
/// <param name="RemainingRatio">残りの割合。</param>
/// <param name="RemainingServings">残りの杯数。</param>
/// <param name="ServingLabel">1 杯の呼び方。</param>
public sealed record RemainingMenuDto(long MenuId, string Name, decimal RemainingRatio, decimal RemainingServings, string ServingLabel);

/// <summary>
/// 画面に注記を出すための通知です。
/// </summary>
/// <param name="Code">通知コード（INCOMPLETE_NUTRIENT: 未測定の食材を含む栄養素がある）。</param>
/// <param name="NutrientCodes">対象の栄養素コード。</param>
public sealed record DashboardNoticeDto(string Code, IReadOnlyList<string> NutrientCodes);

/// <summary>
/// 日次ダッシュボード（GET /api/dashboard/daily）のレスポンスです。
/// </summary>
/// <param name="Date">集計した日。</param>
/// <param name="HasRecords">その日に記録があるか。</param>
/// <param name="Energy">エネルギー。</param>
/// <param name="Pfc">PFC。</param>
/// <param name="Scores">3 つのスコア（その日の進捗）。</param>
/// <param name="Nutrients">栄養素ごとの状態。</param>
/// <param name="RemainingMenus">残りのあるメニュー。</param>
/// <param name="Notices">注記。</param>
public sealed record DailyDashboardDto(
    DateOnly Date,
    bool HasRecords,
    EnergySummaryDto Energy,
    PfcSummaryDto Pfc,
    ScoresDto Scores,
    IReadOnlyList<NutrientStatusDto> Nutrients,
    IReadOnlyList<RemainingMenuDto> RemainingMenus,
    IReadOnlyList<DashboardNoticeDto> Notices);

/// <summary>
/// 7 日平均のエネルギーです。
/// </summary>
/// <param name="IntakeKcal">平均の摂取量（kcal、整数）。</param>
/// <param name="TargetKcal">目標（kcal）。</param>
public sealed record AverageEnergyDto(int IntakeKcal, int TargetKcal);

/// <summary>
/// 記録のある日だけで平均した値です。記録日が 0 のときは Energy と Pfc が null、Nutrients が空です。
/// </summary>
/// <param name="Energy">エネルギー。</param>
/// <param name="Pfc">PFC。</param>
/// <param name="Nutrients">栄養素ごとの状態。</param>
public sealed record WeeklyAveragesDto(AverageEnergyDto? Energy, PfcSummaryDto? Pfc, IReadOnlyList<NutrientStatusDto> Nutrients);

/// <summary>
/// 推移グラフ用の、ある日のスコア（整数）です。
/// </summary>
/// <param name="Beauty">美肌スコア。</param>
/// <param name="Muscle">筋肉スコア。</param>
/// <param name="Base">基礎スコア。</param>
public sealed record SeriesScoresDto(int? Beauty, int? Muscle, int? Base);

/// <summary>
/// 7 日間の推移の 1 日分です。記録のない日は HasRecords が false で、kcal とスコアは null です（0 にはしません）。
/// </summary>
/// <param name="Date">日付。</param>
/// <param name="HasRecords">その日に記録があるか。</param>
/// <param name="IntakeKcal">摂取量（kcal）。</param>
/// <param name="Scores">その日のスコア。</param>
public sealed record SeriesPointDto(DateOnly Date, bool HasRecords, int? IntakeKcal, SeriesScoresDto? Scores);

/// <summary>
/// 直近 7 日ダッシュボード（GET /api/dashboard/weekly）のレスポンスです。
/// </summary>
/// <param name="EndDate">7 日間の最後の日。</param>
/// <param name="WindowStart">7 日間の最初の日。</param>
/// <param name="RecordDays">記録のある日数。</param>
/// <param name="InsufficientData">記録日が少なくてスコアを出さないとき true。</param>
/// <param name="Averages">平均。</param>
/// <param name="Scores">3 つのスコア（7 日平均）。</param>
/// <param name="Series">日ごとの推移（7 件）。</param>
/// <param name="RemainingMenus">残りのあるメニュー。</param>
/// <param name="Notices">注記。</param>
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

/// <summary>
/// メニューの食材の内訳（参考値）です。メニューの現在の構成から按分します。
/// </summary>
/// <param name="FoodId">食材の ID。</param>
/// <param name="Name">食材の名前。</param>
/// <param name="Amount">寄与した量。</param>
public sealed record ContributionChildDto(long FoodId, string Name, decimal Amount);

/// <summary>
/// 寄与した記録項目（メニューまたは単品の食材）です。
/// </summary>
/// <param name="Type">種類（MENU / FOOD）。</param>
/// <param name="Id">メニューまたは食材の ID。</param>
/// <param name="Name">名前。</param>
/// <param name="Amount">寄与した量。</param>
/// <param name="SharePct">全体に占める割合（%）。</param>
/// <param name="Children">メニューの食材の内訳。単品の食材は空。</param>
public sealed record ContributionItemDto(
    string Type,
    long Id,
    string Name,
    decimal Amount,
    decimal SharePct,
    IReadOnlyList<ContributionChildDto> Children);

/// <summary>
/// 栄養素の寄与食材（GET /api/nutrients/{code}/contributions）のレスポンスです。Items は寄与の大きい順です。
/// </summary>
/// <param name="NutrientCode">栄養素コード。</param>
/// <param name="From">期間の開始日。</param>
/// <param name="To">期間の終了日。</param>
/// <param name="TotalAmount">期間の合計。</param>
/// <param name="IsIncomplete">未測定の食材を含むか。</param>
/// <param name="Items">寄与した項目。</param>
public sealed record ContributionsDto(
    string NutrientCode,
    DateOnly From,
    DateOnly To,
    decimal TotalAmount,
    bool IsIncomplete,
    IReadOnlyList<ContributionItemDto> Items);
