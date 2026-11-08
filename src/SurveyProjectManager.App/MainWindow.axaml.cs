using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App;

public partial class MainWindow : Window
{
    private readonly AppConfig _cfg;
    private readonly bool _persistConfig;
    private string _root;
    private List<Project> _projects = new();
    private List<ProjectRow> _rows = new();

    private Project? _openProject;
    private string _projectDir = "";
    private string _current = "";
    private TreeNode? _treeRoot;
    private bool _syncingTree;

    public MainWindow() : this(new AppConfig(), persistConfig: false) { }

    public MainWindow(AppConfig cfg, bool persistConfig)
    {
        InitializeComponent();
        _cfg = cfg;
        _persistConfig = persistConfig;
        _root = cfg.Root;

        NewBtn.Click += async (_, _) => await NewProjectAsync();
        EditBtn.Click += async (_, _) => await EditProjectAsync();
        RevealBtn.Click += (_, _) => { if (NeedProject() is { } p) Shell.Reveal(p.FullPath(_root)); };
        CopyPathBtn.Click += async (_, _) => { if (NeedProject() is { } p) { await Shell.CopyTextAsync(this, p.FullPath(_root)); StatusText.Text = "مسیر کپی شد"; } };
        ZipBtn.Click += async (_, _) => await ArchiveAsync();
        CsvBtn.Click += (_, _) => OpenCsv();
        RootBtn.Click += async (_, _) => await ChangeRootAsync();

        StatusList.SelectionChanged += (_, _) => ApplyFilter();
        SearchBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        ProjectGrid.DoubleTapped += (_, _) => EnterProject();
        ProjectGrid.KeyDown += (_, e) => { if (e.Key == Key.Enter) { EnterProject(); e.Handled = true; } };
        ProjectGrid.ContextMenu = BuildRowMenu();

        BackBtn.Click += (_, _) => ShowList();
        UpBtn.Click += (_, _) => GoUp();
        CopyDirBtn.Click += async (_, _) => await Shell.CopyTextAsync(this, _current);
        ExplorerBtn.Click += (_, _) => Shell.Reveal(_current);
        FolderTree.SelectionChanged += OnTreeSelectionChanged;
        FileGrid.DoubleTapped += (_, _) => ActivateSelectedFile();
        FileGrid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { ActivateSelectedFile(); e.Handled = true; }
            else if (e.Key == Key.Back) { GoUp(); e.Handled = true; }
        };
        DropZone.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        DropZone.AddHandler(DragDrop.DropEvent, async (_, e) => await OnDropAsync(e));

