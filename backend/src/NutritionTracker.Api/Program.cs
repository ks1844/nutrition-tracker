var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// ヘルスチェック（docs/09.API仕様.md §1）
app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.Run();

// WebApplicationFactory（API テスト）から参照するため
public partial class Program;
