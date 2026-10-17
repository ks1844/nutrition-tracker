using MySqlConnector;

namespace NutritionTracker.Api.Config;

/// <summary>
/// 接続文字列を組み立てる。パスワードは設定ファイルに書かず、環境変数 <c>Database__Password</c>
/// （設定キー Database:Password）で渡す（公開リポジトリにシークレットを置かない: NFR-007）。
/// </summary>
public static class DatabaseConnection
{
    public static string Build(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default が設定されていません。");
        var password = configuration["Database:Password"];
        if (string.IsNullOrEmpty(password))
        {
            return connectionString;
        }
        return new MySqlConnectionStringBuilder(connectionString) { Password = password }.ConnectionString;
    }
}
