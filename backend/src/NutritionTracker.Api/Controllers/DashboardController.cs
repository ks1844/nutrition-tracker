using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services;

namespace NutritionTracker.Api.Controllers;

/// <summary>
/// ホーム画面やダッシュボードの集計（日次・直近 7 日）を返す API の入口です。
/// </summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>
    /// 指定した日の摂取状況を返します（GET /api/dashboard/daily、API-17）。
    /// エネルギー・PFC 比・栄養素ごとの達成率と状態・美肌/筋肉/基礎の 3 スコア（その日の進捗）・メニューの残り・未測定の注記を含みます。
    /// </summary>
    /// <param name="date">集計する日（必須）。</param>
    /// <returns>その日のダッシュボード。プロフィール未登録は 404、日付なしは 400。</returns>
    [HttpGet("daily")]
    public async Task<DailyDashboardDto> Daily([FromQuery] DateOnly? date) => await dashboard.DailyAsync(date);

    /// <summary>
    /// 指定した日までの直近 7 日の摂取状況を返します（GET /api/dashboard/weekly、API-18）。
    /// 記録のある日だけで平均した摂取量とスコア、日ごとの推移、期間内に上限を超えた日数を含みます。記録が 3 日未満ならスコアは出しません。
    /// </summary>
    /// <param name="endDate">7 日間の最後の日（必須。未来日は不可）。</param>
    /// <returns>直近 7 日のダッシュボード。</returns>
    [HttpGet("weekly")]
    public async Task<WeeklyDashboardDto> Weekly([FromQuery] DateOnly? endDate) => await dashboard.WeeklyAsync(endDate);
}
