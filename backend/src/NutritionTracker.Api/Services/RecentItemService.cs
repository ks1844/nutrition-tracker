using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Entities;
using NutritionTracker.Api.Exceptions;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 最近記録に使ったメニュー・食材を調べるサービスです。
/// </summary>
public class RecentItemService(AppDbContext db)
{
    /// <summary>
    /// 返す件数の既定値（10 件）。
    /// </summary>
    private const int DefaultLimit = 10;
    /// <summary>
    /// 返せる件数の上限（30 件）。
    /// </summary>
    private const int MaxLimit = 30;

    /// <summary>
    /// 最近記録に使ったメニューと食材を返します（API-20）。
    /// 食事日の新しい順に記録を読み、同じメニュー・食材は最初の 1 件だけに絞ります（最後に使ったときの入力方式・入力値つき）。
    /// 削除済みのメニューと無効な食材は除きます。
    /// </summary>
    /// <param name="limit">メニュー・食材それぞれの最大件数（既定 10、最大 30。1 未満は 400）。</param>
    /// <returns>最近使ったメニューと食材。</returns>
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
