namespace NutritionTracker.Api.Tests.Infrastructure;

/// <summary>テスト用の固定時刻（日付に依存する計算——年齢・今日・7 日窓——を決定的にする）。</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
