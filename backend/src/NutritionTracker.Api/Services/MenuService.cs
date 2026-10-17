using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

public class MenuService(AppDbContext db, NutritionCatalog catalog)
{
    private const int MaxNameLength = 100;
    private const int MaxLabelLength = 20;
    private const int MaxMemoLength = 500;
    private const string DefaultServingLabel = "お椀";

    /// <summary>API-07 メニュー作成。食材量は g または単位で入力でき、換算済みの重量（廃棄前）を保存する。</summary>
    public async Task<MenuDetailDto> CreateAsync(MenuInput input)
    {
        var foods = await ValidateAsync(input);
        var menu = new Menu { UserId = User.FixedUserId };
        Apply(menu, input, foods);
        db.Menus.Add(menu);
        await db.SaveChangesAsync();
        return await ToDetailAsync(menu.Id);
    }

    /// <summary>API-06 メニュー一覧。論理削除されていないメニューを新しい順に、残量つきで返す。</summary>
    public async Task<IReadOnlyList<MenuSummaryDto>> ListAsync()
    {
        var menus = await db.Menus.AsNoTracking()
            .Where(m => m.UserId == User.FixedUserId && m.DeletedAt == null)
            .OrderByDescending(m => m.Id)
            .ToListAsync();
        var ids = menus.Select(m => m.Id).ToList();
        var recorded = await db.MealRecordItems.AsNoTracking()
            .Where(i => i.MenuId != null && i.PortionRatio != null && ids.Contains(i.MenuId!.Value))
            .GroupBy(i => i.MenuId!.Value)
            .Select(g => new { MenuId = g.Key, Sum = g.Sum(i => i.PortionRatio!.Value) })
            .ToDictionaryAsync(x => x.MenuId, x => x.Sum);

        return menus.Select(m =>
        {
            var remaining = PortionCalculator.Remaining([recorded.GetValueOrDefault(m.Id)], m.Servings);
            return new MenuSummaryDto(
                m.Id, m.Name, m.CookedOn, m.CookedTotalWeightG, m.Servings, m.ServingLabel,
                PortionCalculator.ServingWeightG(m.CookedTotalWeightG, m.Servings),
                new RemainingDto(remaining.Ratio, remaining.Servings, remaining.IsOver));
        }).ToList();
    }

    /// <summary>API-12 未保存メニューの栄養計算。DB は更新しない。無効な食材は 409 ではなく警告にする。</summary>
    public async Task<MenuPreviewDto> PreviewAsync(MenuInput input)
    {
        var foods = await ValidateAsync(input, preview: true);
        var codes = await catalog.CodesAsync();
        var reference = await catalog.ReferenceCodesAsync();

        var lines = new List<MenuLine>();
        var items = new List<MenuPreviewItemDto>();
        var warnings = new List<PreviewWarningDto>();
        foreach (var item in input.Items!)
        {
            var food = foods[item.FoodId!.Value];
            var unit = item.UnitId is { } unitId ? food.Units.Single(u => u.Id == unitId) : null;
            var amountG = UnitConverter.ToAmountG(RoundingPolicy.Round(item.Quantity!.Value, 2), unit?.GramsPerUnit);
            var data = NutritionCatalog.ToFoodData(food);
            lines.Add(new MenuLine(data, amountG));
            items.Add(new MenuPreviewItemDto(
                food.Id, food.Name, amountG, FoodNutritionCalculator.EdibleG(data, amountG),
                await catalog.ToDtosAsync(SnapshotCalculator.ForFood(FoodNutritionCalculator.Calculate(data, amountG, codes, reference)))));
            if (!food.IsActive && warnings.All(w => w.FoodId != food.Id))
            {
                warnings.Add(new PreviewWarningDto("FOOD_INACTIVE", food.Id, $"{food.Name}は無効になっている食材です"));
            }
        }

        var totals = MenuNutritionCalculator.Total(lines, codes, reference);
        decimal? servingWeightG = null;
        IReadOnlyList<NutrientAmountDto>? perServing = null;
        if (input.CookedTotalWeightG is { } cooked && input.Servings is { } servings)
        {
            cooked = RoundingPolicy.Round(cooked, 1);
            servings = RoundingPolicy.Round(servings, 1);
            servingWeightG = PortionCalculator.ServingWeightG(cooked, servings);
            var ratio = PortionCalculator.Ratio(InputMode.Serving, 1m, cooked, servings);
            perServing = await catalog.ToDtosAsync(SnapshotCalculator.ForMenu(totals, ratio));
        }
        return new MenuPreviewDto(items, await catalog.ToDtosAsync(SnapshotCalculator.ForFood(totals)), servingWeightG, perServing, warnings);
    }

