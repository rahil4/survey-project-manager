using System.Text;

namespace SurveyProjectManager.Core;

public sealed record BackupResult(int NewFiles, int Updated, int Unchanged, long Bytes, DateTime At);

public sealed record BackupLogEntry(DateTime At, int NewFiles, int Updated, long Bytes);

/// <summary>پشتیبان‌گیری افزایشی: فقط فایل‌های جدید یا تغییرکرده کپی می‌شوند و چیزی از مقصد پاک نمی‌شود.</summary>
public static class BackupService
{
    public const string LogName = "backup_log.csv";

    private static string Full(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

    private static StringComparison Cmp => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static void Validate(string root, string dest)
    {
        if (string.IsNullOrWhiteSpace(dest)) throw new ArgumentException("مقصد پشتیبان انتخاب نشده است.");
        var r = Full(root);
        var d = Full(dest);
        if (string.Equals(r, d, Cmp) || d.StartsWith(r, Cmp))
            throw new ArgumentException("مقصد پشتیبان نباید خود پوشه اصلی پروژه‌ها یا داخل آن باشد.");
        if (r.StartsWith(d, Cmp))
            throw new ArgumentException("مقصد پشتیبان نباید پوشه‌ای باشد که پوشه اصلی پروژه‌ها داخل آن است.");
    }

    private static bool NeedsCopy(FileInfo src, string dstPath, out bool isNew)
    {
        isNew = !File.Exists(dstPath);
        if (isNew) return true;
        var dst = new FileInfo(dstPath);
        return dst.Length != src.Length || (src.LastWriteTimeUtc - dst.LastWriteTimeUtc).TotalSeconds > 2;
    }

    public static BackupResult Run(string root, string dest, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        Validate(root, dest);
        Directory.CreateDirectory(dest);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)).ToList();
        int added = 0, updated = 0, unchanged = 0, done = 0;
        long bytes = 0;
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            var info = new FileInfo(f);
            var dstPath = Path.Combine(dest, Path.GetRelativePath(root, f));
            if (NeedsCopy(info, dstPath, out var isNew))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);
                if (!isNew) File.SetAttributes(dstPath, FileAttributes.Normal);
                File.Copy(f, dstPath, overwrite: true);
                File.SetAttributes(dstPath, FileAttributes.Normal);
                File.SetLastWriteTimeUtc(dstPath, info.LastWriteTimeUtc);
                if (info.Attributes.HasFlag(FileAttributes.ReadOnly)) File.SetAttributes(dstPath, FileAttributes.ReadOnly);
                bytes += info.Length;
                if (isNew) added++; else updated++;
            }
            else unchanged++;
            progress?.Report((++done, files.Count));
        }
        var result = new BackupResult(added, updated, unchanged, bytes, DateTime.Now);
        AppendLog(dest, result);
        return result;
    }

    private static void AppendLog(string dest, BackupResult r)
    {
        var path = Path.Combine(dest, LogName);
        var sb = new StringBuilder();
        if (!File.Exists(path)) sb.Append("at,new,updated,bytes\r\n");
        sb.Append(Csv.Line(new[] { r.At.ToString("yyyy-MM-dd HH:mm"), r.NewFiles.ToString(), r.Updated.ToString(), r.Bytes.ToString() })).Append("\r\n");
        File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static List<BackupLogEntry> History(string dest, int max = 10)
    {
        var rows = Csv.ReadTable(Path.Combine(dest, LogName));
        var list = new List<BackupLogEntry>();
        foreach (var r in rows)
        {
            if (!DateTime.TryParse(r.GetValueOrDefault("at", ""), out var at)) continue;
            int.TryParse(r.GetValueOrDefault("new", "0"), out var n);
            int.TryParse(r.GetValueOrDefault("updated", "0"), out var u);
            long.TryParse(r.GetValueOrDefault("bytes", "0"), out var b);
            list.Add(new BackupLogEntry(at, n, u, b));
        }
        return list.OrderByDescending(e => e.At).Take(max).ToList();
    }
}
