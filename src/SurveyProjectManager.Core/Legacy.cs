using System.Text.RegularExpressions;

namespace SurveyProjectManager.Core;

public enum LegacyMode
{
    /// <summary>فقط در فهرست ثبت می‌شود؛ فایل‌ها همان‌جا می‌مانند.</summary>
    RegisterInPlace,
    /// <summary>فایل‌ها در ساختار استاندارد کپی می‌شوند؛ پوشه قدیمی دست‌نخورده می‌ماند.</summary>
    CopyIntoStandard,
}

public sealed class LegacyEntry
{
    public string Path { get; init; } = "";
    public string FolderName { get; init; } = "";
    public int FileCount { get; init; }
    public bool LooksLikeProject { get; init; }
    public string Note { get; init; } = "";
    public bool Selected { get; set; }
    public string Owner { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "سایر";
}

/// <summary>پیدا کردن پوشه‌های پروژه‌های قدیمی داخل یک پوشه.</summary>
public static partial class LegacyScanner
{
    [GeneratedRegex(@"backup|temp|tmp|recycle|system volume|^\$|^\.", RegexOptions.IgnoreCase)]
    private static partial Regex NotProject();

    public static List<LegacyEntry> Scan(string path)
    {
        var result = new List<LegacyEntry>();
        if (!Directory.Exists(path)) return result;
        foreach (var dir in Directory.GetDirectories(path).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var name = System.IO.Path.GetFileName(dir);
            int count;
            try { count = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { count = 0; }
            var skip = NotProject().IsMatch(name);
            var looks = !skip && count > 0;
            result.Add(new LegacyEntry
            {
                Path = dir, FolderName = name, FileCount = count, LooksLikeProject = looks, Selected = looks,
                Note = skip ? "پروژه به نظر نمی‌رسد" : count == 0 ? "پوشه خالی" : "",
                Owner = GuessOwner(name),
            });
        }
        return result;
    }

    public static string GuessOwner(string folderName)
    {
        var words = folderName.Replace('_', ' ').Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !w.All(char.IsDigit));
        return string.Join(' ', words);
    }

    /// <summary>فایل‌های پوشه قدیمی را بر اساس نوع در ساختار استاندارد پروژه کپی می‌کند؛ فقط پوشه‌های لازم ساخته می‌شوند.</summary>
    public static int CopyIntoStandard(string legacyPath, string projectDir)
    {
        int copied = 0;
        foreach (var file in Directory.EnumerateFiles(legacyPath, "*", SearchOption.AllDirectories))
        {
            var name = System.IO.Path.GetFileName(file);
            var folder = FileClassifier.Classify(name).Folder ?? "01_Documents";
            var dir = ProjectFolders.EnsureSubfolder(projectDir, folder);
            var dest = FileOps.UniquePath(System.IO.Path.Combine(dir, name));
            File.Copy(file, dest);
            if (folder == ProjectFolders.RawFolder) FileOps.MakeReadOnly(dest);
            copied++;
        }
        return copied;
    }
}