    /// <summary>API-08 メニュー詳細。論理削除済みは 404。</summary>
    public async Task<MenuDetailDto> GetAsync(long id)
    {
        await RequireActiveAsync(id);
        return await ToDetailAsync(id);
    }

    /// <summary>
    /// API-09 メニュー更新（全項目置換）。過去の記録は栄養スナップショットを持つため変わらない（ADR-0002）。
    /// 論理削除済みは 404。
    /// </summary>
    public async Task<MenuDetailDto> UpdateAsync(long id, MenuInput input)
    {
        var menu = await db.Menus.Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == User.FixedUserId && m.DeletedAt == null)
            ?? throw new NotFoundException();
        var foods = await ValidateAsync(input);
        Apply(menu, input, foods);
        await db.SaveChangesAsync();
        return await ToDetailAsync(menu.Id);
    }

    /// <summary>API-10 メニュー削除（論理削除）。記録済みの食事記録項目は残る。存在しない・削除済みは 404。</summary>
    public async Task DeleteAsync(long id)
    {
        var menu = await RequireActiveAsync(id);
        menu.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>有効（論理削除されていない）メニューを返す。無ければ 404。</summary>
    private async Task<Menu> RequireActiveAsync(long id) =>
        await db.Menus.FirstOrDefaultAsync(m => m.Id == id && m.UserId == User.FixedUserId && m.DeletedAt == null)
        ?? throw new NotFoundException();

    // ---- 入力の検証と反映 ----

    /// <summary>入力を検証し、使う食材（栄養素つき）を返す。違反は 400（項目別）、無効な食材は 409。</summary>
    /// <param name="preview">プレビューのときは、名前・完成後重量・杯数を省略でき、無効な食材は 409 にせず <paramref name="inactive"/> に集める。</param>
    private async Task<Dictionary<long, Food>> ValidateAsync(MenuInput input, bool preview = false)
    {
        var errors = new List<ErrorDetail>();
        if (!preview && string.IsNullOrWhiteSpace(input.Name))
        {
            errors.Add(new ErrorDetail("name", "メニュー名を入力してください"));
        }
        else if (input.Name is { } name && name.Trim().Length > MaxNameLength)
        {
            errors.Add(new ErrorDetail("name", $"{MaxNameLength}文字以内で入力してください"));
        }
        if ((!preview || input.CookedTotalWeightG is not null)
            && (input.CookedTotalWeightG is not { } cooked || RoundingPolicy.Round(cooked, 1) <= 0m || cooked >= 10_000_000m))
        {
            errors.Add(new ErrorDetail("cookedTotalWeightG", "0より大きい値を指定してください"));
        }
        if ((!preview || input.Servings is not null)
            && (input.Servings is not { } servings || RoundingPolicy.Round(servings, 1) <= 0m || servings >= 10_000m))
        {
            errors.Add(new ErrorDetail("servings", "0より大きい値を指定してください"));
        }
        if (input.ServingLabel is { } label && label.Trim().Length > MaxLabelLength)
        {
            errors.Add(new ErrorDetail("servingLabel", $"{MaxLabelLength}文字以内で入力してください"));
        }
        if (input.Memo is { } memo && memo.Length > MaxMemoLength)
        {
            errors.Add(new ErrorDetail("memo", $"{MaxMemoLength}文字以内で入力してください"));
        }

        var items = input.Items ?? [];
        if (items.Count == 0)
        {
            errors.Add(new ErrorDetail("items", "食材を1件以上追加してください"));
        }

        var foodIds = items.Where(i => i.FoodId is not null).Select(i => i.FoodId!.Value).Distinct().ToList();
        var foods = await db.Foods
            .Include(f => f.Units)
            .Include(f => f.Nutrients).ThenInclude(n => n.Nutrient)
            .Where(f => foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id);

        var inactive = new List<ErrorDetail>();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.FoodId is null || !foods.TryGetValue(item.FoodId.Value, out var food))
            {
                errors.Add(new ErrorDetail($"items[{i}].foodId", "存在しない食材です"));
                continue;
            }
            if (item.Quantity is not { } quantity || quantity <= 0m || RoundingPolicy.Round(quantity, 2) <= 0m || quantity >= 1_000_000m)
            {
                errors.Add(new ErrorDetail($"items[{i}].quantity", "0より大きい値を指定してください"));
            }
            if (item.UnitId is { } unitId && food.Units.All(u => u.Id != unitId))
            {
                errors.Add(new ErrorDetail($"items[{i}].unitId", "この食材の単位ではありません"));
            }
            if (!food.IsActive)
            {
                inactive.Add(new ErrorDetail($"items[{i}].foodId", "FOOD_INACTIVE"));
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
        if (!preview && inactive.Count > 0)
        {
            throw new ConflictException("無効になっている食材は使えません", inactive);
        }
        return foods;
    }

    private static void Apply(Menu menu, MenuInput input, Dictionary<long, Food> foods)
    {
        menu.Name = input.Name!.Trim();
        menu.CookedOn = input.CookedOn;
        menu.Memo = string.IsNullOrWhiteSpace(input.Memo) ? null : input.Memo;
        menu.CookedTotalWeightG = RoundingPolicy.Round(input.CookedTotalWeightG!.Value, 1);
        menu.Servings = RoundingPolicy.Round(input.Servings!.Value, 1);
        menu.ServingLabel = string.IsNullOrWhiteSpace(input.ServingLabel) ? DefaultServingLabel : input.ServingLabel.Trim();

        menu.Items.Clear();
        var order = 0;
        foreach (var item in input.Items!)
        {
            var food = foods[item.FoodId!.Value];
            var unit = item.UnitId is { } unitId ? food.Units.Single(u => u.Id == unitId) : null;
            var quantity = RoundingPolicy.Round(item.Quantity!.Value, 2);
            menu.Items.Add(new MenuItem
            {
                FoodId = food.Id,
                UnitId = unit?.Id,
                Quantity = quantity,
                AmountG = UnitConverter.ToAmountG(quantity, unit?.GramsPerUnit), // 換算済みの重量（廃棄前）が計算の正
                SortOrder = order++,
            });
        }
    }

    // ---- 表示用の組み立て ----

    private async Task<MenuDetailDto> ToDetailAsync(long id)
    {
        var menu = await db.Menus.AsNoTracking()
            .Include(m => m.Items.OrderBy(i => i.SortOrder)).ThenInclude(i => i.Food).ThenInclude(f => f!.Nutrients).ThenInclude(n => n.Nutrient)
            .Include(m => m.Items).ThenInclude(i => i.Unit)
            .FirstAsync(m => m.Id == id);

        var codes = await catalog.CodesAsync();
        var reference = await catalog.ReferenceCodesAsync();
        var totals = MenuNutritionCalculator.Total(
            menu.Items.Select(i => new MenuLine(NutritionCatalog.ToFoodData(i.Food!), i.AmountG)), codes, reference);
        var perServingRatio = PortionCalculator.Ratio(InputMode.Serving, 1m, menu.CookedTotalWeightG, menu.Servings);

        return new MenuDetailDto(
            menu.Id, menu.Name, menu.CookedOn, menu.Memo, menu.CookedTotalWeightG, menu.Servings, menu.ServingLabel,
            PortionCalculator.ServingWeightG(menu.CookedTotalWeightG, menu.Servings),
            menu.Items.Select(i => new MenuItemDto(i.Id, i.FoodId, i.Food!.Name, i.UnitId, i.Unit?.UnitName, i.Quantity, i.AmountG, i.SortOrder)).ToList(),
            await catalog.ToDtosAsync(SnapshotCalculator.ForFood(totals)),
            await catalog.ToDtosAsync(SnapshotCalculator.ForMenu(totals, perServingRatio)),
            await RemainingAsync(menu));
    }

    /// <summary>残量 = 1 − そのメニューの全記録の食べた割合の合計（保存しない）。</summary>
    private async Task<RemainingDto> RemainingAsync(Menu menu)
    {
        var ratios = await db.MealRecordItems.AsNoTracking()
            .Where(i => i.MenuId == menu.Id && i.PortionRatio != null)
            .Select(i => i.PortionRatio!.Value)
            .ToListAsync();
        var remaining = PortionCalculator.Remaining(ratios, menu.Servings);
        return new RemainingDto(remaining.Ratio, remaining.Servings, remaining.IsOver);
    }
}
