using NutritionTracker.Api.Entities;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Data.Seed;

/// <summary>T栄養素の初期データ（docs/04.データモデル.md §5 が正本。単位・区分は成分表の原典と照合する: 要確認）。</summary>
internal static class NutrientSeed
{
    /// <summary>初期データの作成日時（固定。再生成しても差分が出ないようにする）。</summary>
    public static readonly DateTime SeededAt = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly (string Code, string Name, string Unit, string Category)[] Rows =
    [
        (NutrientCodes.Energy, "エネルギー", "kcal", "macro"),
        (NutrientCodes.Protein, "タンパク質", "g", "macro"),
        (NutrientCodes.Fat, "脂質", "g", "macro"),
        (NutrientCodes.Carbohydrate, "炭水化物", "g", "macro"),
        (NutrientCodes.DietaryFiber, "食物繊維総量", "g", "macro"),
        (NutrientCodes.SaltEquivalent, "食塩相当量", "g", "mineral"),
        (NutrientCodes.Potassium, "カリウム", "mg", "mineral"),
        (NutrientCodes.Calcium, "カルシウム", "mg", "mineral"),
        (NutrientCodes.Magnesium, "マグネシウム", "mg", "mineral"),
        (NutrientCodes.Iron, "鉄", "mg", "mineral"),
        (NutrientCodes.Zinc, "亜鉛", "mg", "mineral"),
        (NutrientCodes.VitaminARae, "ビタミンA", "μg", "vitamin"),
        (NutrientCodes.BetaCaroteneEquivalent, "β-カロテン当量", "μg", "vitamin"),
        (NutrientCodes.VitaminD, "ビタミンD", "μg", "vitamin"),
        (NutrientCodes.VitaminE, "ビタミンE", "mg", "vitamin"),
        (NutrientCodes.VitaminK, "ビタミンK", "μg", "vitamin"),
        (NutrientCodes.VitaminB1, "ビタミンB1", "mg", "vitamin"),
        (NutrientCodes.VitaminB2, "ビタミンB2", "mg", "vitamin"),
        (NutrientCodes.Niacin, "ナイアシン", "mg", "vitamin"),
        (NutrientCodes.VitaminB6, "ビタミンB6", "mg", "vitamin"),
        (NutrientCodes.VitaminB12, "ビタミンB12", "μg", "vitamin"),
        (NutrientCodes.Folate, "葉酸", "μg", "vitamin"),
        (NutrientCodes.VitaminC, "ビタミンC", "mg", "vitamin"),
        (NutrientCodes.Astaxanthin, "アスタキサンチン", "mg", "antioxidant"),
    ];

    public static IReadOnlyList<Nutrient> All() => Rows
        .Select((r, i) => new Nutrient
        {
            Id = (short)(i + 1),
            Code = r.Code,
            Name = r.Name,
            Unit = r.Unit,
            Category = r.Category,
            IsReference = r.Code == NutrientCodes.Astaxanthin, // 成分表外の参考値（スコア外）
            DisplayOrder = (short)(i + 1),
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt,
        })
        .ToList();
}
