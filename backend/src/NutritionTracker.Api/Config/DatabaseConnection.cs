using MySqlConnector;

namespace NutritionTracker.Api.Config;

/// <summary>
/// データベースの接続文字列を組み立てる補助です。
/// </summary>
public static class DatabaseConnection
{
    /// <summary>
    /// 接続文字列を組み立てます。パスワードは設定ファイルに書かず、環境変数 Database__Password（設定キー Database:Password）で渡します（公開リポジトリにシークレットを置かないため。NFR-007）。
    /// </summary>
    /// <param name="configuration">アプリの設定。</param>
    /// <returns>パスワードを加えた接続文字列。</returns>
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
