using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SurveyProjectManager.App.Controls;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App;

public sealed class ProjectRow
{
    public ProjectRow(Project p, Workspace w, DateTime today)
    {
        Project = p;
        var client = w.ClientOf(p);
        ClientId = client?.Id ?? "";
        ClientName = client == null || TextNorm.Normalize(client.Name) == TextNorm.Normalize(p.Owner) ? "" : client.Name;
        Search = string.Join(" ", p.Number, p.Owner, p.Location, p.Type, p.StatusLabel, p.Due, p.Note, client?.Name ?? "");
        var days = p.Status == "done" ? null : Jalali.DaysUntil(p.Due, today);
        (BadgeText, BadgeTone) = days switch
        {
            null => ("", ""),
            < 0 => ($"{Digits.Fa(-days.Value)} روز عقب‌افتاده", "reg"),
            <= 3 => ($"{Digits.Fa(days.Value)} روز مانده", "field"),
            _ => ($"{Digits.Fa(days.Value)} روز مانده", "collect"),
        };
    }

    public Project Project { get; }
    public string Number => Project.Number;
    public string Owner => Project.Owner;
    public string ClientId { get; }
    /// <summary>نام متقاضی فقط وقتی از مالک فرق دارد نمایش داده می‌شود.</summary>
    public string ClientName { get; }
    public string ClientText => ClientName.Length > 0 ? ClientName : "همان مالک";
    public bool HasOtherClient => ClientName.Length > 0;
    public string Location => Project.Location;
    public string Type => Project.Type;
    public string Status => Project.Status;
    public string StatusLabel => Project.StatusLabel;
    public string Due => string.IsNullOrEmpty(Project.Due) ? "—" : Digits.Fa(Project.Due);
    public string BadgeText { get; }
    public string BadgeTone { get; }
    public bool HasBadge => BadgeText.Length > 0;
    public string Year => Project.Year;
    public string YearLabel => "سال " + Digits.Fa(Project.Year);
    public string Search { get; }
}

public sealed class ClientRow
{
    public ClientRow(Client c, Workspace w)
    {
        Client = c;
        var ps = w.ProjectsOf(c.Id).ToList();
        var years = ps.Select(p => p.Year).Distinct().ToList();
        ProjectsText = $"{Digits.Fa(ps.Count)} پروژه ({string.Join("، ", years.Select(Digits.Fa))})";
        var last = ps.FirstOrDefault();
        LastNumber = last?.Number ?? "";
        LastType = last?.Type ?? "";
        LastStatus = last?.Status ?? "collect";
        LastStatusLabel = last?.StatusLabel ?? "";
        HasLast = last != null;
        Search = c.Name + " " + c.Phone;
    }

    public Client Client { get; }
    public string Id => Client.Id;
    public string Name => Client.Name;
    public string Kind => Client.Kind;
    public string Phone => Client.Phone;
    public string ProjectsText { get; }
    public string LastNumber { get; }
    public string LastType { get; }
    public string LastStatus { get; }
    public string LastStatusLabel { get; }
    public bool HasLast { get; }
    public string Search { get; }
}

public sealed class FileRow
{
    public FileRow(string fullPath, bool isDirectory, int? fileCount = null)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Name = Path.GetFileName(fullPath);
        DateText = Digits.Fa(Jalali.Format(isDirectory ? Directory.GetLastWriteTime(fullPath) : File.GetLastWriteTime(fullPath)));
        if (isDirectory)
        {
            Kind = "پوشه";
            Note = ProjectFolders.Describe(Name);
            IconKind = Name == ProjectFolders.RawFolder ? "lock" : "folder";
            SizeText = fileCount is { } n ? $"{Digits.Fa(n)} فایل" : "";
        }
        else
        {
            IconKind = KindIcon.KindOf(fullPath);
            Kind = IconKind switch
            {
                "cad" => "اتوکد", "pdf" => "PDF", "img" => "عکس", "csv" => "جدول / متن", "raw" => "فایل خام", _ => "فایل",
            } + $" ({Path.GetExtension(fullPath).TrimStart('.').ToUpperInvariant()})";
            Note = "";
            SizeText = Digits.Fa(FormatSize(new FileInfo(fullPath).Length));
        }
    }

    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string Name { get; }
    public string Kind { get; }
    public string SizeText { get; }
    public string DateText { get; }
    public string Note { get; }
    public string IconKind { get; }

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:0.#} MB";
        return $"{bytes / 1024.0 / 1024 / 1024:0.##} GB";
    }
}

