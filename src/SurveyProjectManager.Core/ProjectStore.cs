using System.Text;

namespace SurveyProjectManager.Core;

/// <summary>فهرست پروژه‌ها در فایل projects.csv (قابل‌باز شدن در اکسل).</summary>
public static class ProjectStore
{
    public const string CsvName = "projects.csv";
    private static readonly string[] Header = { "number", "owner", "location", "type", "status", "due", "folder", "note" };

    public static string CsvPath(string root) => Path.Combine(root, CsvName);

    public static List<Project> Load(string root)
    {
        var path = CsvPath(root);
        if (!File.Exists(path)) return new List<Project>();
        var rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        var result = new List<Project>();
        if (rows.Count == 0) return result;
        var cols = rows[0].Select((name, i) => (name, i)).ToDictionary(x => x.name.Trim(), x => x.i);
        string Cell(List<string> row, string name) =>
            cols.TryGetValue(name, out var i) && i < row.Count ? row[i] : "";
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            result.Add(new Project
            {
                Number = Cell(row, "number"), Owner = Cell(row, "owner"), Location = Cell(row, "location"),
                Type = Cell(row, "type"), Status = Cell(row, "status"), Due = Cell(row, "due"),
                Folder = Cell(row, "folder"), Note = Cell(row, "note"),
            });
        }
        return result;
    }

    /// <summary>ابتدا در فایل موقت می‌نویسد تا اگر اکسل فایل را باز کرده باشد خراب نشود.</summary>
    public static void Save(string root, IEnumerable<Project> projects)
    {
        var path = CsvPath(root);
        var tmp = path + ".tmp";
        var sb = new StringBuilder();
        sb.Append(string.Join(",", Header)).Append("\r\n");
        foreach (var p in projects)
        {
            sb.Append(string.Join(",", new[] { p.Number, p.Owner, p.Location, p.Type, p.Status, p.Due, p.Folder, p.Note }.Select(Quote)))
              .Append("\r\n");
        }
        File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Move(tmp, path, overwrite: true);
    }

    public static string NextNumber(IEnumerable<Project> projects, int year)
    {
        var prefix = $"{year}-";
        var max = 0;
        foreach (var p in projects)
            if (p.Number.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(p.Number[prefix.Length..], out var n))
                max = Math.Max(max, n);
        return $"{year}-{max + 1:000}";
    }

    private static string Quote(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    internal static List<List<string>> ParseCsv(string text)
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
}
