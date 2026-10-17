using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutritionTracker.Api.Config;
using NutritionTracker.Api.Data;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Exceptions;
using NutritionTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(DatabaseConnection.Build(builder.Configuration), new MySqlServerVersion(new Version(8, 4, 0))));

builder.Services.Configure<NutritionOptions>(builder.Configuration.GetSection(NutritionOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AppClock>();
builder.Services.AddScoped<NutrientService>();
builder.Services.AddScoped<FoodService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<NutritionCatalog>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<MealRecordService>();
builder.Services.AddScoped<RecentItemService>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // モデル検証の違反も、項目ごとの詳細つきの VALIDATION_ERROR（400）で返す
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .Select(e => new ErrorDetail(e.Key, e.Value!.Errors[0].ErrorMessage))
                .ToList();
            return new BadRequestObjectResult(new ApiErrorResponse("VALIDATION_ERROR", "入力内容に誤りがあります", details));
        };
    });

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// ヘルスチェック（docs/09.API仕様.md §1）
app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapControllers();

// 存在しない API は NOT_FOUND 形式で返す
app.Map("/api/{**path}", () => { throw new NotFoundException(); });

app.Run();

// WebApplicationFactory（API テスト）から参照するため
public partial class Program;
