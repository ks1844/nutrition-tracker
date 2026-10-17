using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Entities;
using NutritionTracker.Domain.Nutrition;
using NutritionTracker.Domain.Text;

namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>取り込み結果。<see cref="Imported"/> が false のときは、対象の CSV が 1 つも無く何もしていない。</summary>
public sealed record MasterImportResult(
    bool Imported, int Foods, int FoodNutrients, int Units, int Aliases, int Targets, int Deactivated, IReadOnlyList<string> Warnings)
{
    public static MasterImportResult Skipped { get; } = new(false, 0, 0, 0, 0, 0, 0, []);
}

/// <summary>
/// マスタ CSV（食材・食材栄養素・食材単位・食材別名・栄養目標）の取り込み（docs/07.マスタデータ仕様.md §2〜§5, §7）。
/// 食品コード等をキーに upsert するので再実行しても結果は同じ。検証エラーが 1 件でもあれば何も書き込まない。
/// CSV から消えた食材は削除せず無効にする。食材の子（栄養素・単位・別名）は upsert のみで、CSV から消えても削除しない。
/// </summary>
public partial class MasterImportService(AppDbContext db, ILogger<MasterImportService> logger)
{
    public const string FoodsFile = "食材.csv";
    public const string FoodNutrientsFile = "食材栄養素.csv";
    public const string UnitsFile = "食材単位.csv";
    public const string AliasesFile = "食材別名.csv";
    public const string TargetsFile = "栄養目標.csv";

    private static readonly string[] CoreNutrients = ["ENERGY", "PROT", "FAT", "CHO"];
    private static readonly string[] Sexes = ["male", "female", "all"];
    private static readonly string[] Kinds = ["RDA", "AI", "DG_MIN", "DG_MAX", "UL"];
    private static readonly string[] Bases = ["PER_DAY", "PER_KG_BW", "PCT_ENERGY"];

    [GeneratedRegex(@"^[ぁ-ゖーー・\s]+$")]
    private static partial Regex HiraganaOnly();

    private sealed record FoodRow(CsvRow Row, string Code, string Name, string? OfficialName, string Kana, string? Group, decimal RefuseRatePct, string SourceVersion, bool IsActive);

    private sealed record FoodNutrientRow(string FoodCode, string NutrientCode, decimal? Amount, bool IsTrace, string? Note);

    private sealed record UnitRow(string FoodCode, string Name, decimal Grams, bool IsDefault);

    private sealed record TargetRow(string NutrientCode, string Sex, short AgeMin, short AgeMax, string Kind, decimal Amount, string Basis, string? Note);

