namespace SurveyProjectManager.Core;

/// <summary>فهرست پروژه‌ها در فایل projects.csv (قابل‌باز شدن در اکسل).</summary>
public static class ProjectStore
{
    public const string CsvName = "projects.csv";
    private static readonly string[] Header = { "number", "owner", "location", "type", "status", "due", "folder", "note", "client", "related" };

    public static string CsvPath(string root) => Path.Combine(root, CsvName);

    public static List<Project> Load(string root) =>
        Csv.ReadTable(CsvPath(root)).Select(r =>
        {
            string C(string k) => r.GetValueOrDefault(k, "");
            return new Project
            {
                Number = C("number"), Owner = C("owner"), Location = C("location"), Type = C("type"),
                Status = C("status") is { Length: > 0 } st ? st : "collect", Due = C("due"), Folder = C("folder"),
                Note = C("note"), ClientId = C("client"), Related = C("related"),
            };
        }).ToList();

    public static void Save(string root, IEnumerable<Project> projects) =>
        Csv.WriteTable(CsvPath(root), Header, projects.Select(p => new[]
        {
            p.Number, p.Owner, p.Location, p.Type, p.Status, p.Due, p.Folder, p.Note, p.ClientId, p.Related,
        }));

    public static string NextNumber(IEnumerable<Project> projects, int year)
    {
        var prefix = $"{year}-";
        var max = 0;
        foreach (var p in projects)
            if (p.Number.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(p.Number[prefix.Length..], out var n))
                max = Math.Max(max, n);
        return $"{year}-{max + 1:000}";
    }
}
