using Microsoft.Extensions.Options;
using NutritionTracker.Api.Config;

namespace NutritionTracker.Api.Services;

/// <summary>
/// 「今日」の日付を返します。サーバーの時刻設定に左右されないよう、設定のタイムゾーン（既定は Asia/Tokyo）で 0 時区切りにします（日付の区切りは暫定。Q-5）。
/// </summary>
/// <param name="timeProvider">現在時刻の提供元（テストでは固定の時刻に差し替えます）。</param>
/// <param name="options">タイムゾーンの設定。</param>
public class AppClock(TimeProvider timeProvider, IOptions<NutritionOptions> options)
{
    /// <summary>
    /// 設定から読んだタイムゾーン。
    /// </summary>
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    /// <summary>
    /// 設定のタイムゾーンでの今日の日付。
    /// </summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _zone).DateTime);
}
