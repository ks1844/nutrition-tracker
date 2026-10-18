namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>
/// マスタ CSV に検証エラーがあったことを表す例外です。1 件でもあれば、何も取り込みません。
/// </summary>
/// <param name="errors">「ファイル名 n行目: 内容」の形のエラー文言の一覧。</param>
public sealed class MasterImportException(IReadOnlyList<string> errors)
    : Exception($"マスタ CSV に {errors.Count} 件のエラーがあります:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}")
{
    /// <summary>
    /// 行番号つきのエラー文言の一覧。
    /// </summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
