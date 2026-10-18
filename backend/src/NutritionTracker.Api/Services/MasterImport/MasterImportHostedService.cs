using Microsoft.Extensions.Options;
using NutritionTracker.Api.Config;

namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>
/// アプリの起動時にマスタ CSV を取り込みます（設定 Nutrition:MasterImport:Enabled が true のとき）。
/// CSV に検証エラーがあれば、何も取り込まずに起動を止めます（docs/07.マスタデータ仕様.md §5）。
/// </summary>
/// <param name="scopes">取り込みに使うサービスを取り出すための窓口。</param>
/// <param name="options">取り込みの設定（有効・無効、フォルダ）。</param>
/// <param name="environment">アプリの実行環境（相対パスの基準になるフォルダ）。</param>
/// <param name="logger">ログの出力先。</param>
public sealed class MasterImportHostedService(
    IServiceScopeFactory scopes,
    IOptions<NutritionOptions> options,
    IHostEnvironment environment,
    ILogger<MasterImportHostedService> logger) : IHostedService
{
    /// <summary>
    /// アプリの起動時に呼ばれ、CSV を取り込みます。無効な設定や CSV がないときは何もしません。エラーはログに全件出して、起動を止めます。
    /// </summary>
    /// <param name="cancellationToken">中止の合図。</param>
    /// <returns>処理の完了を表すタスク。</returns>
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

    /// <summary>
    /// アプリの終了時に呼ばれます。後始末はありません。
    /// </summary>
    /// <param name="cancellationToken">中止の合図。</param>
    /// <returns>完了済みのタスク。</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
