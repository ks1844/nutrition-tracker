using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

/// <summary>
/// メニュー（作り置きの料理）の作成・参照・更新・削除・複製と、保存前の栄養計算を行うサービスです。
/// </summary>
/// <param name="db">データベースへの窓口。</param>
/// <param name="catalog">栄養素の情報と計算結果の変換。</param>
/// <param name="clock">今日の日付（複製したメニューの作った日に使います）。</param>
public class MenuService(AppDbContext db, NutritionCatalog catalog, AppClock clock)
{
    /// <summary>
    /// メニュー名の最大文字数（100 文字）。
    /// </summary>
    private const int MaxNameLength = 100;
    /// <summary>
    /// 1 杯の呼び方の最大文字数（20 文字）。
    /// </summary>
    private const int MaxLabelLength = 20;
    /// <summary>
    /// メモの最大文字数（500 文字）。
    /// </summary>
    private const int MaxMemoLength = 500;
    /// <summary>
    /// 1 杯の呼び方を省略したときの既定値（お椀）。
    /// </summary>
    private const string DefaultServingLabel = "お椀";

    /// <summary>
    /// メニューを作成して保存します（API-07）。
    /// 食材は g でも単位（本・大さじ など）でも指定でき、単位は重さ（廃棄前）に換算して保存します。
    /// </summary>
    /// <param name="input">名前・完成後の重さ・杯数・食材など。</param>
    /// <returns>作ったメニューの詳細（メニュー全体と 1 杯あたりの栄養量を含む）。入力に問題があれば 400、無効な食材が含まれていれば 409。</returns>
    public async Task<MenuDetailDto> CreateAsync(MenuInput input)
    {
        var foods = await ValidateAsync(input);
        var menu = new Menu { UserId = User.FixedUserId };
        Apply(menu, input, foods);
        db.Menus.Add(menu);
        await db.SaveChangesAsync();
        return await ToDetailAsync(menu.Id);
    }

    /// <summary>
    /// メニューの一覧を返します（API-06）。
    /// 削除していないメニューを新しい順に、1 杯の重さと残りの量つきで返します。残りは、そのメニューの記録済みの割合から計算します。
    /// </summary>
    /// <returns>メニューの一覧。</returns>
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

    /// <summary>
    /// 保存せずに、入力中のメニューの栄養量を計算します（API-12）。データベースは更新しません。
    /// 名前・完成後の重さ・杯数は省略でき（重さと杯数がそろったときだけ 1 杯あたりを返す）、無効な食材は 409 にせず警告にします。
    /// </summary>
    /// <param name="input">入力中のメニュー。</param>
    /// <returns>食材ごとの重さ・可食部・栄養量、全体と 1 杯あたりの栄養量、警告。</returns>
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

    /// <summary>
    /// メニュー 1 件の詳細を返します（API-08）。
    /// </summary>
    /// <param name="id">メニューの ID。</param>
    /// <returns>メニューの詳細。存在しない、または削除済みなら 404。</returns>
    public async Task<MenuDetailDto> GetAsync(long id)
    {
        await RequireActiveAsync(id);
        return await ToDetailAsync(id);
    }

    /// <summary>
    /// メニューを更新します（API-09）。名前・完成後の重さ・杯数・食材をすべて置き換えます。
    /// 記録済みの栄養量はその時点の値を保存してあるため、更新しても過去の記録は変わりません（docs/adr/0002）。
    /// </summary>
    /// <param name="id">更新するメニューの ID。</param>
    /// <param name="input">更新後の内容。</param>
    /// <returns>更新後のメニューの詳細。存在しない、または削除済みなら 404。</returns>
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

    /// <summary>
    /// 複製したメニューの名前の末尾に付ける言葉（「〜のコピー」）。
    /// </summary>
    private const string CopySuffix = "のコピー";

