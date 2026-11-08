namespace SurveyProjectManager.Core;

/// <summary>ساخت ساختار پوشه پروژه روی دیسک و قواعد پوشه فایل‌های خام.</summary>
public static class ProjectFolders
{
    public const string RawFolder = "02_Field_Raw";

    public static readonly IReadOnlyList<(string Name, string Description)> Subfolders = new[]
    {
        ("01_Documents", "اسناد دریافتی"),
        ("02_Field_Raw", "خام توتال / GPS / دوربین"),
        ("03_Processing", "پردازش"),
        ("04_CAD", "فایل‌های اتوکد"),
        ("05_Final_PDF", "خروجی نهایی"),
        ("06_Registration", "مدارک ثبتی"),
        ("07_Photos", "عکس‌ها"),
        ("99_Archive", "نسخه‌های قدیمی"),
    };

    public static string Describe(string folderName) =>
        Subfolders.FirstOrDefault(s => s.Name == folderName).Description ?? "";

    public static string RelativePath(string number, string slug) => $"{number.Split('-')[0]}/{number}_{slug}";

    /// <summary>پوشه پروژه و زیرپوشه‌ها را می‌سازد. اگر پوشه از قبل هست خطا می‌دهد.</summary>
    public static string Create(string root, Project project, string crs)
    {
        var baseDir = project.FullPath(root);
        if (Directory.Exists(baseDir)) throw new IOException($"پوشه از قبل وجود دارد: {baseDir}");
        foreach (var (name, _) in Subfolders)
            Directory.CreateDirectory(Path.Combine(baseDir, name));
        var readme = string.Join("\n",
            $"Project : {project.Number}",
            $"Owner   : {project.Owner}",
            $"Location: {project.Location}",
            $"Type    : {project.Type}",
            $"CRS     : {crs}",
            "Control : (write base points / benchmarks here)",
            "",
            "02_Field_Raw holds raw total-station / GPS / camera files.",
            "Do not edit them: they are the reference for the whole job.",
            "");
        File.WriteAllText(Path.Combine(baseDir, "README.txt"), readme, new System.Text.UTF8Encoding(false));
        return baseDir;
    }

    public static bool IsInRawFolder(string projectDir, string path)
    {
        var rel = Path.GetRelativePath(Path.GetFullPath(projectDir), Path.GetFullPath(path));
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return false;
        var first = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return first == RawFolder;
    }
}
