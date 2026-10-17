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
/// API テスト用の実 DB（Testcontainers の MySQL 8.4）と、そこへ接続する Web アプリ。テスト全体で 1 つを共有する。
/// マイグレーション V1〜V4 を適用した状態で始まる。
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4").Build();
    private Factory? _factory;

    /// <summary>テスト中の「今日」（Asia/Tokyo の 2026-10-17）。</summary>
    public static readonly DateOnly Today = new(2026, 10, 17);

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services => _factory!.Services;

    public Task InitializeAsync() => InitializeCoreAsync();

    public Task DisposeAsync() => DisposeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        await _container.StartAsync();
        _factory = new Factory(_container.GetConnectionString());
        Client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    private async Task DisposeCoreAsync()
    {
        Client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _container.DisposeAsync();
    }

    /// <summary>テストごとに、データを初期状態（栄養素 24 件と固定ユーザーだけ）に戻す。</summary>
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

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
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

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api（実 DB）";
}
