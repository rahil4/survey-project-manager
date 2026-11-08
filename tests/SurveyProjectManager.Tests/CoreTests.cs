using SurveyProjectManager.Core;

namespace SurveyProjectManager.Tests;

public class CoreTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "spm_" + Guid.NewGuid().ToString("N"));

    public CoreTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        // ویندوز حذف فایل فقط‌خواندنی (مثل فایل‌های خام کپی‌شده) را رد می‌کند
        foreach (var f in Directory.EnumerateFiles(_tmp, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_tmp, recursive: true);
    }

    [Theory]
    [InlineData(2026, 10, 28, 1405, 8, 6)]
    [InlineData(2025, 3, 21, 1404, 1, 1)]
    [InlineData(2024, 3, 20, 1403, 1, 1)]
    [InlineData(2026, 3, 21, 1405, 1, 1)]
    public void Jalali_known_dates(int gy, int gm, int gd, int jy, int jm, int jd) =>
        Assert.Equal((jy, jm, jd), Jalali.FromGregorian(gy, gm, gd));

    [Fact]
    public void Translit_and_slug()
    {
        Assert.Equal("Ahmdy", Translit.ToLatin("احمدی"));
        Assert.Equal("Rzayy_Krj", Translit.FolderSlug("آقای رضایی", "کرج، پلاک ۱۲"));
        Assert.Equal("Project", Translit.FolderSlug("", ""));
        Assert.Equal("Custom_Name", Translit.SanitizeSlug("Custom Name!"));
    }

    [Fact]
    public void NextNumber_counts_per_year()
    {
        var ps = new[] { new Project { Number = "1405-001" }, new Project { Number = "1405-009" }, new Project { Number = "1404-120" } };
        Assert.Equal("1405-010", ProjectStore.NextNumber(ps, 1405));
        Assert.Equal("1406-001", ProjectStore.NextNumber(ps, 1406));
        Assert.Equal("1405-001", ProjectStore.NextNumber(Array.Empty<Project>(), 1405));
    }

    [Fact]
    public void Csv_roundtrip_keeps_persian_commas_and_quotes()
    {
        var p = new Project
        {
            Number = "1405-001", Owner = "آقای احمدی", Location = "کرج، پلاک ۱", Type = "تفکیک", Status = "draw",
            Due = "1405/08/15", Folder = "1405/1405-001_Ahmdy_Krj", Note = "یادداشت, با \"ویرگول\"\nو خط جدید",
        };
        ProjectStore.Save(_tmp, new[] { p });
        var bytes = File.ReadAllBytes(ProjectStore.CsvPath(_tmp));
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]); // BOM برای اکسل
        var loaded = Assert.Single(ProjectStore.Load(_tmp));
        Assert.Equal(p.Owner, loaded.Owner);
        Assert.Equal(p.Location, loaded.Location);
        Assert.Equal(p.Note, loaded.Note);
        Assert.Equal(p.Folder, loaded.Folder);
        Assert.Empty(ProjectStore.Load(Path.Combine(_tmp, "missing")));
    }

    [Fact]
    public void Create_folders_readme_and_raw_guard()
    {
        var p = new Project { Number = "1405-002", Owner = "رضایی", Folder = ProjectFolders.RelativePath("1405-002", "Rzayy") };
        var dir = ProjectFolders.Create(_tmp, p, "UTM39");
        foreach (var (name, _) in ProjectFolders.Subfolders) Assert.True(Directory.Exists(Path.Combine(dir, name)));
        Assert.Contains("UTM39", File.ReadAllText(Path.Combine(dir, "README.txt")));
        Assert.True(ProjectFolders.IsInRawFolder(dir, Path.Combine(dir, ProjectFolders.RawFolder, "x.job")));
        Assert.False(ProjectFolders.IsInRawFolder(dir, Path.Combine(dir, "04_CAD", "x.dwg")));
        Assert.False(ProjectFolders.IsInRawFolder(dir, Path.Combine(_tmp, "other")));
        Assert.Throws<IOException>(() => ProjectFolders.Create(_tmp, p, "UTM39"));
    }

    [Fact]
    public void CopyInto_never_overwrites_and_marks_raw_read_only()
    {
        var p = new Project { Number = "1405-003", Folder = ProjectFolders.RelativePath("1405-003", "X") };
        var dir = ProjectFolders.Create(_tmp, p, "UTM39");
        var src = Path.Combine(_tmp, "plan.dwg");
        File.WriteAllText(src, "x");

        var cad = Path.Combine(dir, "04_CAD");
        Assert.Equal(new[] { "plan.dwg" }, FileOps.CopyInto(dir, cad, new[] { src }).Copied);
        File.WriteAllText(Path.Combine(cad, "plan.dwg"), "changed");
        var again = FileOps.CopyInto(dir, cad, new[] { src });
        Assert.Equal(new[] { "plan.dwg" }, again.Skipped);
        Assert.Equal("changed", File.ReadAllText(Path.Combine(cad, "plan.dwg")));

        var raw = Path.Combine(dir, ProjectFolders.RawFolder);
        FileOps.CopyInto(dir, raw, new[] { src });
        Assert.True(File.GetAttributes(Path.Combine(raw, "plan.dwg")).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void Config_roundtrip()
    {
        Assert.Equal("", AppConfig.Load(_tmp).Root);
        new AppConfig { Root = @"D:\Projects", Crs = "X" }.Save(_tmp);
        var cfg = AppConfig.Load(_tmp);
        Assert.Equal(@"D:\Projects", cfg.Root);
        Assert.Equal("X", cfg.Crs);
    }

    [Fact]
    public void Archive_creates_zip()
    {
        var p = new Project { Number = "1405-004", Folder = ProjectFolders.RelativePath("1405-004", "Z") };
        var dir = ProjectFolders.Create(_tmp, p, "UTM39");
        var zip = FileOps.Archive(_tmp, dir);
        Assert.True(File.Exists(zip));
        Assert.True(new FileInfo(zip).Length > 0);
    }
}
