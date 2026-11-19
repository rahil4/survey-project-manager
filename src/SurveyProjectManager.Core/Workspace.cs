namespace SurveyProjectManager.Core;

public sealed class NewProjectRequest
{
    public string? ClientId { get; set; }
    public string ClientName { get; set; } = "";
    public string ClientKind { get; set; } = "حقیقی";
    public string ClientPhone { get; set; } = "";
    public bool OwnerIsClient { get; set; } = true;
    public string Owner { get; set; } = "";
    public string Location { get; set; } = "";
    public string Type { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? FromProject { get; set; }
    /// <summary>مسیر کامل مدارکی از پروژه قبلی که باید به 01_Documents پروژه جدید کپی شود.</summary>
    public List<string> DocsToCopy { get; set; } = new();
}

/// <summary>پروژه‌ها و متقاضیان یک پوشه اصلی، همراه با قواعد ارتباط بین آن‌ها.</summary>
public sealed class Workspace
{
    public Workspace(string root) => Root = root;

    public string Root { get; }
    public List<Project> Projects { get; private set; } = new();
    public List<Client> Clients { get; private set; } = new();

    public static Workspace Load(string root)
    {
        var w = new Workspace(root)
        {
            Projects = ProjectStore.Load(root),
            Clients = ClientStore.Load(root),
        };
        w.Projects.Sort((a, b) => string.CompareOrdinal(b.Number, a.Number));
        if (w.MigrateClients()) w.TrySave();
        return w;
    }

    /// <summary>پروژه‌هایی که متقاضی ندارند (فایل نسخه ۱) به متقاضی هم‌نام مالک وصل می‌شوند.</summary>
    public bool MigrateClients()
    {
        var changed = false;
        foreach (var p in Projects.Where(p => string.IsNullOrEmpty(p.ClientId) || Client(p.ClientId) == null))
        {
            var name = p.Owner.Trim();
            if (name.Length == 0) name = "نامشخص";
            var client = ClientMatcher.FindExact(Clients, name);
            if (client == null)
            {
                client = new Client { Id = ClientStore.NextId(Clients), Name = name };
                Clients.Add(client);
            }
            p.ClientId = client.Id;
            changed = true;
        }
        return changed;
    }

    private void TrySave()
    {
        try { Save(); } catch (IOException) { /* فایل در اکسل باز است؛ دفعه بعد ذخیره می‌شود */ }
    }

    public void Save()
    {
        ProjectStore.Save(Root, Projects);
        ClientStore.Save(Root, Clients);
    }

    public Client? Client(string? id) => Clients.FirstOrDefault(c => c.Id == id);

    public Client? ClientOf(Project p) => Client(p.ClientId);

    public Project? Find(string number) => Projects.FirstOrDefault(p => p.Number == number);

    public IEnumerable<Project> ProjectsOf(string clientId) =>
        Projects.Where(p => p.ClientId == clientId).OrderByDescending(p => p.Number, StringComparer.Ordinal);

    /// <summary>پروژه‌های مرتبط: لینک صریح یا همان موقعیت/پلاک.</summary>
    public List<Project> RelatedTo(Project p)
    {
        var loc = TextNorm.Normalize(p.Location);
        var explicitSet = new HashSet<string>(p.RelatedNumbers);
        return Projects.Where(q => q.Number != p.Number &&
                (explicitSet.Contains(q.Number) || q.RelatedNumbers.Contains(p.Number) ||
                 (loc.Length > 0 && TextNorm.Normalize(q.Location) == loc)))
            .OrderBy(q => q.Number, StringComparer.Ordinal).ToList();
    }

    /// <summary>ملک‌هایی که برای این متقاضی بیش از یک بار سفارش داده شده (بر اساس متن موقعیت).</summary>
    public List<(string Location, List<Project> Projects)> RepeatedProperties(string clientId) =>
        ProjectsOf(clientId).Where(p => p.Location.Trim().Length > 0)
            .GroupBy(p => TextNorm.Normalize(p.Location))
            .Where(g => g.Count() > 1)
            .Select(g => (g.First().Location, g.OrderBy(p => p.Number, StringComparer.Ordinal).ToList()))
            .ToList();

