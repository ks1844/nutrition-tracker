using System.Text;

namespace NutritionTracker.Api.Services.MasterImport;

/// <summary>CSV の 1 行。<see cref="Line"/> はファイル内の行番号（1 始まり。ヘッダーが 1 行目）。</summary>
public sealed class CsvRow(string file, int line, IReadOnlyDictionary<string, string> values)
{
    public string File { get; } = file;

    public int Line { get; } = line;

    /// <summary>列の値（前後の空白を除く）。列が無い・空欄は空文字。</summary>
    public string this[string column] => values.GetValueOrDefault(column, "");

    public string Where => $"{File} {Line}行目";
}

/// <summary>
/// マスタ CSV（UTF-8、ヘッダー行あり、カンマ区切り。docs/07.マスタデータ仕様.md §2）。
/// 引用符で囲んだ値（カンマ・改行・<c>""</c> を含められる）に対応する。列の順序は問わず、仕様にない列は無視する。
/// </summary>
public sealed class CsvTable
{
    private CsvTable(string file, IReadOnlyList<string> header, IReadOnlyList<CsvRow> rows)
    {
        File = file;
        Header = header;
        Rows = rows;
    }

    public string File { get; }

    public IReadOnlyList<string> Header { get; }

    public IReadOnlyList<CsvRow> Rows { get; }

    /// <summary>ファイルが無ければ null。</summary>
    public static CsvTable? ReadIfExists(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        return System.IO.File.Exists(path) ? Parse(fileName, System.IO.File.ReadAllText(path, new UTF8Encoding(false))) : null;
    }

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

    /// <summary>必須列のうち、ヘッダーに無いものを返す。</summary>
    public IEnumerable<string> MissingColumns(params string[] required) => required.Where(c => !Header.Contains(c));

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
