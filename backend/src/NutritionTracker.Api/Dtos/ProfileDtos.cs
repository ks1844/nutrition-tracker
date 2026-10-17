namespace NutritionTracker.Api.Dtos;

/// <summary>
/// プロフィール登録・更新（PUT /api/profile）のリクエストです。
/// 入力漏れを見つけられるよう、すべて null を許す型で受けます。
/// </summary>
/// <param name="Sex">性別（male / female）。</param>
/// <param name="BirthDate">生年月日（過去の日付）。</param>
/// <param name="HeightCm">身長（cm）。</param>
/// <param name="WeightKg">体重（kg）。</param>
/// <param name="ActivityLevel">活動レベル（1〜4）。</param>
/// <param name="Goal">目的（skin / muscle / both）。</param>
/// <param name="EnergyTargetKcal">エネルギー目標の手入力（kcal）。自動算出を使うときは null。</param>
public sealed record ProfileInput(
    string? Sex,
    DateOnly? BirthDate,
    decimal? HeightCm,
    decimal? WeightKg,
    int? ActivityLevel,
    string? Goal,
    int? EnergyTargetKcal);

/// <summary>
/// エネルギー目標（kcal）です。
/// </summary>
/// <param name="Auto">プロフィールから自動算出した値。</param>
/// <param name="Override">利用者が手入力した値。なければ null。</param>
/// <param name="Effective">実際に使う値（手入力があればそれ、なければ自動算出）。</param>
public sealed record EnergyTargetDto(int Auto, int? Override, int Effective);

/// <summary>
/// プロフィールから算出した目標です（docs/06.スコアリング仕様.md §2）。
/// </summary>
/// <param name="EnergyKcal">エネルギー目標。</param>
/// <param name="ProteinG">タンパク質の目標（g）。</param>
/// <param name="Nutrients">性別・年齢に合う栄養目標の一覧（転記が済むまで空）。</param>
public sealed record ProfileTargetsDto(EnergyTargetDto EnergyKcal, decimal ProteinG, IReadOnlyList<NutrientTargetDto> Nutrients);

/// <summary>
/// 栄養目標 1 件です（食事摂取基準を転記するまでは返りません）。
/// </summary>
/// <param name="Code">栄養素コード。</param>
/// <param name="Kind">目標の種類（RDA / AI / DG_MIN / DG_MAX / UL）。</param>
/// <param name="Amount">目標の値。</param>
/// <param name="Basis">値の基準（PER_DAY / PER_KG_BW / PCT_ENERGY）。</param>
public sealed record NutrientTargetDto(string Code, string Kind, decimal Amount, string Basis);

/// <summary>
/// プロフィールの取得・更新（GET / PUT /api/profile）のレスポンスです。入力した内容と、算出した目標を返します。
/// </summary>
/// <param name="Sex">性別。</param>
/// <param name="BirthDate">生年月日。</param>
/// <param name="HeightCm">身長（cm）。</param>
/// <param name="WeightKg">体重（kg）。</param>
/// <param name="ActivityLevel">活動レベル。</param>
/// <param name="Goal">目的。</param>
/// <param name="EnergyTargetKcal">エネルギー目標の手入力。</param>
/// <param name="Targets">算出した目標。</param>
public sealed record ProfileDto(
    string Sex,
    DateOnly BirthDate,
    decimal HeightCm,
    decimal WeightKg,
    int ActivityLevel,
    string Goal,
    int? EnergyTargetKcal,
    ProfileTargetsDto Targets);
