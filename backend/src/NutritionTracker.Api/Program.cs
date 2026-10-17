using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Config;
using NutritionTracker.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(DatabaseConnection.Build(builder.Configuration), new MySqlServerVersion(new Version(8, 4, 0))));

var app = builder.Build();

// ヘルスチェック（docs/09.API仕様.md §1）
app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.Run();

// WebApplicationFactory（API テスト）から参照するため
public partial class Program;
