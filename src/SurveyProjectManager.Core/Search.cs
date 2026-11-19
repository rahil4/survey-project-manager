namespace SurveyProjectManager.Core;

public sealed record FileEntry(string ProjectNumber, string Folder, string Name, string FullPath);

public sealed class SearchResults
{
    public List<Client> Clients { get; init; } = new();
    public List<Project> Projects { get; init; } = new();
    public List<FileEntry> Files { get; init; } = new();
}

/// <summary>فهرست نام فایل‌های همه پروژه‌ها و جستجوی سراسری.</summary>
public static class FileIndex
{
    public static List<FileEntry> Build(Workspace w, int cap = 200_000)
    {
        var list = new List<FileEntry>();
        foreach (var p in w.Projects)
        {
            var dir = p.FullPath(w.Root);
            if (!Directory.Exists(dir)) continue;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(dir, f);
                    var parts = rel.Split(Path.DirectorySeparatorChar);
                    list.Add(new FileEntry(p.Number, parts.Length > 1 ? parts[0] : "", Path.GetFileName(f), f));
                    if (list.Count >= cap) return list;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return list;
    }

    public static SearchResults Search(Workspace w, IReadOnlyList<FileEntry> index, string query, string? currentProject = null, int maxFiles = 7, int maxOthers = 6)
    {
        var q = TextNorm.Normalize(query);
        if (q.Length == 0)
            return new SearchResults { Projects = w.Projects.Take(3).ToList() };
        bool Match(string s) => TextNorm.Normalize(s).Contains(q, StringComparison.Ordinal);
        var clients = w.Clients.Where(c => Match(c.Name) || Match(c.Phone)).Take(maxOthers).ToList();
        var projects = w.Projects.Where(p => Match(p.Number) || Match(p.Owner) || Match(p.Location) || Match(p.Type) ||
                                             (w.ClientOf(p) is { } c && Match(c.Name))).Take(maxOthers).ToList();
        var files = index.Where(f => Match(f.Name))
            .OrderBy(f => f.ProjectNumber == currentProject ? 0 : 1)
            .ThenBy(f => TextNorm.Normalize(f.Name).StartsWith(q, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxFiles).ToList();
        return new SearchResults { Clients = clients, Projects = projects, Files = files };
    }
}
