using Microsoft.Extensions.Options;
using NutritionTracker.Api.Config;

namespace NutritionTracker.Api.Services;

/// <summary>「今日」の日付。サーバーのタイムゾーンに依存しないよう、設定のタイムゾーン（既定 Asia/Tokyo）で 0:00 区切りにする（Q-5 の暫定）。</summary>
public class AppClock(TimeProvider timeProvider, IOptions<NutritionOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), _zone).DateTime);
}
