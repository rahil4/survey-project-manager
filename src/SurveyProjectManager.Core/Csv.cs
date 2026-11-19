using System.Text;

namespace SurveyProjectManager.Core;

/// <summary>خواندن و نوشتن CSV ساده (سازگار با اکسل: UTF-8 با BOM).</summary>
public static class Csv
{
    public static string Quote(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    public static string Line(IEnumerable<string> cells) => string.Join(",", cells.Select(Quote));

    public static List<List<string>> Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false, any = false;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else inQuotes = false;
                }
                else cell.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': inQuotes = true; any = true; break;
                case ',': row.Add(cell.ToString()); cell.Clear(); any = true; break;
                case '\r': break;
                case '\n':
                    row.Add(cell.ToString()); cell.Clear();
                    rows.Add(row); row = new List<string>(); any = false;
                    break;
                default: cell.Append(c); any = true; break;
            }
        }
        if (any || cell.Length > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    /// <summary>ردیف‌ها را به‌صورت فهرست دیکشنری (نام ستون ← مقدار) برمی‌گرداند.</summary>
    public static List<Dictionary<string, string>> ReadTable(string path)
    {
        var result = new List<Dictionary<string, string>>();
        if (!File.Exists(path)) return result;
        var rows = Parse(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count == 0) return result;
        var cols = rows[0].Select(c => c.Trim()).ToList();
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var d = new Dictionary<string, string>();
            for (int i = 0; i < cols.Count; i++) d[cols[i]] = i < row.Count ? row[i] : "";
            result.Add(d);
        }
        return result;
    }

    /// <summary>ابتدا در فایل موقت می‌نویسد تا اگر اکسل فایل را باز کرده باشد، فایل اصلی خراب نشود.</summary>
    public static void WriteTable(string path, IReadOnlyList<string> header, IEnumerable<IEnumerable<string>> rows)
    {
        var tmp = path + ".tmp";
        var sb = new StringBuilder();
        sb.Append(Line(header)).Append("\r\n");
        foreach (var r in rows) sb.Append(Line(r)).Append("\r\n");
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Move(tmp, path, overwrite: true);
    }
}