    public async Task<MasterImportResult> ImportAsync(string directory, CancellationToken cancellationToken = default)
    {
        var foodsCsv = CsvTable.ReadIfExists(directory, FoodsFile);
        var nutrientsCsv = CsvTable.ReadIfExists(directory, FoodNutrientsFile);
        var unitsCsv = CsvTable.ReadIfExists(directory, UnitsFile);
        var aliasesCsv = CsvTable.ReadIfExists(directory, AliasesFile);
        var targetsCsv = CsvTable.ReadIfExists(directory, TargetsFile);
        if (foodsCsv is null && nutrientsCsv is null && unitsCsv is null && aliasesCsv is null && targetsCsv is null)
        {
            return MasterImportResult.Skipped;
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var nutrientIds = await db.Nutrients.AsNoTracking().ToDictionaryAsync(n => n.Code, n => n.Id, cancellationToken);

        var foods = ParseFoods(foodsCsv, errors);
        var foodCodes = foods.Select(f => f.Code).ToHashSet();
        var foodNutrients = ParseFoodNutrients(nutrientsCsv, foodCodes, nutrientIds.Keys, errors, warnings);
        var units = ParseUnits(unitsCsv, foodCodes, errors);
        var aliases = ParseAliases(aliasesCsv, foodCodes, errors);
        var targets = ParseTargets(targetsCsv, nutrientIds.Keys, errors);

        if (foods.Count > 0 && nutrientsCsv is not null)
        {
            var withRows = foodNutrients.GroupBy(n => n.FoodCode).ToDictionary(g => g.Key, g => g.Select(n => n.NutrientCode).ToHashSet());
            foreach (var food in foods)
            {
                var missing = CoreNutrients.Where(c => !withRows.GetValueOrDefault(food.Code, []).Contains(c)).ToList();
                if (missing.Count > 0)
                {
                    warnings.Add($"{food.Row.Where}: {food.Code} に {string.Join("・", missing)} の行がありません（V-7）");
                }
            }
        }
        if (errors.Count > 0)
        {
            throw new MasterImportException(errors);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var deactivated = 0;
        if (foodsCsv is not null)
        {
            deactivated = await UpsertFoodsAsync(foods, foodNutrients, units, aliases, nutrientIds, cancellationToken);
        }
        if (targetsCsv is not null)
        {
            await UpsertTargetsAsync(targets, nutrientIds, cancellationToken);
        }
        await tx.CommitAsync(cancellationToken);

        foreach (var warning in warnings)
        {
            logger.LogWarning("マスタ取り込み: {Warning}", warning);
        }
        return new MasterImportResult(true, foods.Count, foodNutrients.Count, units.Count, aliases.Count, targets.Count, deactivated, warnings);
    }

    // ---- 解析と検証 ----

    private static bool RequireColumns(CsvTable? table, List<string> errors, params string[] required)
    {
        var missing = table?.MissingColumns(required).ToList() ?? [];
        foreach (var column in missing)
        {
            errors.Add($"{table!.File}: 必須の列がありません: {column}");
        }
        return table is not null && missing.Count == 0;
    }

    private static List<FoodRow> ParseFoods(CsvTable? table, List<string> errors)
    {
        var result = new List<FoodRow>();
        if (!RequireColumns(table, errors, "食品コード", "名称", "読み仮名", "廃棄率", "出典版"))
        {
            return result;
        }
        var seen = new HashSet<string>();
        foreach (var row in table!.Rows)
        {
            var before = errors.Count;
            var code = row["食品コード"];
            Required(row, "食品コード", 20, errors);
            if (code.Length > 0 && !seen.Add(code))
            {
                errors.Add($"{row.Where}: 食品コード「{code}」が重複しています（V-1）");
            }
            Required(row, "名称", 100, errors);
            Optional(row, "正式名称", 200, errors);
            Optional(row, "食品群", 50, errors);
            Required(row, "出典版", 50, errors); // 空は不可（V-6。NFR-008 出典の保持）

            var kana = SearchTextNormalizer.ToHiragana(row["読み仮名"]);
            if (row["読み仮名"].Length == 0)
            {
                errors.Add($"{row.Where}: 読み仮名は必須です");
            }
            else if (!HiraganaOnly().IsMatch(kana) || kana.Length > 100)
            {
                errors.Add($"{row.Where}: 読み仮名はひらがな（100 文字以内）で指定してください: {row["読み仮名"]}（V-8）");
            }

            decimal refuse = 0m;
            if (!TryDecimal(row["廃棄率"], out refuse) || refuse < 0m || refuse >= 100m)
            {
                errors.Add($"{row.Where}: 廃棄率は 0 以上 100 未満の数値で指定してください: {row["廃棄率"]}（V-4）");
            }
            var active = ParseFlag(row, "有効フラグ", true, errors);

            if (errors.Count == before)
            {
                result.Add(new FoodRow(
                    row, code, row["名称"], NullIfEmpty(row["正式名称"]), kana, NullIfEmpty(row["食品群"]),
                    RoundingPolicy.Round(refuse, 1), row["出典版"], active));
            }
        }
        return result;
    }

    private static List<FoodNutrientRow> ParseFoodNutrients(
        CsvTable? table, HashSet<string> foodCodes, IEnumerable<string> nutrientCodes, List<string> errors, List<string> warnings)
    {
        var result = new List<FoodNutrientRow>();
        if (!RequireColumns(table, errors, "食品コード", "栄養素コード", "成分表表記"))
        {
            return result;
        }
        var known = nutrientCodes.ToHashSet();
        var seen = new HashSet<(string, string)>();
        foreach (var row in table!.Rows)
        {
            var before = errors.Count;
            var food = row["食品コード"];
            var nutrient = row["栄養素コード"];
            if (!foodCodes.Contains(food))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」が {FoodsFile} にありません（V-1）");
            }
            if (!known.Contains(nutrient))
            {
                errors.Add($"{row.Where}: 栄養素コード「{nutrient}」は存在しません（V-2）");
            }
            else if (!seen.Add((food, nutrient)))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」の栄養素コード「{nutrient}」が重複しています");
            }
            NotationValue value = default;
            if (!FoodTableNotation.TryParse(row["成分表表記"], out value))
            {
                errors.Add($"{row.Where}: 成分表表記「{row["成分表表記"]}」を変換できません（V-3）");
            }
            else if (value.Amount is >= 100_000_000m)
            {
                errors.Add($"{row.Where}: 成分表表記の値が大きすぎます: {row["成分表表記"]}");
            }
            Optional(row, "出典メモ", 200, errors);
            if (errors.Count == before)
            {
                result.Add(new FoodNutrientRow(
                    food, nutrient, value.Amount is { } a ? RoundingPolicy.Round(a, 4) : null, value.IsTrace, NullIfEmpty(row["出典メモ"])));
            }
        }
        return result;
    }

