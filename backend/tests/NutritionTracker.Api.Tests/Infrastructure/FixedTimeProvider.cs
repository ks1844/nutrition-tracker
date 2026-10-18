namespace NutritionTracker.Api.Tests.Infrastructure;

/// <summary>
/// 常に同じ時刻を返す時刻の提供元です。年齢・「今日」・7 日の窓のように日付で結果が変わる計算を、テストで毎回同じにするために使います。
/// </summary>
/// <param name="now">固定する時刻。</param>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>
    /// 固定した時刻を UTC で返します。
    /// </summary>
    /// <returns>固定した時刻。</returns>
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
