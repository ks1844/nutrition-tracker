using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NutritionTracker.Api.Data;
using Testcontainers.MySql;

namespace NutritionTracker.Api.Tests.Infrastructure;

/// <summary>
/// API テスト用の実データベース（Testcontainers の MySQL 8.4）と、そこにつながる Web アプリです。テスト全体で 1 つを共有します。
/// マイグレーション V1〜V4 を適用した状態で始まります。
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    /// <summary>
    /// テスト用に起動する MySQL 8.4 のコンテナ。
    /// </summary>
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4").Build();
    /// <summary>
    /// テスト用の Web アプリ。
    /// </summary>
    private Factory? _factory;

    /// <summary>
    /// テスト中の「今日」（Asia/Tokyo の 2026-10-17）。
    /// </summary>
    public static readonly DateOnly Today = new(2026, 10, 17);

    /// <summary>
    /// テスト用の Web アプリに API を呼ぶ HTTP クライアント。
    /// </summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>
    /// テスト用の Web アプリのサービス（DbContext などを取り出すために使います）。
    /// </summary>
    public IServiceProvider Services => _factory!.Services;

    /// <summary>
    /// テストの開始前に、MySQL を起動して Web アプリをつなぎ、マイグレーションを適用します。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public Task InitializeAsync() => InitializeCoreAsync();

    /// <summary>
    /// テストの終了後に、Web アプリと MySQL を片づけます。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public Task DisposeAsync() => DisposeCoreAsync();

    /// <summary>
    /// MySQL の起動、Web アプリの作成、マイグレーションの適用を行います。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    private async Task InitializeCoreAsync()
    {
        await _container.StartAsync();
        _factory = new Factory(_container.GetConnectionString());
        Client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    /// <summary>
    /// クライアント、Web アプリ、MySQL のコンテナを順に破棄します。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    private async Task DisposeCoreAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _container.DisposeAsync();
    }

    /// <summary>
    /// テストごとに、データを初期状態（栄養素 24 件と固定ユーザーだけ）に戻します。マスタ・メニュー・記録・プロフィールを空にします。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public async Task ResetAsync()
    {
        await WithDbAsync(async db =>
        {
            const string sql = """
                SET FOREIGN_KEY_CHECKS = 0;
                TRUNCATE TABLE `T食事記録項目栄養素`;
                TRUNCATE TABLE `T食事記録項目`;
                TRUNCATE TABLE `T食事記録`;
                TRUNCATE TABLE `Tメニュー食材`;
                TRUNCATE TABLE `Tメニュー`;
                TRUNCATE TABLE `Tユーザープロフィール`;
                TRUNCATE TABLE `T栄養目標`;
                TRUNCATE TABLE `T食材単位`;
                TRUNCATE TABLE `T食材栄養素`;
                TRUNCATE TABLE `T食材別名`;
                TRUNCATE TABLE `T食材`;
                SET FOREIGN_KEY_CHECKS = 1;
                """;
            await db.Database.ExecuteSqlRawAsync(sql);
        });
    }

    /// <summary>
    /// テストのためにデータベースを直接操作します（準備や結果の確認に使います）。
    /// </summary>
    /// <param name="action">DbContext を使う処理。</param>
    /// <returns>完了を表すタスク。</returns>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// テストのためにデータベースを直接操作し、結果を受け取ります。
    /// </summary>
    /// <param name="action">DbContext を使って値を返す処理。</param>
    /// <typeparam name="T">返す値の型。</typeparam>
    /// <returns>処理の結果。</returns>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// テスト用の Web アプリの設定です。接続先をテスト用の MySQL にし、起動時のマスタ取り込みを止め、時刻を固定します。
    /// </summary>
    /// <param name="connectionString">テスト用の MySQL の接続文字列。</param>
    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        /// <summary>
        /// テスト用の設定（実行環境、接続先、起動時の取り込みの無効化、固定した現在時刻）を Web アプリに加えます。
        /// </summary>
        /// <param name="builder">Web アプリのビルダー。</param>
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = connectionString,
                ["Nutrition:MasterImport:Enabled"] = "false", // テストでは起動時に取り込まない（取り込みは MasterImportTests で直接検証）
                ["Database:Password"] = "", // 接続文字列にパスワードが含まれているので、環境変数のパスワードで上書きしない
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.FromHours(9))));
            });
        }
    }
}

/// <summary>
/// API テストが同じ実データベースを共有して、順番に（並列にせずに）実行されるようにするための定義です。
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    /// <summary>
    /// テストのグループ名。
    /// </summary>
    public const string Name = "Api（実 DB）";
}