    public string NextNumber(int year) => ProjectStore.NextNumber(Projects, year);

    public static void Link(Project a, Project b)
    {
        if (!a.RelatedNumbers.Contains(b.Number)) a.Related = string.Join(';', a.RelatedNumbers.Append(b.Number));
        if (!b.RelatedNumbers.Contains(a.Number)) b.Related = string.Join(';', b.RelatedNumbers.Append(a.Number));
    }

    /// <summary>مدارکی (سند، استعلام، کروکی …) که می‌شود از پروژه قبلی کپی کرد.</summary>
    public static List<string> CopyableDocuments(string root, Project from)
    {
        var dir = from.FullPath(root);
        var result = new List<string>();
        foreach (var sub in new[] { "01_Documents", "06_Registration" })
        {
            var d = Path.Combine(dir, sub);
            if (Directory.Exists(d)) result.AddRange(Directory.GetFiles(d).OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
        }
        return result;
    }

    public Project CreateProject(NewProjectRequest r, int year, string crs)
    {
        var client = r.ClientId != null ? Client(r.ClientId) : ClientMatcher.FindExact(Clients, r.ClientName);
        if (client == null)
        {
            if (string.IsNullOrWhiteSpace(r.ClientName)) throw new ArgumentException("نام متقاضی خالی است.");
            client = new Client
            {
                Id = ClientStore.NextId(Clients), Name = r.ClientName.Trim(), Kind = r.ClientKind, Phone = r.ClientPhone.Trim(),
            };
            Clients.Add(client);
        }
        var number = NextNumber(year);
        var project = new Project
        {
            Number = number,
            ClientId = client.Id,
            Owner = r.OwnerIsClient || string.IsNullOrWhiteSpace(r.Owner) ? client.Name : r.Owner.Trim(),
            Location = r.Location.Trim(),
            Type = r.Type,
            Folder = ProjectFolders.RelativePath(number, Translit.SanitizeSlug(r.Slug)),
        };
        var dir = ProjectFolders.Create(Root, project, crs);
        if (r.DocsToCopy.Count > 0)
            FileOps.CopyInto(dir, Path.Combine(dir, "01_Documents"), r.DocsToCopy);
        Projects.Insert(0, project);
        if (r.FromProject != null && Find(r.FromProject) is { } from) Link(project, from);
        Projects.Sort((a, b) => string.CompareOrdinal(b.Number, a.Number));
        Save();
        return project;
    }

    /// <summary>پروژه‌های قدیمی را (بدون جابه‌جا کردن، یا با کپی در ساختار استاندارد) در فهرست ثبت می‌کند.</summary>
    public List<Project> ImportLegacy(IEnumerable<LegacyEntry> entries, LegacyMode mode, int year, string crs)
    {
        var created = new List<Project>();
        foreach (var e in entries.Where(e => e.Selected))
        {
            var ownerName = e.Owner.Trim().Length > 0 ? e.Owner.Trim() : e.FolderName;
            var client = ClientMatcher.FindExact(Clients, ownerName);
            if (client == null)
            {
                client = new Client { Id = ClientStore.NextId(Clients), Name = ownerName };
                Clients.Add(client);
            }
            var number = NextNumber(year);
            var project = new Project
            {
                Number = number, ClientId = client.Id, Owner = client.Name, Location = e.Location.Trim(),
                Type = e.Type, Status = "done",
            };
            if (mode == LegacyMode.RegisterInPlace) project.Folder = e.Path;
            else
            {
                project.Folder = ProjectFolders.RelativePath(number, Translit.SanitizeSlug(e.FolderName));
                var dir = ProjectFolders.Create(Root, project, crs);
                LegacyScanner.CopyIntoStandard(e.Path, dir);
            }
            Projects.Add(project);
            created.Add(project);
        }
        Projects.Sort((a, b) => string.CompareOrdinal(b.Number, a.Number));
        Save();
        return created;
    }

    public Client AddClient(Client c)
    {
        c.Id = ClientStore.NextId(Clients);
        Clients.Add(c);
        Save();
        return c;
    }
}
