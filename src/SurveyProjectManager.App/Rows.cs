using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App;

public sealed class ProjectRow
{
    private static readonly Dictionary<string, (string Fg, string Bg)> Colors = new()
    {
        ["collect"] = ("#52606D", "#E9EDF1"),
        ["field"] = ("#8A5A00", "#FBEFD2"),
        ["draw"] = ("#0B5C9E", "#DCECF8"),
        ["check"] = ("#6B3FA0", "#EEE6F8"),
        ["done"] = ("#1D7A46", "#DFF3E7"),
        ["reg"] = ("#A3361D", "#FBE5DF"),
    };

    public ProjectRow(Project p)
    {
        Project = p;
        var (fg, bg) = Colors.GetValueOrDefault(p.Status, ("#000000", "#FFFFFF"));
        StatusForeground = new SolidColorBrush(Color.Parse(fg));
        StatusBackground = new SolidColorBrush(Color.Parse(bg));
    }

    public Project Project { get; }
    public string Number => Project.Number;
    public string Owner => Project.Owner;
    public string Location => Project.Location;
    public string Type => Project.Type;
    public string StatusLabel => Project.StatusLabel;
    public string Due => string.IsNullOrEmpty(Project.Due) ? "—" : Project.Due;
    public IBrush StatusForeground { get; }
    public IBrush StatusBackground { get; }
}

public sealed class FileRow
{
    private static readonly IBrush FolderBrush = new SolidColorBrush(Color.Parse("#E0A525"));
    private static readonly IBrush LockedBrush = new SolidColorBrush(Color.Parse("#A3361D"));
    private static readonly IBrush CadBrush = new SolidColorBrush(Color.Parse("#0F6CBD"));
    private static readonly IBrush PdfBrush = new SolidColorBrush(Color.Parse("#A3361D"));
    private static readonly IBrush ImgBrush = new SolidColorBrush(Color.Parse("#6B3FA0"));
    private static readonly IBrush TabBrush = new SolidColorBrush(Color.Parse("#1D7A46"));
    private static readonly IBrush OtherBrush = new SolidColorBrush(Color.Parse("#7A8896"));

    public FileRow(string fullPath, bool isDirectory, bool locked = false)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Name = Path.GetFileName(fullPath);
        var ext = Path.GetExtension(fullPath).TrimStart('.').ToLowerInvariant();
        DateText = Jalali.Format(isDirectory ? Directory.GetLastWriteTime(fullPath) : File.GetLastWriteTime(fullPath));
        if (isDirectory)
        {
            Kind = "پوشه";
            SizeText = "";
            Note = ProjectFolders.Describe(Name);
            IconBrush = locked ? LockedBrush : FolderBrush;
        }
        else
        {
            (Kind, IconBrush) = ext switch
            {
                "dwg" or "dxf" or "bak" or "dwt" => ($"اتوکد ({ext.ToUpperInvariant()})", CadBrush),
                "pdf" => ("PDF", PdfBrush),
                "jpg" or "jpeg" or "png" or "tif" or "tiff" or "heic" => ($"عکس ({ext.ToUpperInvariant()})", ImgBrush),
                "csv" or "xlsx" or "xls" or "txt" => ($"جدول / متن ({ext.ToUpperInvariant()})", TabBrush),
                "" => ("فایل", OtherBrush),
                _ => ($"فایل {ext.ToUpperInvariant()}", OtherBrush),
            };
            SizeText = FormatSize(new FileInfo(fullPath).Length);
            Note = "";
        }
    }

    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string Name { get; }
    public string Kind { get; }
    public string SizeText { get; }
    public string DateText { get; }
    public string Note { get; }
    public IBrush IconBrush { get; }
    public bool IsLocked => false;

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024:0.#} MB";
        return $"{bytes / 1024.0 / 1024 / 1024:0.##} GB";
    }
}

/// <summary>گره درخت پوشه‌ها؛ فرزندان فقط هنگام باز شدن خوانده می‌شوند.</summary>
public sealed class TreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _loaded;

    public TreeNode(string fullPath, bool isDirectory, string? displayName = null)
    {
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Name = displayName ?? Path.GetFileName(fullPath);
        if (isDirectory && HasChildren()) Children.Add(Placeholder);
    }

    private static readonly TreeNode Placeholder = new("", false, "…");

    public string FullPath { get; }
    public bool IsDirectory { get; }
    public string Name { get; }
    public ObservableCollection<TreeNode> Children { get; } = new();
    public IBrush IconBrush => IsDirectory ? new SolidColorBrush(Color.Parse("#E0A525")) : new SolidColorBrush(Color.Parse("#9AA7B4"));

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
        if (_loaded || !IsDirectory) return;
        _loaded = true;
        Children.Clear();
        try
        {
            foreach (var dir in Directory.GetDirectories(FullPath).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                Children.Add(new TreeNode(dir, true));
            foreach (var file in Directory.GetFiles(FullPath).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                Children.Add(new TreeNode(file, false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
