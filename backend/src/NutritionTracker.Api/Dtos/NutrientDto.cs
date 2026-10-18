namespace NutritionTracker.Api.Dtos;

/// <summary>
/// 栄養素一覧（GET /api/nutrients）の 1 件です。栄養素の名前・単位・区分を返します。
/// </summary>
/// <param name="Code">栄養素コード（例: PROT）。</param>
/// <param name="NameJa">日本語名（例: タンパク質）。</param>
/// <param name="Unit">量の単位（例: g、mg）。</param>
/// <param name="Category">区分（macro / vitamin / mineral / antioxidant）。</param>
/// <param name="IsReference">参考値の栄養素か（スコアには使わない）。</param>
/// <param name="DisplayOrder">画面に並べる順番。</param>
public sealed record NutrientDto(string Code, string NameJa, string Unit, string Category, bool IsReference, int DisplayOrder);
