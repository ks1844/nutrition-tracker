namespace NutritionTracker.Api.Dtos;

/// <summary>
/// 食材の換算単位です（例: にんじん 1 本 = 150g）。
/// </summary>
/// <param name="Id">換算単位の ID。</param>
/// <param name="UnitName">単位の名前（本・大さじ など）。</param>
/// <param name="GramsPerUnit">1 単位あたりの重さ（g、廃棄部込み）。</param>
/// <param name="IsDefault">既定の単位か。</param>
public sealed record FoodUnitDto(long Id, string UnitName, decimal GramsPerUnit, bool IsDefault);

/// <summary>
/// 食材検索（GET /api/foods）で返す食材 1 件です。名前・読み・廃棄率・換算単位を持ちます。
/// </summary>
/// <param name="Id">食材の ID。</param>
/// <param name="FoodCode">食品コード。</param>
/// <param name="Name">画面に表示する名前。</param>
/// <param name="NameKana">読み仮名。</param>
/// <param name="FoodGroup">食品群。</param>
/// <param name="RefuseRatePct">廃棄率（%）。</param>
/// <param name="Units">換算単位の一覧。</param>
public sealed record FoodSummaryDto(
    long Id,
    string FoodCode,
    string Name,
    string NameKana,
    string? FoodGroup,
    decimal RefuseRatePct,
    IReadOnlyList<FoodUnitDto> Units);

/// <summary>
/// 食材検索の結果です。
/// </summary>
/// <param name="Items">指定した件数分の食材。</param>
/// <param name="Total">条件に合う食材の総件数（ページ分けする前の件数）。</param>
public sealed record FoodSearchResultDto(IReadOnlyList<FoodSummaryDto> Items, int Total);

/// <summary>
/// 食材の栄養素 1 つ分の含有量です。
/// </summary>
/// <param name="Code">栄養素コード。</param>
/// <param name="Per100g">可食部 100g あたりの含有量。null は未測定（0 ではありません）。</param>
/// <param name="IsTrace">成分表で微量（Tr）とされた値か。</param>
/// <param name="SourceNote">出典の補足。</param>
public sealed record FoodNutrientDto(string Code, decimal? Per100g, bool IsTrace, string? SourceNote);

/// <summary>
/// 食材詳細（GET /api/foods/{id}）です。全栄養素の含有量・換算単位・別名・出典を返します。
/// </summary>
/// <param name="Id">食材の ID。</param>
/// <param name="FoodCode">食品コード。</param>
/// <param name="Name">画面に表示する名前。</param>
/// <param name="OfficialName">成分表上の正式名称。</param>
/// <param name="NameKana">読み仮名。</param>
/// <param name="FoodGroup">食品群。</param>
/// <param name="RefuseRatePct">廃棄率（%）。</param>
/// <param name="SourceVersion">成分値の出典の版。</param>
/// <param name="IsActive">有効か（無効な食材も詳細は取得できます）。</param>
/// <param name="Aliases">別名の一覧。</param>
/// <param name="Units">換算単位の一覧。</param>
/// <param name="Nutrients">栄養素ごとの含有量。</param>
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