    private static List<UnitRow> ParseUnits(CsvTable? table, HashSet<string> foodCodes, List<string> errors)
    {
        var result = new List<UnitRow>();
        if (!RequireColumns(table, errors, "食品コード", "単位名", "1単位あたりグラム"))
        {
            return result;
        }
        var names = new HashSet<(string, string)>();
        var defaults = new HashSet<string>();
        foreach (var row in table!.Rows)
        {
            var before = errors.Count;
            var food = row["食品コード"];
            if (!foodCodes.Contains(food))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」が {FoodsFile} にありません（V-1）");
            }
            var name = Required(row, "単位名", 30, errors);
            if (name is not null && !names.Add((food, name)))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」の単位名「{name}」が重複しています（V-5）");
            }
            decimal grams = 0m;
            if (!TryDecimal(row["1単位あたりグラム"], out grams) || RoundingPolicy.Round(grams, 2) <= 0m || grams >= 1_000_000m)
            {
                errors.Add($"{row.Where}: 1単位あたりグラムは 0 より大きい数値で指定してください: {row["1単位あたりグラム"]}（V-5）");
            }
            var isDefault = ParseFlag(row, "既定フラグ", false, errors);
            if (isDefault && !defaults.Add(food))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」の既定フラグが 2 件以上あります（V-5）");
            }
            if (errors.Count == before)
            {
                result.Add(new UnitRow(food, name!, RoundingPolicy.Round(grams, 2), isDefault));
            }
        }
        return result;
    }

    private static List<(string FoodCode, string Alias)> ParseAliases(CsvTable? table, HashSet<string> foodCodes, List<string> errors)
    {
        var result = new List<(string, string)>();
        if (!RequireColumns(table, errors, "食品コード", "別名"))
        {
            return result;
        }
        var seen = new HashSet<(string, string)>();
        foreach (var row in table!.Rows)
        {
            var before = errors.Count;
            var food = row["食品コード"];
            if (!foodCodes.Contains(food))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」が {FoodsFile} にありません（V-1）");
            }
            var alias = Required(row, "別名", 100, errors);
            if (alias is not null && !seen.Add((food, alias)))
            {
                errors.Add($"{row.Where}: 食品コード「{food}」の別名「{alias}」が重複しています");
            }
            if (errors.Count == before)
            {
                result.Add((food, alias!));
            }
        }
        return result;
    }

    private static List<TargetRow> ParseTargets(CsvTable? table, IEnumerable<string> nutrientCodes, List<string> errors)
    {
        var result = new List<TargetRow>();
        if (!RequireColumns(table, errors, "栄養素コード", "性別", "年齢下限", "年齢上限", "種別", "目標値", "単位基準"))
        {
            return result;
        }
        var known = nutrientCodes.ToHashSet();
        var keys = new HashSet<(string, string, short, short, string)>();
        foreach (var row in table!.Rows)
        {
            var before = errors.Count;
            var nutrient = row["栄養素コード"];
            if (!known.Contains(nutrient))
            {
                errors.Add($"{row.Where}: 栄養素コード「{nutrient}」は存在しません（V-2）");
            }
            if (!Sexes.Contains(row["性別"]))
            {
                errors.Add($"{row.Where}: 性別は male / female / all のいずれかです: {row["性別"]}");
            }
            if (!Kinds.Contains(row["種別"]))
            {
                errors.Add($"{row.Where}: 種別は {string.Join(" / ", Kinds)} のいずれかです: {row["種別"]}");
            }
            else if (nutrient == NutrientCodes.Energy && row["種別"].StartsWith("DG_", StringComparison.Ordinal))
            {
                errors.Add($"{row.Where}: ENERGY には DG_* を登録できません（プロフィールから算出します）");
            }
            if (!Bases.Contains(row["単位基準"]))
            {
                errors.Add($"{row.Where}: 単位基準は {string.Join(" / ", Bases)} のいずれかです: {row["単位基準"]}");
            }
            if (!short.TryParse(row["年齢下限"], NumberStyles.None, CultureInfo.InvariantCulture, out var ageMin)
                || !short.TryParse(row["年齢上限"], NumberStyles.None, CultureInfo.InvariantCulture, out var ageMax)
                || ageMin > ageMax || ageMax > 999)
            {
                errors.Add($"{row.Where}: 年齢下限・年齢上限は 0 以上 999 以下の整数で、下限 ≤ 上限にしてください（V-10）");
                ageMin = ageMax = 0;
            }
            decimal amount = 0m;
            if (!TryDecimal(row["目標値"], out amount) || amount >= 100_000_000m)
            {
                errors.Add($"{row.Where}: 目標値は 0 以上の数値で指定してください: {row["目標値"]}（V-9）");
            }
            Optional(row, "出典メモ", 200, errors);
            if (errors.Count == before && !keys.Add((nutrient, row["性別"], ageMin, ageMax, row["種別"])))
            {
                errors.Add($"{row.Where}: 同じ条件（栄養素・性別・年齢・種別）の行が重複しています（V-10）");
            }
            if (errors.Count == before)
            {
                result.Add(new TargetRow(nutrient, row["性別"], ageMin, ageMax, row["種別"], RoundingPolicy.Round(amount, 4), row["単位基準"], NullIfEmpty(row["出典メモ"])));
            }
        }

        // 同じ栄養素・種別で、年齢の範囲が重なる行がある（性別が同じ、またはどちらかが all）と、どの行を使うか決まらない
        foreach (var group in result.GroupBy(t => (t.NutrientCode, t.Kind)))
        {
            var rows = group.ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                for (var j = i + 1; j < rows.Count; j++)
                {
                    var (a, b) = (rows[i], rows[j]);
                    var sexOverlap = a.Sex == b.Sex || a.Sex == "all" || b.Sex == "all";
                    if (sexOverlap && a.AgeMin <= b.AgeMax && b.AgeMin <= a.AgeMax)
                    {
                        errors.Add($"{TargetsFile}: {a.NutrientCode} の {a.Kind} で、年齢の範囲が重なる行があります（{a.Sex} {a.AgeMin}〜{a.AgeMax} と {b.Sex} {b.AgeMin}〜{b.AgeMax}）（V-10）");
                    }
                }
            }
        }
        return result;
    }

    private static string? Required(CsvRow row, string column, int maxLength, List<string> errors)
    {
        var value = row[column];
        if (value.Length == 0)
        {
            errors.Add($"{row.Where}: {column}は必須です");
            return null;
        }
        if (value.Length > maxLength)
        {
            errors.Add($"{row.Where}: {column}は {maxLength} 文字以内で指定してください");
            return null;
        }
        return value;
    }

    private static void Optional(CsvRow row, string column, int maxLength, List<string> errors)
    {
        if (row[column].Length > maxLength)
        {
            errors.Add($"{row.Where}: {column}は {maxLength} 文字以内で指定してください");
        }
    }

    private static bool ParseFlag(CsvRow row, string column, bool whenEmpty, List<string> errors)
    {
        switch (row[column])
        {
            case "":
                return whenEmpty;
            case "0":
                return false;
            case "1":
                return true;
            default:
                errors.Add($"{row.Where}: {column}は 0 か 1 で指定してください: {row[column]}");
                return whenEmpty;
        }
    }

    /// <summary>全角・桁区切りのカンマを許す、0 以上の小数。符号・指数・記号（-、Tr、括弧）は不可（V-9）。</summary>
    private static bool TryDecimal(string text, out decimal value)
    {
        var folded = new string(text.Trim()
            .Select(c => c is >= '０' and <= '９' ? (char)(c - '０' + '0') : c == '．' ? '.' : c)
            .Where(c => c != ',')
            .ToArray());
        return decimal.TryParse(folded, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && folded.Length > 0;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    // ---- 書き込み ----

    private async Task<int> UpsertFoodsAsync(
        List<FoodRow> foods,
        List<FoodNutrientRow> foodNutrients,
        List<UnitRow> units,
        List<(string FoodCode, string Alias)> aliases,
        Dictionary<string, short> nutrientIds,
        CancellationToken ct)
    {
        var codes = foods.Select(f => f.Code).ToList();
        var existing = await db.Foods
            .Include(f => f.Nutrients).Include(f => f.Units).Include(f => f.Aliases)
            .Where(f => codes.Contains(f.FoodCode))
            .ToDictionaryAsync(f => f.FoodCode, ct);

        // 既定単位の付け替えが一意制約に触れないよう、CSV に単位がある食材は先に既定フラグをすべて外して保存する
        var foodsWithUnits = units.Select(u => u.FoodCode).ToHashSet();
        foreach (var unit in existing.Where(p => foodsWithUnits.Contains(p.Key)).SelectMany(p => p.Value.Units).Where(u => u.IsDefault))
        {
            unit.IsDefault = false;
        }
        await db.SaveChangesAsync(ct);

        foreach (var row in foods)
        {
            if (!existing.TryGetValue(row.Code, out var food))
            {
                food = new Food { FoodCode = row.Code };
                db.Foods.Add(food);
                existing[row.Code] = food;
            }
            food.Name = row.Name;
            food.OfficialName = row.OfficialName;
            food.NameKana = row.Kana;
            food.FoodGroup = row.Group;
            food.RefuseRatePct = row.RefuseRatePct;
            food.SourceVersion = row.SourceVersion;
            food.IsActive = row.IsActive;
        }

        foreach (var row in foodNutrients)
        {
            var food = existing[row.FoodCode];
            var nutrientId = nutrientIds[row.NutrientCode];
            var current = food.Nutrients.FirstOrDefault(n => n.NutrientId == nutrientId);
            if (current is null)
            {
                current = new FoodNutrient { NutrientId = nutrientId };
                food.Nutrients.Add(current);
            }
            current.AmountPer100g = row.Amount;
            current.IsTrace = row.IsTrace;
            current.SourceNote = row.Note;
        }

        foreach (var row in units)
        {
            var food = existing[row.FoodCode];
            var current = food.Units.FirstOrDefault(u => u.UnitName == row.Name);
            if (current is null)
            {
                current = new FoodUnit { UnitName = row.Name };
                food.Units.Add(current);
            }
            current.GramsPerUnit = row.Grams;
            current.IsDefault = row.IsDefault;
        }

        foreach (var (foodCode, alias) in aliases)
        {
            var food = existing[foodCode];
            if (food.Aliases.All(a => a.Alias != alias))
            {
                food.Aliases.Add(new FoodAlias { Alias = alias });
            }
        }

        // CSV から消えた食材は削除せず無効にする
        var deactivated = 0;
        foreach (var food in await db.Foods.Where(f => f.IsActive && !codes.Contains(f.FoodCode)).ToListAsync(ct))
        {
            food.IsActive = false;
            deactivated++;
        }
        await db.SaveChangesAsync(ct);
        return deactivated;
    }

    private async Task UpsertTargetsAsync(List<TargetRow> targets, Dictionary<string, short> nutrientIds, CancellationToken ct)
    {
        var existing = await db.NutrientTargets.ToListAsync(ct);
        foreach (var row in targets)
        {
            var nutrientId = nutrientIds[row.NutrientCode];
            var current = existing.FirstOrDefault(t =>
                t.NutrientId == nutrientId && t.Sex == row.Sex && t.AgeMin == row.AgeMin && t.AgeMax == row.AgeMax && t.Kind == row.Kind);
            if (current is null)
            {
                current = new NutrientTarget { NutrientId = nutrientId, Sex = row.Sex, AgeMin = row.AgeMin, AgeMax = row.AgeMax, Kind = row.Kind };
                db.NutrientTargets.Add(current);
                existing.Add(current);
            }
            current.Amount = row.Amount;
            current.Basis = row.Basis;
            current.SourceNote = row.Note;
        }
        await db.SaveChangesAsync(ct);
    }
}
