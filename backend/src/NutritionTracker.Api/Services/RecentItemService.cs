using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;

namespace NutritionTracker.Api.Services;

public class RecentItemService(AppDbContext db)
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 30;

    /// <summary>
    /// API-20 最近使った食材・メニュー。直近の記録から重複を除き、使った日の新しい順に返す（メニュー・食材それぞれ <c>limit</c> 件まで）。
    /// 論理削除されたメニューと無効な食材は除く。
    /// </summary>
    public async Task<RecentItemsDto> GetAsync(int? limit)
    {
        if (limit is < 1)
        {
            throw new ValidationException("limit", "1以上の値を指定してください");
        }
        var size = Math.Min(limit ?? DefaultLimit, MaxLimit);

        var rows = db.MealRecordItems.AsNoTracking()
            .Where(i => i.MealRecord!.UserId == User.FixedUserId)
            .Where(i => (i.Menu != null && i.Menu.DeletedAt == null) || (i.Food != null && i.Food.IsActive))
            .OrderByDescending(i => i.MealRecord!.EatenOn).ThenByDescending(i => i.MealRecordId).ThenByDescending(i => i.Id)
            .Select(i => new
            {
                i.MenuId, MenuName = i.Menu != null ? i.Menu.Name : null,
                i.FoodId, FoodName = i.Food != null ? i.Food.Name : null,
                i.UnitId, i.InputMode, i.InputValue, EatenOn = i.MealRecord!.EatenOn,
            })
            .AsAsyncEnumerable();

        var menus = new List<RecentMenuDto>();
        var foods = new List<RecentFoodDto>();
        var seenMenus = new HashSet<long>();
        var seenFoods = new HashSet<long>();
        await foreach (var row in rows)
        {
            if (row.MenuId is { } menuId && menus.Count < size && seenMenus.Add(menuId))
            {
                menus.Add(new RecentMenuDto(menuId, row.MenuName!, row.InputMode, row.InputValue, row.EatenOn));
            }
            else if (row.FoodId is { } foodId && foods.Count < size && seenFoods.Add(foodId))
            {
                foods.Add(new RecentFoodDto(foodId, row.FoodName!, row.InputMode, row.InputValue, row.UnitId, row.EatenOn));
            }
            if (menus.Count >= size && foods.Count >= size)
            {
                break;
            }
        }
        return new RecentItemsDto(menus, foods);
    }
}
