using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

public class MealRecordService(AppDbContext db, NutritionCatalog catalog)
{
    private const int MaxMemoLength = 500;
    private static readonly string[] MealTypes = ["breakfast", "lunch", "dinner", "snack"];

    /// <summary>入力を検証して解決した記録項目 1 行（割合・重量は検証時に確定する）。</summary>
    private sealed record ResolvedItem(
        MealRecordItemInput Input, InputMode Mode, decimal InputValue, Menu? Menu, Food? Food, FoodUnit? Unit, decimal? Ratio, decimal? AmountG)
    {
        /// <summary>更新時、入力が変わらず既存のスナップショットをそのまま残す項目。</summary>
        public MealRecordItem? Kept { get; init; }
    }

    /// <summary>
    /// API-14 食事記録の作成。サーバーが食べた割合・重量を決め、項目ごとに栄養スナップショットを保存する
    /// （メニューは合計 × 割合、単品食材は重量から。docs/03.栄養計算仕様.md §6, §7）。
    /// </summary>
    public async Task<MealRecordSaveResultDto> CreateAsync(MealRecordInput input)
    {
        var record = await BuildRecordAsync(input, await ValidateAsync(input));
        db.MealRecords.Add(record);
        await db.SaveChangesAsync();
        return await ToSaveResultAsync(record.Id);
    }

    /// <summary>
    /// API-21 他の日の記録をコピーする（「昨日と同じ」）。<b>現在のメニュー・食材の内容で再計算</b>してスナップショットを作る。
    /// 論理削除されたメニュー・無効な食材はスキップして <c>skipped</c> に載せる。全記録を 1 回の保存で作る。
    /// </summary>
    public async Task<MealRecordCopyResultDto> CopyAsync(MealRecordCopyInput input)
    {
        var errors = new List<ErrorDetail>();
        if (input.FromDate is null)
        {
            errors.Add(new ErrorDetail("fromDate", "コピー元の日付を指定してください"));
        }
        if (input.ToDate is null)
        {
            errors.Add(new ErrorDetail("toDate", "コピー先の日付を指定してください"));
        }
        if (errors.Count == 0 && input.FromDate == input.ToDate)
        {
            errors.Add(new ErrorDetail("toDate", "コピー元と異なる日付を指定してください"));
        }
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
        var from = input.FromDate!.Value;
        var to = input.ToDate!.Value;

        var sources = await db.MealRecords.AsNoTracking()
            .Include(r => r.Items).ThenInclude(i => i.Menu)
            .Include(r => r.Items).ThenInclude(i => i.Food)
            .Where(r => r.UserId == User.FixedUserId && r.EatenOn == from)
            .OrderBy(r => r.Id)
            .ToListAsync();
        if (sources.Count == 0)
        {
            throw new ValidationException("fromDate", "コピー元の日に記録がありません");
        }
        if (input.AllowExisting != true && await db.MealRecords.AnyAsync(r => r.UserId == User.FixedUserId && r.EatenOn == to))
        {
            throw new ConflictException("コピー先の日にすでに記録があります", [new ErrorDetail("toDate", "TARGET_DATE_HAS_RECORDS")]);
        }

        var skipped = new List<SkippedItemDto>();
        var created = new List<MealRecord>();
        foreach (var source in sources)
        {
            var items = new List<MealRecordItemInput>();
            foreach (var item in source.Items.OrderBy(i => i.Id))
            {
                if (item.Menu is { DeletedAt: not null })
                {
                    skipped.Add(new SkippedItemDto(item.MenuId, null, "MENU_DELETED"));
                }
                else if (item.Food is { IsActive: false })
                {
                    skipped.Add(new SkippedItemDto(null, item.FoodId, "FOOD_INACTIVE"));
                }
                else
                {
                    items.Add(new MealRecordItemInput(item.MenuId, item.FoodId, item.UnitId, item.InputMode, item.InputValue));
                }
            }
            if (items.Count == 0)
            {
                continue;
            }
            var request = new MealRecordInput(to, source.MealType, source.Memo, items);
            created.Add(await BuildRecordAsync(request, await ValidateAsync(request)));
        }
        db.MealRecords.AddRange(created);
        await db.SaveChangesAsync();
        return new MealRecordCopyResultDto(await LoadAsync(r => created.Select(c => c.Id).Contains(r.Id)), skipped);
    }

