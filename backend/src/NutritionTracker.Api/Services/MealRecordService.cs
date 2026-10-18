using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 食事記録の登録・取得・更新・削除・コピーを行うサービスです。
/// 記録するときに栄養量を計算して保存し、あとから表示するときはその保存値を使います。
/// </summary>
/// <param name="db">データベースへの窓口。</param>
/// <param name="catalog">栄養素の情報。</param>
public class MealRecordService(AppDbContext db, NutritionCatalog catalog)
{
    /// <summary>
    /// メモの最大文字数（500 文字）。
    /// </summary>
    private const int MaxMemoLength = 500;
    /// <summary>
    /// 受け付ける食事区分（朝・昼・夕・間食）。この並びが表示順です。
    /// </summary>
    private static readonly string[] MealTypes = ["breakfast", "lunch", "dinner", "snack"];

    /// <summary>
    /// 検証を通った記録項目 1 行です。食べた割合と重さは検証のときに確定します。更新で入力が変わらない項目は、既存の行（Kept）をそのまま持ちます。
    /// </summary>
    private sealed record ResolvedItem(
        MealRecordItemInput Input, InputMode Mode, decimal InputValue, Menu? Menu, Food? Food, FoodUnit? Unit, decimal? Ratio, decimal? AmountG)
    {
        /// <summary>
        /// 更新のとき、入力が変わらず前のスナップショットをそのまま残す項目。
        /// </summary>
        public MealRecordItem? Kept { get; init; }
    }

    /// <summary>
    /// 食事を記録します（API-14）。
    /// サーバーが食べた割合・重さを決め、項目ごとに栄養量（スナップショット）を計算して保存します。メニューは「全体の栄養量 × 食べた割合」、単品の食材は「重さから」計算します。
    /// </summary>
    /// <param name="input">食事日・食事区分・メモ・食べたもの。</param>
    /// <returns>保存した記録と、メニューの記録が合計 100% を超えたときの警告。入力に問題があれば 400、無効な食材は 409。</returns>
    public async Task<MealRecordSaveResultDto> CreateAsync(MealRecordInput input)
    {
        var record = await BuildRecordAsync(input, await ValidateAsync(input));
        db.MealRecords.Add(record);
        await db.SaveChangesAsync();
        return await ToSaveResultAsync(record.Id);
    }

    /// <summary>
    /// ある日の記録を別の日にコピーします（API-21。「昨日と同じ」）。
    /// 現在のメニュー・食材の内容で栄養量を計算し直します。削除済みのメニューと無効な食材はスキップして理由を返し、全項目がスキップされた食事は作りません。すべて 1 回の保存で作ります。
    /// </summary>
    /// <param name="input">コピー元の日・コピー先の日・コピー先に記録があってもコピーするか。</param>
    /// <returns>作った記録とスキップした項目。コピー元に記録がなければ 400、コピー先に記録があって許可がなければ 409。</returns>
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

    /// <summary>
    /// 検証済みの入力から、保存前の食事記録（項目と栄養量つき）を組み立てます。
    /// </summary>
    /// <param name="input">入力内容。</param>
    /// <param name="resolved">検証を通った項目。</param>
    /// <returns>保存前の食事記録。</returns>
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
    /// 食事記録を更新します（API-15）。食事日・区分・メモ・項目をすべて置き換えます。
    /// 入力（メニュー・食材・単位・入力方式・入力値）が前と同じ項目は前の栄養量を残し、変わった項目と新しい項目だけ計算し直します。外した項目は消えます。
    /// </summary>
    /// <param name="id">更新する食事記録の ID。</param>
    /// <param name="input">更新後の内容。</param>
    /// <returns>更新後の記録と警告。存在しなければ 404。</returns>
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

    /// <summary>
    /// 食事記録を削除します（API-16）。項目と栄養量も DB のカスケードで一緒に消えます。
    /// </summary>
    /// <param name="id">削除する食事記録の ID。存在しなければ 404。</param>
    /// <returns>処理の完了を表すタスク。</returns>
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

