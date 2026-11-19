using System.Text.RegularExpressions;

namespace SurveyProjectManager.Core;

public sealed class ImportItem
{
    public string Source { get; init; } = "";
    public bool IsDirectory { get; init; }
    public string OriginalName { get; init; } = "";
    /// <summary>نام نهایی فایل در مقصد (ممکن است برای نسخه‌گذاری فرق کند).</summary>
    public string TargetName { get; set; } = "";
    public string? Folder { get; set; }
    public string Reason { get; set; } = "";
    public bool Selected { get; set; }
    public bool Conflict { get; set; }
    public bool NewFolder { get; set; }
    public string VersionNote { get; set; } = "";
    public List<string> ArchiveOlder { get; set; } = new();
}

public sealed record ImportResult(List<string> Copied, List<string> Skipped, List<string> CreatedFolders, List<string> Archived);

/// <summary>برنامه‌ریزی و اجرای افزودن فایل‌ها به پروژه با پیشنهاد پوشه و نسخه‌گذاری نقشه.</summary>
public static partial class ImportPlanner
{
    public const string CadFolder = "04_CAD";
    public const string ArchiveFolder = "99_Archive";

    [GeneratedRegex(@"^(?<base>.*?)[_\-\s][vV](?<ver>\d+)$")]
    private static partial Regex VersionRx();

    [GeneratedRegex(@"^\d{4}-\d{3}_")]
    private static partial Regex NumberPrefixRx();

    public static List<ImportItem> Plan(string projectDir, IEnumerable<string> sources)
    {
        var items = new List<ImportItem>();
        foreach (var src in sources)
        {
            var isDir = Directory.Exists(src);
            var name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var s = FileClassifier.Classify(name, isDir);
            var item = new ImportItem { Source = src, IsDirectory = isDir, OriginalName = name, TargetName = name, Folder = s.Folder, Reason = s.Reason };
            Refresh(projectDir, item, resetSelection: true);
            items.Add(item);
        }
        return items;
    }

    private static string FamilyKey(string stem) => NumberPrefixRx().Replace(TextNorm.Normalize(stem), "");

    /// <summary>بعد از هر تغییر پوشه مقصد، وضعیت (پوشه جدید، تکراری، نسخه) دوباره محاسبه می‌شود.</summary>
    public static void Refresh(string projectDir, ImportItem item, bool resetSelection = false)
    {
        item.TargetName = item.OriginalName;
        item.VersionNote = "";
        item.ArchiveOlder = new List<string>();
        item.NewFolder = item.Folder != null && !Directory.Exists(Path.Combine(projectDir, item.Folder));
        if (item.Folder == CadFolder && !item.IsDirectory) ApplyVersioning(projectDir, item);
        item.Conflict = item.Folder != null &&
            (File.Exists(Path.Combine(projectDir, item.Folder, item.TargetName)) || Directory.Exists(Path.Combine(projectDir, item.Folder, item.TargetName)));
        if (item.Conflict) item.VersionNote = "فایلی با این نام وجود دارد و رونویسی نمی‌شود";
        if (resetSelection) item.Selected = item.Folder != null && !item.Conflict;
        else if (item.Conflict || item.Folder == null) item.Selected = false;
    }

    private static void ApplyVersioning(string projectDir, ImportItem item)
    {
        var cadDir = Path.Combine(projectDir, CadFolder);
        if (!Directory.Exists(cadDir)) return;
        var ext = Path.GetExtension(item.OriginalName);
        var stem = Path.GetFileNameWithoutExtension(item.OriginalName);
        var existing = new List<(string Path, string Stem, string Base, int Ver, int Width)>();
        foreach (var f in Directory.GetFiles(cadDir))
        {
            if (!string.Equals(Path.GetExtension(f), ext, StringComparison.OrdinalIgnoreCase)) continue;
            var fs = Path.GetFileNameWithoutExtension(f);
            var m = VersionRx().Match(fs);
            if (m.Success) existing.Add((f, fs, m.Groups["base"].Value, int.Parse(m.Groups["ver"].Value), m.Groups["ver"].Value.Length));
        }

        var mine = VersionRx().Match(stem);
        if (mine.Success)
        {
            var key = FamilyKey(mine.Groups["base"].Value);
            var newVer = int.Parse(mine.Groups["ver"].Value);
            var older = existing.Where(e => FamilyKey(e.Base) == key && e.Ver < newVer).ToList();
            if (older.Count > 0)
            {
                item.ArchiveOlder = older.Select(o => o.Path).ToList();
                item.VersionNote = "نسخه جدید نقشه؛ نسخه‌های قبلی به آرشیو می‌روند";
            }
            return;
        }

        var family = existing.Where(e => FamilyKey(e.Base) == FamilyKey(stem)).ToList();
        if (family.Count == 0) return;
        var latest = family.OrderBy(e => e.Ver).Last();
        var next = latest.Ver + 1;
        item.TargetName = $"{latest.Base}_v{next.ToString().PadLeft(latest.Width, '0')}{ext}";
        item.ArchiveOlder = family.Select(o => o.Path).ToList();
        item.VersionNote = "نسخه جدید نقشه (شماره نسخه خودکار)؛ نسخه‌های قبلی به آرشیو می‌روند";
    }

    public static ImportResult Execute(string projectDir, IEnumerable<ImportItem> items)
    {
        var copied = new List<string>();
        var skipped = new List<string>();
        var created = new List<string>();
        var archived = new List<string>();
        foreach (var item in items.Where(i => i.Selected))
        {
            if (item.Folder == null || item.Conflict) { skipped.Add(item.OriginalName); continue; }
            try
            {
                var folderPath = Path.Combine(projectDir, item.Folder);
                if (!Directory.Exists(folderPath)) { ProjectFolders.EnsureSubfolder(projectDir, item.Folder); created.Add(item.Folder); }
                var dest = Path.Combine(folderPath, item.TargetName);
                if (File.Exists(dest) || Directory.Exists(dest)) { skipped.Add(item.OriginalName); continue; }
                if (item.IsDirectory) FileOps.CopyDirectory(item.Source, dest); else File.Copy(item.Source, dest);
                if (item.Folder == ProjectFolders.RawFolder) FileOps.MakeReadOnly(dest);
                copied.Add(item.TargetName);
                foreach (var old in item.ArchiveOlder.Where(File.Exists))
                {
                    var archiveDir = Path.Combine(projectDir, ArchiveFolder);
                    if (!Directory.Exists(archiveDir)) { ProjectFolders.EnsureSubfolder(projectDir, ArchiveFolder); created.Add(ArchiveFolder); }
                    File.Move(old, FileOps.UniquePath(Path.Combine(archiveDir, Path.GetFileName(old))));
                    archived.Add(Path.GetFileName(old));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                skipped.Add($"{item.OriginalName} ({e.Message})");
            }
        }
        return new ImportResult(copied, skipped, created.Distinct().ToList(), archived);
    }
}
