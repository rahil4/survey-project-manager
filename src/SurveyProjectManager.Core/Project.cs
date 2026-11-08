namespace SurveyProjectManager.Core;

public sealed class Project
{
    public string Number { get; set; } = "";   // مثل 1405-014
    public string Owner { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "collect";
    public string Due { get; set; } = "";
    public string Folder { get; set; } = "";   // مسیر نسبی با / (مثل 1405/1405-014_Ahmadi_Karaj)
    public string Note { get; set; } = "";

    public string StatusLabel => Statuses.Label(Status);

    public string Year => Number.Split('-')[0];

    public string FullPath(string root) =>
        Path.Combine(new[] { root }.Concat(Folder.Split('/', StringSplitOptions.RemoveEmptyEntries)).ToArray());
}

public static class Statuses
{
    public static readonly IReadOnlyList<(string Key, string Label)> All = new[]
    {
        ("collect", "دریافت مدارک"),
        ("field", "برداشت"),
        ("draw", "ترسیم"),
        ("check", "کنترل"),
        ("done", "تحویل‌شده"),
        ("reg", "در ثبت"),
    };

    public static string Label(string key) => All.FirstOrDefault(s => s.Key == key).Label ?? key;

    public static readonly IReadOnlyList<string> ProjectTypes = new[]
    {
        "تفکیک", "افراز", "ثبتی / تحدید حدود", "توپوگرافی", "پیاده‌سازی", "پایان‌کار", "سایر",
    };
}