    /// <summary>
    /// メニューを複製します（API-11）。食材・完成後の重さ・杯数・メモを写した新しいメニューを作り、記録は複製しません。
    /// 名前を省略すると「元の名前のコピー」、作った日を省略すると今日になります。
    /// </summary>
    /// <param name="id">複製元のメニューの ID。</param>
    /// <param name="input">新しい名前と作った日（省略可）。</param>
    /// <returns>複製したメニューの詳細。複製元が存在しない、または削除済みなら 404。</returns>
    public async Task<MenuDetailDto> DuplicateAsync(long id, MenuDuplicateInput? input)
    {
        if (input?.Name is { } requested && (string.IsNullOrWhiteSpace(requested) || requested.Trim().Length > MaxNameLength))
        {
            throw new ValidationException([new ErrorDetail("name", $"1〜{MaxNameLength}文字で入力してください")]);
        }
        var source = await db.Menus.AsNoTracking().Include(m => m.Items)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == User.FixedUserId && m.DeletedAt == null)
            ?? throw new NotFoundException();

        var name = input?.Name?.Trim()
            ?? (source.Name.Length + CopySuffix.Length <= MaxNameLength ? source.Name : source.Name[..(MaxNameLength - CopySuffix.Length)]) + CopySuffix;
        var copy = new Menu
        {
            UserId = User.FixedUserId,
            Name = name,
            CookedOn = input?.CookedOn ?? clock.Today,
            Memo = source.Memo,
            CookedTotalWeightG = source.CookedTotalWeightG,
            Servings = source.Servings,
            ServingLabel = source.ServingLabel,
        };
        foreach (var item in source.Items.OrderBy(i => i.SortOrder))
        {
            copy.Items.Add(new MenuItem
            {
                FoodId = item.FoodId, UnitId = item.UnitId, Quantity = item.Quantity, AmountG = item.AmountG, SortOrder = item.SortOrder,
            });
        }
        db.Menus.Add(copy);
        await db.SaveChangesAsync();
        return await ToDetailAsync(copy.Id);
    }

    /// <summary>
    /// メニューを削除します（API-10）。削除日時を入れるだけの論理削除で、過去の記録は残ります。
    /// </summary>
    /// <param name="id">削除するメニューの ID。存在しない、または削除済みなら 404。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task DeleteAsync(long id)
    {
        var menu = await RequireActiveAsync(id);
        menu.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 削除されていないメニューを取り出します。
    /// </summary>
    /// <param name="id">メニューの ID。</param>
    /// <returns>メニュー。存在しない、または削除済みなら 404。</returns>
    private async Task<Menu> RequireActiveAsync(long id) =>
        await db.Menus.FirstOrDefaultAsync(m => m.Id == id && m.UserId == User.FixedUserId && m.DeletedAt == null)
        ?? throw new NotFoundException();

    // ---- 入力の検証と反映 ----

    /// <summary>
    /// メニューの入力を検証し、使う食材を取り出します。
    /// 名前・完成後の重さ・杯数・食材の数量や単位などを確かめて項目別の 400 にし、無効な食材は 409（プレビューのときは警告用に集めるだけ）にします。
    /// </summary>
    /// <param name="input">入力内容。</param>
    /// <param name="preview">プレビューのとき true。名前・完成後の重さ・杯数を省略でき、無効な食材でも 409 にしません。</param>
    /// <returns>食材の ID → 食材（換算単位と栄養素つき）。</returns>
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

    /// <summary>
    /// 検証済みの入力をメニューに反映します。
    /// 名前などを整え、食材を作り直します（単位入力は重さに換算し、換算した重さを計算の元として保存します）。
    /// </summary>
    /// <param name="menu">反映先のメニュー。</param>
    /// <param name="input">検証済みの入力。</param>
    /// <param name="foods">入力で使う食材。</param>
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

    /// <summary>
    /// 保存済みのメニューから、詳細（全体と 1 杯あたりの栄養量、残りを含む）を組み立てます。
    /// </summary>
    /// <param name="id">メニューの ID。</param>
    /// <returns>メニューの詳細。</returns>
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

    /// <summary>
    /// メニューの残りを計算します（1 − そのメニューの全記録の食べた割合の合計）。残りは保存せず、毎回記録から求めます。
    /// </summary>
    /// <param name="menu">メニュー。</param>
    /// <returns>残りの割合・杯数・食べすぎかどうか。</returns>
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