    private async Task<MealRecord> BuildRecordAsync(MealRecordInput input, List<ResolvedItem> resolved)
    {
        var record = new MealRecord
        {
            UserId = User.FixedUserId,
            EatenOn = input.EatenOn!.Value,
            MealType = input.MealType!,
            Memo = string.IsNullOrWhiteSpace(input.Memo) ? null : input.Memo,
        };
        foreach (var item in resolved)
        {
            record.Items.Add(await BuildItemAsync(item));
        }
        return record;
    }


    /// <summary>
    /// API-15 食事記録の更新（全項目置換）。入力が変わった項目はその時点のメニュー・食材の内容でスナップショットを再計算して置き換え、
    /// 入力が同じ項目は変えない（docs/03.栄養計算仕様.md §8）。
    /// </summary>
    public async Task<MealRecordSaveResultDto> UpdateAsync(long id, MealRecordInput input)
    {
        var record = await db.MealRecords.Include(r => r.Items).ThenInclude(i => i.Nutrients)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == User.FixedUserId)
            ?? throw new NotFoundException();
        var resolved = await ValidateAsync(input, record.Items);

        record.EatenOn = input.EatenOn!.Value;
        record.MealType = input.MealType!;
        record.Memo = string.IsNullOrWhiteSpace(input.Memo) ? null : input.Memo;