    /// <summary>
    /// 指定した日の食事記録を返します（API-13）。栄養量は記録時の保存値で、あとからメニューを変えても変わりません。
    /// </summary>
    /// <param name="date">取得する日（必須。なければ 400）。</param>
    /// <returns>その日の記録（朝・昼・夕・間食の順）。</returns>
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
    /// 食事記録の入力を検証し、項目ごとに食べた割合と重さを確定します。
    /// 食事日・区分・項目の数・メニューか食材のどちらか一方・入力方式・入力値・単位などを確かめて項目別の 400 にし、無効な食材は 409 にします。
    /// 更新のときは、入力が変わらない既存項目を、削除済みメニューや無効な食材でもそのまま残せます。
    /// </summary>
    /// <param name="input">入力内容。</param>
    /// <param name="existing">更新のときの既存の項目。作成のときは null。</param>
    /// <returns>検証を通った項目。</returns>
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

    /// <summary>
    /// 既存の項目と入力が同じ（メニュー・食材・単位・入力方式・入力値が同じ）かを判定します。
    /// </summary>
    /// <param name="e">既存の項目。</param>
    /// <param name="i">入力された項目。</param>
    /// <param name="mode">入力方式。</param>
    /// <param name="inputValue">小数 3 桁に丸めた入力値。</param>
    /// <returns>同じとき true。</returns>
    private static bool IsSameInput(MealRecordItem e, MealRecordItemInput i, InputMode mode, decimal inputValue) =>
        e.MenuId == i.MenuId && e.FoodId == i.FoodId && e.InputMode == i.InputMode && e.InputValue == inputValue
        && e.UnitId == (mode == InputMode.Unit ? i.UnitId : null);

    /// <summary>
    /// 入力方式の文字列（RATIO / SERVING / GRAM / UNIT）を計算用の種類に変換します。
    /// </summary>
    /// <param name="mode">入力方式の文字列。</param>
    /// <returns>計算用の入力方式。</returns>
    private static InputMode ParseMode(string mode) => mode switch
    {
        "RATIO" => InputMode.Ratio,
        "SERVING" => InputMode.Serving,
        "GRAM" => InputMode.Gram,
        _ => InputMode.Unit,
    };

    // ---- スナップショットの作成 ----

    /// <summary>
    /// 検証済みの項目から、保存する記録項目を組み立て、栄養量（スナップショット）を計算します。
    /// メニューは「メニュー全体の栄養量 × 食べた割合」、単品の食材は「重さからの栄養量」を、小数 4 桁に丸めて栄養素ごとに持たせます。
    /// </summary>
    /// <param name="r">検証を通った項目。</param>
    /// <returns>栄養量つきの記録項目。</returns>
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

    /// <summary>
    /// 保存した記録を読み直し、メニューの合計 100% 超えの警告を付けて返却用の形にします。
    /// </summary>
    /// <param name="recordId">食事記録の ID。</param>
    /// <returns>記録と警告。</returns>
    private async Task<MealRecordSaveResultDto> ToSaveResultAsync(long recordId)
    {
        var dto = (await LoadAsync(r => r.Id == recordId)).Single();
        return new MealRecordSaveResultDto(dto.Id, dto.EatenOn, dto.MealType, dto.Memo, dto.Items, await WarningsAsync(dto));
    }

    /// <summary>
    /// 条件に合う食事記録を、項目と保存済みの栄養量つきで読み込みます。
    /// 食事区分の順（朝・昼・夕・間食）、同じ区分は登録順に並べます。
    /// </summary>
    /// <param name="filter">記録を絞り込む条件。</param>
    /// <returns>返却用の食事記録の一覧。</returns>
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

    /// <summary>
    /// 記録に含まれるメニューごとに、全記録の食べた割合の合計が 100% を超えていないかを調べ、超えていれば警告にします。
    /// </summary>
    /// <param name="record">調べる食事記録。</param>
    /// <returns>警告の一覧（なければ空）。</returns>
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
