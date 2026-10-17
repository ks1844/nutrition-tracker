using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Services;

public class ContributionService(AppDbContext db, NutritionCatalog catalog)
{
    private const int MaxDays = 31;

    /// <summary>
    /// API-19 栄養素の寄与食材。期間内の記録を、保存済みスナップショットからメニュー・単品食材ごとに集計し、寄与の大きい順に返す。
    /// メニューの <c>children</c> は、メニューの<b>現在の構成</b>で按分した参考値（記録後にメニューを編集していると実際とずれうる。暫定）。
    /// </summary>
    public async Task<ContributionsDto> GetAsync(string code, DateOnly? from, DateOnly? to)
    {
        var nutrient = (await catalog.NutrientsAsync()).FirstOrDefault(n => n.Code == code)
            ?? throw new NotFoundException("栄養素が見つかりません");
        var errors = new List<ErrorDetail>();
        if (from is null)
        {
            errors.Add(new ErrorDetail("from", "開始日を指定してください"));
        }
        if (to is null)
        {
            errors.Add(new ErrorDetail("to", "終了日を指定してください"));
        }
        if (errors.Count == 0 && from > to)
        {
            errors.Add(new ErrorDetail("from", "from は to 以前の日付を指定してください"));
        }
        else if (errors.Count == 0 && to!.Value.DayNumber - from!.Value.DayNumber + 1 > MaxDays)
        {
            errors.Add(new ErrorDetail("to", $"期間は最大 {MaxDays} 日です"));
        }
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
        var start = from!.Value;
        var end = to!.Value;

        var rows = await db.MealRecordItemNutrients.AsNoTracking()
            .Where(n => n.NutrientId == nutrient.Id
                && n.MealRecordItem!.MealRecord!.UserId == User.FixedUserId
                && n.MealRecordItem.MealRecord.EatenOn >= start && n.MealRecordItem.MealRecord.EatenOn <= end)
            .Select(n => new { n.MealRecordItem!.MenuId, n.MealRecordItem.FoodId, n.MealRecordItem.PortionRatio, n.Amount, n.IsIncomplete })
            .ToListAsync();

        var total = rows.Sum(r => r.Amount);
        var groups = rows
            .GroupBy(r => (r.MenuId, r.FoodId))
            .Select(g => (g.Key.MenuId, g.Key.FoodId, Amount: g.Sum(r => r.Amount), Ratio: g.Sum(r => r.PortionRatio ?? 0m)))
            .Where(g => g.Amount > 0m)
            .OrderByDescending(g => g.Amount)
            .ToList();

        var menuIds = groups.Where(g => g.MenuId is not null).Select(g => g.MenuId!.Value).ToList();
        var menus = await db.Menus.AsNoTracking()
            .Include(m => m.Items).ThenInclude(i => i.Food).ThenInclude(f => f!.Nutrients).ThenInclude(n => n.Nutrient)
            .Where(m => menuIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);
        var foodIds = groups.Where(g => g.FoodId is not null).Select(g => g.FoodId!.Value).ToList();
        var foodNames = await db.Foods.AsNoTracking().Where(f => foodIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Name);
        var codes = await catalog.CodesAsync();
        var reference = await catalog.ReferenceCodesAsync();

        decimal Display(decimal amount) => SnapshotCalculator.ForDisplay(amount, nutrient.Unit);
        decimal Share(decimal amount) => total == 0m ? 0m : RoundingPolicy.Round(amount / total * 100m, 1);

        var items = new List<ContributionItemDto>();
        foreach (var g in groups)
        {
            if (g.MenuId is { } menuId)
            {
                var menu = menus[menuId];
                var children = menu.Items
                    .GroupBy(i => i.FoodId)
                    .Select(fg =>
                    {
                        var food = fg.First().Food!;
                        var perFood = fg.Sum(i => FoodNutritionCalculator.Calculate(NutritionCatalog.ToFoodData(food), i.AmountG, codes, reference)[code].Amount);
                        return new ContributionChildDto(food.Id, food.Name, Display(perFood * g.Ratio));
                    })
                    .Where(c => c.Amount > 0m)
                    .OrderByDescending(c => c.Amount)
                    .ToList();
                items.Add(new ContributionItemDto("MENU", menuId, menu.Name, Display(g.Amount), Share(g.Amount), children));
            }
            else
            {
                items.Add(new ContributionItemDto("FOOD", g.FoodId!.Value, foodNames[g.FoodId.Value], Display(g.Amount), Share(g.Amount), []));
            }
        }

        return new ContributionsDto(code, start, end, Display(total), rows.Any(r => r.IsIncomplete), items);
    }
}
