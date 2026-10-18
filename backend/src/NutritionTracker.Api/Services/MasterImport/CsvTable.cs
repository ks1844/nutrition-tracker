using System.Text;

namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>
/// CSV の 1 行分です。列名を指定して値を取り出せます。
/// </summary>
/// <param name="file">ファイル名。</param>
/// <param name="line">ファイル内の行番号（ヘッダーが 1 行目）。</param>
/// <param name="values">列名 → 値。</param>
public sealed class CsvRow(string file, int line, IReadOnlyDictionary<string, string> values)
{
    /// <summary>
    /// この行があるファイル名。
    /// </summary>
    public string File { get; } = file;

    /// <summary>
    /// ファイル内の行番号（1 始まり。ヘッダーが 1 行目）。
    /// </summary>
    public int Line { get; } = line;

    /// <summary>
    /// 列の値を返します。前後の空白は除きます。
    /// </summary>
    /// <param name="column">列名。</param>
    /// <returns>値。列がない、または空欄なら空文字。</returns>
    public string this[string column] => values.GetValueOrDefault(column, "");

    /// <summary>
    /// エラー文言に使う「ファイル名 n行目」。
    /// </summary>
    public string Where => $"{File} {Line}行目";
}

/// <summary>
/// マスタ CSV（UTF-8、ヘッダー行あり、カンマ区切り）を読み込んだ表です（docs/07.マスタデータ仕様.md §2）。
/// 引用符で囲んだ値（カンマ・改行・二重引用符を含められる）に対応します。列の順序は問わず、仕様にない列は無視します。
/// </summary>
public sealed class CsvTable
{
    /// <summary>
    /// 読み込み済みの表を作ります（Parse から使います）。
    /// </summary>
    /// <param name="file">ファイル名。</param>
    /// <param name="header">ヘッダー行の列名。</param>
    /// <param name="rows">データ行。</param>
    private CsvTable(string file, IReadOnlyList<string> header, IReadOnlyList<CsvRow> rows)
    {
        File = file;
        Header = header;
        Rows = rows;
    }

    /// <summary>
    /// ファイル名。
    /// </summary>
    public string File { get; }

    /// <summary>
    /// ヘッダー行の列名。
    /// </summary>
    public IReadOnlyList<string> Header { get; }

    /// <summary>
    /// データ行（空行は含みません）。
    /// </summary>
    public IReadOnlyList<CsvRow> Rows { get; }

    /// <summary>
    /// 指定したフォルダの CSV ファイルを読み込みます。
    /// </summary>
    /// <param name="directory">フォルダ。</param>
    /// <param name="fileName">ファイル名。</param>
    /// <returns>読み込んだ表。ファイルがなければ null。</returns>
    public static CsvTable? ReadIfExists(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        return System.IO.File.Exists(path) ? Parse(fileName, System.IO.File.ReadAllText(path, new UTF8Encoding(false))) : null;
    }

    /// <summary>
    /// CSV の文字列を表に変換します。先頭の BOM と空行は無視します。
    /// </summary>
    /// <param name="fileName">エラー文言に使うファイル名。</param>
    /// <param name="text">CSV の内容。</param>
    /// <returns>表。</returns>
    public static CsvTable Parse(string fileName, string text)
    {
        var records = Split(text.TrimStart('﻿'));
        if (records.Count == 0)
        {
            return new CsvTable(fileName, [], []);
        }
        var header = records[0].Fields.Select(h => h.Trim()).ToList();
        var rows = new List<CsvRow>();
        foreach (var (line, fields) in records.Skip(1))
        {
            var values = new Dictionary<string, string>();
            for (var i = 0; i < header.Count && i < fields.Count; i++)
            {
                values[header[i]] = fields[i].Trim();
            }
            rows.Add(new CsvRow(fileName, line, values));
        }
        return new CsvTable(fileName, header, rows);
    }

    /// <summary>
    /// 必須の列のうち、ヘッダーにないものを返します。
    /// </summary>
    /// <param name="required">必須の列名。</param>
    /// <returns>足りない列名。</returns>
    public IEnumerable<string> MissingColumns(params string[] required) => required.Where(c => !Header.Contains(c));

    /// <summary>
    /// CSV の文字列を、行ごとの項目の並びに分けます（引用符の中のカンマ・改行・二重引用符を正しく扱います）。
    /// </summary>
    /// <param name="text">CSV の内容。</param>
    /// <returns>行番号と項目の並び。</returns>
    private static List<(int Line, List<string> Fields)> Split(string text)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;
        var hasContent = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            // 空行（区切りだけの行も含む）は読み飛ばす
            if (hasContent)
            {
                records.Add((recordLine, fields));
            }
            fields = [];
            hasContent = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                    if (c == '\n')
                    {
                        line++;
                    }
                }
                continue;
            }
            switch (c)
            {
                case '"':
                    inQuotes = true;
                    hasContent = true;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRecord();
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(c);
                    hasContent = hasContent || !char.IsWhiteSpace(c);
                    break;
            }
        }
        EndRecord();
        return records;
    }
}