        Opened += async (_, _) => await EnsureRootAsync();
        if (!string.IsNullOrEmpty(_root)) Reload();
    }

    // ---- شروع کار ----
    private async Task EnsureRootAsync()
    {
        if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) return;
        if (!string.IsNullOrEmpty(_root))
        {
            try { Directory.CreateDirectory(_root); Reload(); return; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        await MessageDialog.ShowAsync(this, "پوشه‌یار نقشه‌برداری",
            "برای شروع، پوشه‌ای را که می‌خواهید پروژه‌ها در آن ساخته شوند انتخاب کنید (مثلاً D:\\Projects).");
        if (!await PickRootAsync()) Close();
    }

    private async Task<bool> PickRootAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "پوشه اصلی پروژه‌ها را انتخاب کنید",
            AllowMultiple = false,
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return false;
        _root = path;
        _cfg.Root = path;
        if (_persistConfig) _cfg.Save();
        ShowList();
        Reload();
        return true;
    }

    private async Task ChangeRootAsync() => await PickRootAsync();

    // ---- فهرست پروژه‌ها ----
    public void Reload(string? select = null)
    {
        _projects = ProjectStore.Load(_root);
        _projects.Sort((a, b) => string.CompareOrdinal(b.Number, a.Number));
        _rows = _projects.Select(p => new ProjectRow(p)).ToList();
        var keep = StatusList.SelectedIndex;
        var entries = new List<string> { $"همه پروژه‌ها   ({_projects.Count})" };
        entries.AddRange(Statuses.All.Select(s => $"{s.Label}   ({_projects.Count(p => p.Status == s.Key)})"));
        StatusList.ItemsSource = entries;
        StatusList.SelectedIndex = Math.Max(keep, 0);
        ApplyFilter(select);
        StatusText.Text = $"{_projects.Count} پروژه · پوشه اصلی: {_root}";
    }

    private void ApplyFilter(string? select = null)
    {
        var idx = StatusList.SelectedIndex;
        var status = idx > 0 ? Statuses.All[idx - 1].Key : "";
        var text = (SearchBox.Text ?? "").Trim();
        var visible = _rows.Where(r =>
            (status == "" || r.Project.Status == status) &&
            (text == "" || string.Join(" ", r.Number, r.Owner, r.Location, r.Type, r.StatusLabel, r.Project.Due, r.Project.Note).Contains(text))).ToList();
        var previous = select ?? (ProjectGrid.SelectedItem as ProjectRow)?.Number;
        ProjectGrid.ItemsSource = visible;
        if (previous != null) ProjectGrid.SelectedItem = visible.FirstOrDefault(r => r.Number == previous);
    }

    private Project? CurrentProject() =>
        ProjectPage.IsVisible ? _openProject : (ProjectGrid.SelectedItem as ProjectRow)?.Project;

    private Project? NeedProject()
    {
        var p = CurrentProject();
        if (p == null) _ = MessageDialog.ShowAsync(this, "پوشه‌یار", "ابتدا یک پروژه را از فهرست انتخاب کنید.");
        return p;
    }

    private void Save()
    {
        try { ProjectStore.Save(_root, _projects); }
        catch (IOException e)
        {
            _ = MessageDialog.ShowAsync(this, "ذخیره فهرست ممکن نشد",
                "اگر فایل projects.csv در اکسل باز است آن را ببندید و دوباره امتحان کنید.\n" + e.Message);
        }
    }

    public async Task NewProjectAsync()
    {
        if (string.IsNullOrEmpty(_root)) return;
        var number = ProjectStore.NextNumber(_projects, Jalali.Today().Year);
        var dlg = new ProjectDialog(number, _root);
        await dlg.ShowDialog(this);
        if (!dlg.Accepted) return;
        var project = new Project { Number = number };
        dlg.ApplyTo(project);
        project.Folder = ProjectFolders.RelativePath(number, dlg.SlugText());
        try { ProjectFolders.Create(_root, project, _cfg.Crs); }
        catch (IOException e)
        {
            await MessageDialog.ShowAsync(this, "پوشه تکراری یا خطا در ساخت", e.Message + "\nنام لاتین پوشه را عوض کنید.");
            return;
        }
        catch (UnauthorizedAccessException e)
        {
            await MessageDialog.ShowAsync(this, "خطا در ساخت پوشه", e.Message);
            return;
        }
        _projects.Add(project);
        Save();
        Reload(select: number);
    }

    public async Task EditProjectAsync()
    {
        if (NeedProject() is not { } p) return;
        var dlg = new ProjectDialog(p.Number, _root, p);
        await dlg.ShowDialog(this);
        if (!dlg.Accepted) return;
        dlg.ApplyTo(p);
        Save();
        Reload(select: p.Number);
    }

    public void SetStatus(Project p, string status)
    {
        p.Status = status;
        Save();
        Reload(select: p.Number);
    }

    private ContextMenu BuildRowMenu()
    {
        var statusMenu = new MenuItem { Header = "تغییر وضعیت" };
        foreach (var (key, label) in Statuses.All)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => { if (CurrentProject() is { } p) SetStatus(p, key); };
            statusMenu.Items.Add(item);
        }
        var enter = new MenuItem { Header = "ورود به پروژه" };
        enter.Click += (_, _) => EnterProject();
        var reveal = new MenuItem { Header = "نمایش پوشه در Explorer" };
        reveal.Click += (_, _) => { if (CurrentProject() is { } p) Shell.Reveal(p.FullPath(_root)); };
        var edit = new MenuItem { Header = "ویرایش مشخصات" };
        edit.Click += async (_, _) => await EditProjectAsync();
        return new ContextMenu { Items = { enter, reveal, edit, new Separator(), statusMenu } };
    }

    private async Task ArchiveAsync()
    {
        if (NeedProject() is not { } p) return;
        var src = p.FullPath(_root);
        if (!Directory.Exists(src)) { await MessageDialog.ShowAsync(this, "پوشه پیدا نشد", src); return; }
        var ok = await MessageDialog.AskAsync(this, "بایگانی ZIP",
            $"از پوشه پروژه {p.Number} یک فایل ZIP ساخته شود؟\nپوشه اصلی دست‌نخورده می‌ماند.");
        if (!ok) return;
        try
        {
            var zip = FileOps.Archive(_root, src);
            StatusText.Text = $"ساخته شد: {zip}";
            Shell.Reveal(zip);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await MessageDialog.ShowAsync(this, "خطا در بایگانی", e.Message);
        }
    }

    private void OpenCsv()
    {
        if (string.IsNullOrEmpty(_root)) return;
        var path = ProjectStore.CsvPath(_root);
        if (!File.Exists(path)) Save();
        Shell.Open(path);
    }

    // ---- داخل پروژه ----
    public void EnterProject()
    {
        if (CurrentProject() is not { } p) return;
        var dir = p.FullPath(_root);
        if (!Directory.Exists(dir))
        {
            _ = MessageDialog.ShowAsync(this, "پوشه پیدا نشد", "این پوشه روی دیسک نیست:\n" + dir);
            return;
        }
        _openProject = p;
        _projectDir = dir;
        _treeRoot = new TreeNode(dir, true) { IsExpanded = true };
        FolderTree.ItemsSource = new[] { _treeRoot };
        ListPage.IsVisible = false;
        ProjectPage.IsVisible = true;
        Title = $"پوشه‌یار نقشه‌برداری — {p.Number} {p.Owner}";
        Navigate(dir);
    }

    public void ShowList()
    {
        ProjectPage.IsVisible = false;
        ListPage.IsVisible = true;
        Title = "پوشه‌یار نقشه‌برداری";
    }

    public void Navigate(string path)
    {
        if (!Directory.Exists(path)) return;
        _current = path;
        var inRaw = ProjectFolders.IsInRawFolder(_projectDir, path);
        var rows = new List<FileRow>();
        try
        {
            foreach (var d in Directory.GetDirectories(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                rows.Add(new FileRow(d, true, locked: Path.GetFileName(d) == ProjectFolders.RawFolder));
            foreach (var f in Directory.GetFiles(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                rows.Add(new FileRow(f, false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = e.Message;
        }
        FileGrid.ItemsSource = rows;
        PathBox.Text = path;
        UpBtn.IsEnabled = !SamePath(path, _projectDir);
        FolderTitle.Text = Path.GetFileName(path);
        if (SamePath(path, _projectDir) && _openProject != null)
            FolderSubtitle.Text = $"{_openProject.Owner} — {_openProject.Location} · {_openProject.Type}";
        else
            FolderSubtitle.Text = ProjectFolders.Describe(Path.GetFileName(path)) + (inRaw ? " · فقط‌افزودنی (فایل خام)" : "");
        StatusText.Text = $"{rows.Count} مورد در این پوشه";
        SyncTree(path);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private void GoUp()
    {
        if (!SamePath(_current, _projectDir) && Path.GetDirectoryName(_current) is { } parent)
            Navigate(parent);
    }

    private void SyncTree(string path)
    {
        if (_treeRoot == null) return;
        _syncingTree = true;
        try
        {
            var node = _treeRoot;
            var rel = Path.GetRelativePath(_projectDir, path);
            if (rel != ".")
            {
                foreach (var seg in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    node.IsExpanded = true;
                    node = node.Children.FirstOrDefault(c => c.Name == seg) ?? node;
                }
            }
            if (node.IsDirectory && !SamePath(path, _projectDir)) node.IsExpanded = true;
            FolderTree.SelectedItem = node;
        }
        finally { _syncingTree = false; }
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingTree || FolderTree.SelectedItem is not TreeNode node || string.IsNullOrEmpty(node.FullPath)) return;
        if (node.IsDirectory) Navigate(node.FullPath);
        else if (Path.GetDirectoryName(node.FullPath) is { } dir)
        {
            Navigate(dir);
            FileGrid.SelectedItem = (FileGrid.ItemsSource as List<FileRow>)?.FirstOrDefault(r => r.FullPath == node.FullPath);
        }
    }

    private void ActivateSelectedFile()
    {
        if (FileGrid.SelectedItem is not FileRow row) return;
        if (row.IsDirectory) Navigate(row.FullPath);
        else Shell.Open(row.FullPath);
    }

    private async Task OnDropAsync(DragEventArgs e)
    {
        var sources = (e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>())
            .Select(i => i.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
        if (sources.Count == 0 || string.IsNullOrEmpty(_current)) return;
        var result = FileOps.CopyInto(_projectDir, _current, sources);
        Navigate(_current);
        if (result.Skipped.Count > 0)
            await MessageDialog.ShowAsync(this, "بعضی فایل‌ها کپی نشد",
                "این موارد از قبل در پوشه هستند یا کپی نشدند و دست‌نخورده ماندند:\n" + string.Join("\n", result.Skipped));
        else
            StatusText.Text = $"{result.Copied.Count} مورد کپی شد";
    }

    // برای آزمون‌ها
    public string CurrentFolder => _current;
    public bool IsProjectPageVisible => ProjectPage.IsVisible;
    public IReadOnlyList<Project> Projects => _projects;
    public void CopyFiles(IEnumerable<string> sources)
    {
        var result = FileOps.CopyInto(_projectDir, _current, sources);
        Navigate(_current);
        StatusText.Text = $"{result.Copied.Count} مورد کپی شد";
    }
}
