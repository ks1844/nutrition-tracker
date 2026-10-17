using Microsoft.Extensions.Options;
using NutritionTracker.Api.Config;

namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>
/// 起動時にマスタ CSV を取り込む（<c>Nutrition:MasterImport:Enabled</c> が true のとき）。
/// CSV に検証エラーがあれば、何も取り込まずに起動を止める（docs/07.マスタデータ仕様.md §5）。
/// </summary>
public sealed class MasterImportHostedService(
    IServiceScopeFactory scopes,
    IOptions<NutritionOptions> options,
    IHostEnvironment environment,
    ILogger<MasterImportHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value.MasterImport;
        if (!settings.Enabled)
        {
            logger.LogInformation("マスタ CSV の取り込みは無効です");
            return;
        }

        var directory = Path.IsPathRooted(settings.Path) ? settings.Path : Path.Combine(environment.ContentRootPath, settings.Path);
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            var result = await scope.ServiceProvider.GetRequiredService<MasterImportService>().ImportAsync(directory, cancellationToken);
            if (result.Imported)
            {
                logger.LogInformation(
                    "マスタ CSV を取り込みました: 食材 {Foods} 件、栄養値 {Nutrients} 件、単位 {Units} 件、別名 {Aliases} 件、栄養目標 {Targets} 件（無効化 {Deactivated} 件、警告 {Warnings} 件）",
                    result.Foods, result.FoodNutrients, result.Units, result.Aliases, result.Targets, result.Deactivated, result.Warnings.Count);
            }
            else
            {
                logger.LogInformation("マスタ CSV がないため、取り込みをスキップしました: {Directory}", directory);
            }
        }
        catch (MasterImportException ex)
        {
            foreach (var error in ex.Errors)
            {
                logger.LogError("マスタ取り込みエラー: {Error}", error);
            }
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
