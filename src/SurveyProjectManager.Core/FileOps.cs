namespace SurveyProjectManager.Core;

public sealed record CopyResult(List<string> Copied, List<string> Skipped);

/// <summary>کپی فایل‌ها به پوشه پروژه: هرگز رونویسی نمی‌کند و فایل‌های خام را فقط‌خواندنی می‌کند.</summary>
public static class FileOps
{
    public static CopyResult CopyInto(string projectDir, string targetDir, IEnumerable<string> sources)
    {
        var inRaw = ProjectFolders.IsInRawFolder(projectDir, targetDir);
        var copied = new List<string>();
        var skipped = new List<string>();
        foreach (var src in sources)
        {
            var name = Path.GetFileName(src.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var dest = Path.Combine(targetDir, name);
            try
            {
                if (File.Exists(dest) || Directory.Exists(dest)) { skipped.Add(name); continue; }
                if (Directory.Exists(src)) CopyDirectory(src, dest);
                else if (File.Exists(src)) File.Copy(src, dest);
                else { skipped.Add(name); continue; }
                if (inRaw) MakeReadOnly(dest);
                copied.Add(name);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                skipped.Add($"{name} ({e.Message})");
            }
        }
        return new CopyResult(copied, skipped);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    public static void MakeReadOnly(string path)
    {
        if (File.Exists(path)) { File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly); return; }
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
    }

    public static string Archive(string root, string projectDir)
    {
        var outDir = Path.Combine(root, "_Archive");
        Directory.CreateDirectory(outDir);
        var zip = Path.Combine(outDir, Path.GetFileName(projectDir) + ".zip");
        if (File.Exists(zip)) zip = Path.Combine(outDir, $"{Path.GetFileName(projectDir)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(projectDir, zip, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
        return zip;
    }
}
