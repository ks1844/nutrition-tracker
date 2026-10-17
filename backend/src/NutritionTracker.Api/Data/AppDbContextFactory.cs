using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NutritionTracker.Api.Data;

/// <summary>
/// <c>dotnet ef</c>（マイグレーションの作成）用。サーバーのバージョンを明示するので、DB に接続せずにマイグレーションを作れる。
/// 接続先は環境変数 <c>ConnectionStrings__Default</c>（未設定なら既定のローカル接続）。
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
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
