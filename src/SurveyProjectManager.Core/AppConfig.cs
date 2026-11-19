using System.Text.Json;

namespace SurveyProjectManager.Core;

/// <summary>تنظیمات برنامه: پوشه اصلی پروژه‌ها و سیستم مختصات پیش‌فرض.</summary>
public sealed class AppConfig
{
    public const string DefaultCrs = "WGS84 / UTM zone 39N (EPSG:32639)";

    public string Root { get; set; } = "";
    public string Crs { get; set; } = DefaultCrs;
    public string BackupDest { get; set; } = "";
    public int BackupRemindDays { get; set; } = 7;
    public string LastBackup { get; set; } = "";          // yyyy-MM-dd HH:mm
    public List<string> Recents { get; set; } = new();    // شماره پروژه‌های اخیراً باز شده، تازه‌ترین اول
    public string Theme { get; set; } = "system";         // system | light | dark
    public bool PersianDigits { get; set; } = true;

    public void AddRecent(string number)
    {
        Recents.Remove(number);
        Recents.Insert(0, number);
        if (Recents.Count > 10) Recents.RemoveRange(10, Recents.Count - 10);
    }

    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SurveyProjectManager");

    public static AppConfig Load(string? directory = null)
    {
        var path = Path.Combine(directory ?? DefaultDirectory(), "config.json");
        try
        {
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path)) ?? new AppConfig();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppConfig();
        }
    }

    public void Save(string? directory = null)
    {
        var dir = directory ?? DefaultDirectory();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"),
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }
}