        var keptItems = resolved.Where(r => r.Kept is not null).Select(r => r.Kept!).ToHashSet();
        db.MealRecordItems.RemoveRange(record.Items.Where(i => !keptItems.Contains(i)).ToList());
        foreach (var item in resolved.Where(r => r.Kept is null))
        {
            record.Items.Add(await BuildItemAsync(item));
        }
        await db.SaveChangesAsync();
        return await ToSaveResultAsync(record.Id);
    }

    /// <summary>API-16 食事記録の削除（物理削除。項目・スナップショットは DB のカスケードで消える）。存在しなければ 404。</summary>
    public async Task DeleteAsync(long id)
    {
        var deleted = await db.MealRecords
            .Where(r => r.Id == id && r.UserId == User.FixedUserId)
            .ExecuteDeleteAsync();
        if (deleted == 0)
        {
            throw new NotFoundException();
        }
    }

    /// <summary>API-13 日別の食事記録。栄養は保存済みスナップショットをそのまま返す（メニューを後から編集しても変わらない）。</summary>
    public async Task<MealRecordDayDto> GetByDateAsync(DateOnly? date)
    {
        if (date is not { } day)
        {
            throw new ValidationException("date", "日付を指定してください");
        }
        return new MealRecordDayDto(day, await LoadAsync(r => r.EatenOn == day));
    }

    // ---- 入力の検証 ----

    /// <summary>
    /// 入力を検証する。形式・範囲の違反は 400（項目別）、無効な食材は 409。
    /// <paramref name="existing"/>（更新時の既存項目）と入力が同じ項目は、削除済みメニューや無効な食材でも保持できる。
    /// </summary>
    private async Task<List<ResolvedItem>> ValidateAsync(MealRecordInput input, IReadOnlyList<MealRecordItem>? existing = null)
    {
        var unmatched = existing?.ToList() ?? [];
        var errors = new List<ErrorDetail>();
        if (input.EatenOn is null)
        {
            errors.Add(new ErrorDetail("eatenOn", "食事日を指定してください"));
        }
        if (input.MealType is null || !MealTypes.Contains(input.MealType))
        {
            errors.Add(new ErrorDetail("mealType", "breakfast / lunch / dinner / snack のいずれかを指定してください"));
        }
        if (input.Memo is { } memo && memo.Length > MaxMemoLength)
        {
            errors.Add(new ErrorDetail("memo", $"{MaxMemoLength}文字以内で入力してください"));
        }
        var items = input.Items ?? [];
        if (items.Count == 0)
        {
            errors.Add(new ErrorDetail("items", "項目を1件以上追加してください"));
        }

        var menuIds = items.Where(i => i.MenuId is not null).Select(i => i.MenuId!.Value).Distinct().ToList();
        var menus = await db.Menus
            .Include(m => m.Items).ThenInclude(i => i.Food).ThenInclude(f => f!.Nutrients).ThenInclude(n => n.Nutrient)
            .Where(m => menuIds.Contains(m.Id) && m.UserId == User.FixedUserId && m.DeletedAt == null)
            .ToDictionaryAsync(m => m.Id);
        var foodIds = items.Where(i => i.FoodId is not null).Select(i => i.FoodId!.Value).Distinct().ToList();
        var foods = await db.Foods
            .Include(f => f.Units)
            .Include(f => f.Nutrients).ThenInclude(n => n.Nutrient)
            .Where(f => foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id);

        var resolved = new List<ResolvedItem>();
        var inactive = new List<ErrorDetail>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var before = errors.Count;
            if ((item.MenuId is null) == (item.FoodId is null))
            {
                errors.Add(new ErrorDetail($"items[{i}].menuId", "メニューか食材のどちらか一方を指定してください"));
                continue;
            }
            var allowed = item.MenuId is not null ? new[] { "RATIO", "SERVING", "GRAM" } : ["GRAM", "UNIT"];
            if (item.InputMode is null || !allowed.Contains(item.InputMode))
            {
                errors.Add(new ErrorDetail($"items[{i}].inputMode", $"{string.Join(" / ", allowed)} のいずれかを指定してください"));
            }
            if (item.InputValue is not { } value || value <= 0m || RoundingPolicy.Round(value, 3) <= 0m || value >= 1_000_000m)
            {
                errors.Add(new ErrorDetail($"items[{i}].inputValue", "0より大きい値を指定してください"));
            }
            if (errors.Count > before)
            {
                continue;
            }

            var mode = ParseMode(item.InputMode!);
            var inputValue = RoundingPolicy.Round(item.InputValue!.Value, 3);
            var kept = unmatched.FirstOrDefault(e => IsSameInput(e, item, mode, inputValue));
            if (kept is not null)
            {
                unmatched.Remove(kept);
                resolved.Add(new ResolvedItem(item, mode, inputValue, null, null, null, null, null) { Kept = kept });
                continue;
            }
            if (item.MenuId is { } menuId)
            {
                if (!menus.TryGetValue(menuId, out var menu))
                {
                    errors.Add(new ErrorDetail($"items[{i}].menuId", "存在しない、または削除されたメニューです"));
                    continue;
                }
                var ratio = PortionCalculator.Ratio(mode, inputValue, menu.CookedTotalWeightG, menu.Servings);
                if (ratio <= 0m || ratio >= 1000m)
                {
                    errors.Add(new ErrorDetail($"items[{i}].inputValue", "指定できる範囲を超えています"));
                    continue;
                }
                resolved.Add(new ResolvedItem(item, mode, inputValue, menu, null, null, ratio, mode == InputMode.Gram ? inputValue : null));
                continue;
            }

            if (!foods.TryGetValue(item.FoodId!.Value, out var food))
            {
                errors.Add(new ErrorDetail($"items[{i}].foodId", "存在しない食材です"));
                continue;
            }
            FoodUnit? unit = null;
            if (mode == InputMode.Unit)
            {
                if (item.UnitId is null)
                {
                    errors.Add(new ErrorDetail($"items[{i}].unitId", "単位を指定してください"));
                    continue;
                }
                unit = food.Units.FirstOrDefault(u => u.Id == item.UnitId);
                if (unit is null)
                {
                    errors.Add(new ErrorDetail($"items[{i}].unitId", "この食材の単位ではありません"));
                    continue;
                }
            }
            var amountG = UnitConverter.ToAmountG(inputValue, unit?.GramsPerUnit);
            if (amountG <= 0m || amountG >= 1_000_000m)
            {
                errors.Add(new ErrorDetail($"items[{i}].inputValue", "指定できる範囲を超えています"));
                continue;
            }
            if (!food.IsActive)
            {
                inactive.Add(new ErrorDetail($"items[{i}].foodId", "FOOD_INACTIVE"));
            }
            resolved.Add(new ResolvedItem(item, mode, inputValue, null, food, unit, null, amountG));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
        if (inactive.Count > 0)
        {
            throw new ConflictException("無効になっている食材は記録できません", inactive);
        }
        return resolved;
    }

    private static bool IsSameInput(MealRecordItem e, MealRecordItemInput i, InputMode mode, decimal inputValue) =>
        e.MenuId == i.MenuId && e.FoodId == i.FoodId && e.InputMode == i.InputMode && e.InputValue == inputValue
        && e.UnitId == (mode == InputMode.Unit ? i.UnitId : null);

    private static InputMode ParseMode(string mode) => mode switch
    {
        "RATIO" => InputMode.Ratio,
        "SERVING" => InputMode.Serving,
        "GRAM" => InputMode.Gram,
        _ => InputMode.Unit,
    };

    // ---- スナップショットの作成 ----

    private async Task<MealRecordItem> BuildItemAsync(ResolvedItem r)
    {
        var codes = await catalog.CodesAsync();
        var reference = await catalog.ReferenceCodesAsync();
        var item = new MealRecordItem { InputMode = r.Input.InputMode!, InputValue = r.InputValue };
        NutrientTotals snapshot;
        if (r.Menu is { } menu)
        {
            var totals = MenuNutritionCalculator.Total(
                menu.Items.Select(i => new MenuLine(NutritionCatalog.ToFoodData(i.Food!), i.AmountG)), codes, reference);
            item.MenuId = menu.Id;
            item.PortionRatio = r.Ratio;
            item.AmountG = r.AmountG;
            snapshot = SnapshotCalculator.ForMenu(totals, r.Ratio!.Value);
        }
        else
        {
            var food = r.Food!;
            item.FoodId = food.Id;
            item.UnitId = r.Unit?.Id;
            item.AmountG = r.AmountG;
            snapshot = SnapshotCalculator.ForFood(
                FoodNutritionCalculator.Calculate(NutritionCatalog.ToFoodData(food), r.AmountG!.Value, codes, reference));
        }

        foreach (var nutrient in await catalog.NutrientsAsync())
        {
            if (snapshot.Codes.Contains(nutrient.Code))
            {
                item.Nutrients.Add(new MealRecordItemNutrient
                {
                    NutrientId = nutrient.Id,
                    Amount = snapshot[nutrient.Code].Amount,
                    IsIncomplete = snapshot[nutrient.Code].IsIncomplete,
                });
            }
        }
        return item;
    }

    // ---- 表示用の組み立て ----

    private async Task<MealRecordSaveResultDto> ToSaveResultAsync(long recordId)
    {
        var dto = (await LoadAsync(r => r.Id == recordId)).Single();
        return new MealRecordSaveResultDto(dto.Id, dto.EatenOn, dto.MealType, dto.Memo, dto.Items, await WarningsAsync(dto));
    }

    /// <summary>条件に合う記録を、食事区分順（朝・昼・夕・間食）、同じ区分は登録順に返す。</summary>
    private async Task<List<MealRecordDto>> LoadAsync(Expression<Func<MealRecord, bool>> filter)
    {
        var records = await db.MealRecords.AsNoTracking()
            .Include(r => r.Items).ThenInclude(i => i.Menu)
            .Include(r => r.Items).ThenInclude(i => i.Food)
            .Include(r => r.Items).ThenInclude(i => i.Nutrients)
            .Where(r => r.UserId == User.FixedUserId)
            .Where(filter)
            .ToListAsync();
        var nutrients = await catalog.NutrientsAsync();

        return records
            .OrderBy(r => Array.IndexOf(MealTypes, r.MealType)).ThenBy(r => r.Id)
            .Select(r => new MealRecordDto(
                r.Id, r.EatenOn, r.MealType, r.Memo,
                r.Items.OrderBy(i => i.Id).Select(i => new MealRecordItemDto(
                    i.Id, i.MenuId, i.Menu?.Name, i.FoodId, i.Food?.Name, i.InputMode, i.InputValue, i.PortionRatio, i.AmountG,
                    nutrients
                        .Join(i.Nutrients, n => n.Id, s => s.NutrientId, (n, s) => new NutrientAmountDto(n.Code, s.Amount, s.IsIncomplete))
                        .ToList())).ToList()))
            .ToList();
    }

    /// <summary>この記録に含まれるメニューごとに、全記録の食べた割合の合計が 100% を超えていれば警告を返す。</summary>
    private async Task<IReadOnlyList<MealRecordWarningDto>> WarningsAsync(MealRecordDto record)
    {
        var menus = record.Items.Where(i => i.MenuId is not null).GroupBy(i => i.MenuId!.Value).ToList();
        var warnings = new List<MealRecordWarningDto>();
        foreach (var group in menus)
        {
            var menuId = group.Key;
            var total = await db.MealRecordItems.AsNoTracking()
                .Where(i => i.MenuId == menuId && i.PortionRatio != null)
                .SumAsync(i => i.PortionRatio!.Value);
            if (total > 1m)
            {
                warnings.Add(new MealRecordWarningDto(
                    "MENU_OVER_100PCT", menuId, RoundingPolicy.Round(total, RoundingPolicy.RatioDigits),
                    $"{group.First().MenuName}の記録が合計100%を超えています"));
            }
        }
        return warnings;
    }
}
