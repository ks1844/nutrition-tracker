namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>マスタ CSV の検証エラー。1 件でもあれば何も取り込まない。<see cref="Errors"/> は行番号つきの日本語メッセージ。</summary>
public sealed class MasterImportException(IReadOnlyList<string> errors)
    : Exception($"マスタ CSV に {errors.Count} 件のエラーがあります:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