/// <summary>گره درخت پوشه‌ها. دسته‌های استانداردی که هنوز ساخته نشده‌اند به‌صورت کم‌رنگ (Ghost) دیده می‌شوند.</summary>
public sealed class TreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _loaded;

    public TreeNode(string fullPath, bool isDirectory, string? displayName = null, bool ghost = false, bool isProjectRoot = false)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        IsGhost = ghost;
        IsProjectRoot = isProjectRoot;
        Name = displayName ?? Path.GetFileName(fullPath);
        IconKind = ghost ? "folder" : !isDirectory ? KindIcon.KindOf(fullPath) : Name == ProjectFolders.RawFolder ? "lock" : "folder";
        if (isDirectory && !ghost && HasChildren()) Children.Add(Placeholder);
    }

    private static readonly TreeNode Placeholder = new("", false, "…");

    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsGhost { get; }
    public bool IsProjectRoot { get; }
    public string Name { get; }
    public string IconKind { get; }
    public double NodeOpacity => IsGhost ? 0.55 : 1;
    public ObservableCollection<TreeNode> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            if (value) Load();
            OnPropertyChanged();
        }
    }

    private bool HasChildren()
    {
        try { return Directory.EnumerateFileSystemEntries(FullPath).Any(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    public void Load()
    {
        if (_loaded || !IsDirectory || IsGhost) return;
        _loaded = true;
        Children.Clear();
        try
        {
            foreach (var dir in Directory.GetDirectories(FullPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                Children.Add(new TreeNode(dir, true));
            if (IsProjectRoot)
                foreach (var missing in ProjectFolders.MissingSubfolders(FullPath))
                    Children.Add(new TreeNode(Path.Combine(FullPath, missing), true, missing, ghost: true));
            foreach (var file in Directory.GetFiles(FullPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                Children.Add(new TreeNode(file, false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public void Reload()
    {
        _loaded = false;
        Children.Clear();
        if (_isExpanded) Load();
        else if (IsDirectory && !IsGhost && HasChildren()) Children.Add(Placeholder);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>یک ردیف پنجره «افزودن فایل»: انتخاب پوشه مقصد، توضیح و وضعیت.</summary>
public sealed class ImportRow : INotifyPropertyChanged
{
    private readonly string _projectDir;
    private bool _selected;

    public ImportRow(string projectDir, ImportItem item)
    {
        _projectDir = projectDir;
        Item = item;
        _selected = item.Selected;
        Choices = ProjectFolders.Subfolders.Select(s => new FolderChoice(s.Name,
            Directory.Exists(Path.Combine(projectDir, s.Name)) ? s.Name : s.Name + " (پوشه جدید)")).ToList();
        SelectedChoice = Choices.FirstOrDefault(c => c.Value == item.Folder);
    }

    public ImportItem Item { get; }
    public string Name => Item.TargetName;
    public string Original => Item.OriginalName;
    public List<FolderChoice> Choices { get; }
    private FolderChoice? _choice;

    public FolderChoice? SelectedChoice
    {
        get => _choice;
        set
        {
            if (_choice == value) return;
            _choice = value;
            Item.Folder = value?.Value;
            ImportPlanner.Refresh(_projectDir, Item);
            if (Item.Folder != null && !Item.Conflict) { Item.Selected = true; _selected = true; }
            OnPropertyChanged(nameof(SelectedChoice)); OnPropertyChanged(nameof(Note)); OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsSelected)); OnPropertyChanged(nameof(NoteTone));
        }
    }

    public bool IsSelected
    {
        get => _selected;
        set { _selected = value; Item.Selected = value; OnPropertyChanged(); }
    }

    public string Note
    {
        get
        {
            var parts = new List<string>();
            if (Item.Reason.Length > 0 && Item.Folder != null) parts.Add(Item.Reason);
            if (Item.Folder == null) parts.Add(Item.Reason);
            if (Item.VersionNote.Length > 0) parts.Add(Item.VersionNote);
            if (Item.NewFolder && Item.Folder != null) parts.Add($"پوشه «{ProjectFolders.Describe(Item.Folder)}» ساخته می‌شود");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>ok | ask | conflict</summary>
    public string NoteTone => Item.Conflict ? "reg" : Item.Folder == null ? "field" : "done";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record FolderChoice(string Value, string Label)
{
    public override string ToString() => Label;
}
