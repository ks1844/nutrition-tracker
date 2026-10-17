using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NutritionTracker.Api.Data;

/// <summary>
/// dotnet ef（マイグレーションの作成・適用）が DbContext を作るための工場です。
/// サーバーのバージョンを明示するので、データベースに接続せずにマイグレーションを作れます。接続先は環境変数 ConnectionStrings__Default（なければローカルの既定）です。
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// dotnet ef 用の DbContext を作ります。
    /// </summary>
    /// <param name="args">dotnet ef から渡される引数（使いません）。</param>
    /// <returns>DbContext。</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Server=localhost;Port=3306;Database=nutritiontracker;User ID=nutrition;";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;
        return new AppDbContext(options);
    }
}
