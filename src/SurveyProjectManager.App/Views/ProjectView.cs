using FlowDirection = Avalonia.Media.FlowDirection;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using SurveyProjectManager.App.Controls;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>داخل یک پروژه: درخت پوشه‌ها، محتوای پوشه، پیش‌نمایش فایل و افزودن هوشمند فایل.</summary>
public sealed class ProjectView : UserControl
{
    private readonly AppState _st;
    private readonly Project _p;
    private readonly string _dir;
    private string _current = "";
    private TreeNode _root = null!;
    private bool _syncing;

    private readonly TreeView _tree = new() { FlowDirection = FlowDirection.LeftToRight, Background = Brushes.Transparent };
    private readonly DataGrid _files = new();
    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeight.Bold, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Right, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace") };
    private readonly TextBlock _subtitle = new() { Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _emptyMsg = new() { Classes = { "muted" }, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 70, 16, 16), TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _ghostLine = new() { Classes = { "muted" }, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 6) };
    private readonly TextBox _path = new() { IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 12.5 };
    private readonly Button _up = UiBuild.Btn("↑ بالاتر", () => { });
    private readonly Border _preview = new();
    private readonly WrapPanel _meta = new();

    public ProjectView(AppState st, Project p)
    {
        _st = st;
        _p = p;
        _dir = p.FullPath(st.Root);

        var back = UiBuild.Btn("→ پروژه‌ها", () => st.Show("projects", null));
        _up.Click += (_, _) => GoUp();
        var add = UiBuild.Btn("+ افزودن فایل", () => _ = PickAndImportAsync(), primary: true);
        var explorer = UiBuild.Btn("Explorer", () => Shell.Reveal(Directory.Exists(_current) ? _current : _dir));
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,6,Auto,6,*,6,Auto,6,Auto") };
        bar.Children.Add(back);
        Grid.SetColumn(_up, 2);
        Grid.SetColumn(_path, 4);
        Grid.SetColumn(add, 6);
        Grid.SetColumn(explorer, 8);
        bar.Children.Add(_up); bar.Children.Add(_path); bar.Children.Add(add); bar.Children.Add(explorer);

        BuildTree();
        BuildFileGrid();

        var middle = new DockPanel { Margin = new Thickness(0) };
        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        head.Children.Add(_title);
        head.Children.Add(_subtitle);
        DockPanel.SetDock(head, Dock.Top);
        middle.Children.Add(head);
        var hint = UiBuild.Text("دوبار کلیک: ورود به پوشه یا بازکردن فایل با برنامه پیش‌فرض · فایل را از Explorer اینجا بکشید تا کپی شود", "muted", wrap: true);
        hint.Margin = new Thickness(0, 6, 0, 0);
        DockPanel.SetDock(hint, Dock.Bottom);
        middle.Children.Add(hint);
        var list = new DockPanel();
        DockPanel.SetDock(_ghostLine, Dock.Bottom);
        list.Children.Add(_ghostLine);
        var filesHost = new Grid();
        filesHost.Children.Add(_files);
        filesHost.Children.Add(_emptyMsg);
        list.Children.Add(filesHost);
        var dropZone = new Border { Child = list };
        DragDrop.SetAllowDrop(dropZone, true);
        dropZone.Classes.Add("card");
        dropZone.Padding = new Thickness(0);
        dropZone.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None);
        dropZone.AddHandler(DragDrop.DropEvent, async (_, e) => await OnDropAsync(e));
        middle.Children.Add(dropZone);

        var treeCard = new Border { Child = _tree };
        treeCard.Classes.Add("card");
        treeCard.Padding = new Thickness(4);
        _preview.Classes.Add("card");
        _preview.Padding = new Thickness(0);

        var panes = new Grid { ColumnDefinitions = new ColumnDefinitions("270,10,*,10,280") };
        panes.Children.Add(treeCard);
        Grid.SetColumn(middle, 2);
        Grid.SetColumn(_preview, 4);
        panes.Children.Add(middle);
        panes.Children.Add(_preview);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,8,Auto,8,*"), Margin = new Thickness(24, 16) };
        root.Children.Add(bar);
        Grid.SetRow(_meta, 2);
        root.Children.Add(_meta);
        Grid.SetRow(panes, 4);
        root.Children.Add(panes);
        Content = root;

        BuildMeta();
        ResetTree();
        Navigate(_dir);
    }

    public string Number => _p.Number;
    public TreeNode RootNode => _root;
    public IReadOnlyList<FileRow> Rows => (_files.ItemsSource as IReadOnlyList<FileRow>) ?? Array.Empty<FileRow>();
    public FileRow? SelectedRow { get => _files.SelectedItem as FileRow; set => _files.SelectedItem = value; }
    public string CurrentFolder => _current;

    // ---------- ساخت اجزا ----------
    private void BuildMeta()
    {
        _meta.Children.Clear();
        void Add(Control c) { c.Margin = new Thickness(0, 0, 18, 0); _meta.Children.Add(c); }
        var client = _st.W.ClientOf(_p);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(UiBuild.Text("متقاضی:", "muted"));
        var link = new Button { Content = client?.Name ?? "—", Classes = { "link" }, IsEnabled = client != null };
        link.Click += (_, _) => { if (client != null) _st.Show("client", client.Id); };
        row.Children.Add(link);
        Add(row);
        Add(UiBuild.Text("مالک ملک: " + _p.Owner, "muted"));
        var rel = _st.W.RelatedTo(_p);
        if (rel.Count > 0)
        {
            var rrow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            rrow.Children.Add(UiBuild.Text("پروژه‌های مرتبط (همین ملک):", "muted"));
            foreach (var r in rel)
            {
                var b = new Button { Classes = { "link" } };
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                sp.Children.Add(new TextBlock { Text = r.Number, Classes = { "ltr" } });
                sp.Children.Add(new TextBlock { Text = r.Type });
                b.Content = sp;
                b.Click += (_, _) => _st.OpenProject(r.Number, null, null);
                rrow.Children.Add(b);
            }
            Add(rrow);
        }
    }

    private void BuildTree()
    {
        _tree.ItemTemplate = new FuncTreeDataTemplate<TreeNode>((n, _) =>
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Opacity = n.NodeOpacity };
            sp.Children.Add(new KindIcon { Kind = n.IconKind, Ghost = n.IsGhost });
            sp.Children.Add(new TextBlock
            {
                Text = n.Name, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 12.5,
                FontStyle = n.IsGhost ? FontStyle.Italic : FontStyle.Normal, VerticalAlignment = VerticalAlignment.Center,
            });
            if (n.IsGhost) ToolTip.SetTip(sp, "هنوز ساخته نشده؛ با اولین فایل ساخته می‌شود");
            return sp;
        }, n => n.Children);
        var style = new Style(x => x.OfType<TreeViewItem>());
        style.Setters.Add(new Setter(TreeViewItem.IsExpandedProperty, new Binding(nameof(TreeNode.IsExpanded)) { Mode = BindingMode.TwoWay }));
        _tree.Styles.Add(style);
        _tree.SelectionChanged += OnTreeSelection;
    }

    private void BuildFileGrid()
    {
        _files.IsReadOnly = true;
        _files.AutoGenerateColumns = false;
        _files.SelectionMode = DataGridSelectionMode.Single;
        _files.CanUserSortColumns = false;
        _files.RowHeight = 34;
        _files.Background = Brushes.Transparent;
        _files.BorderThickness = new Thickness(0);
        _files.Columns.Add(new DataGridTemplateColumn
        {
            Header = "نام", Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<FileRow>((r, _) =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new KindIcon { Kind = r.IconKind });
                sp.Children.Add(new TextBlock { Text = r.Name, FlowDirection = FlowDirection.LeftToRight, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
                if (r.Note.Length > 0) sp.Children.Add(UiBuild.Text(r.Note, "muted"));
                return sp;
            }),
        });
        _files.Columns.Add(new DataGridTextColumn { Header = "نوع", Binding = new Binding(nameof(FileRow.Kind)), Width = new DataGridLength(150) });
        _files.Columns.Add(new DataGridTemplateColumn
        {
            Header = "حجم / تعداد", Width = new DataGridLength(110),
            CellTemplate = new FuncDataTemplate<FileRow>((r, _) => new TextBlock { Text = r.SizeText, FlowDirection = r.IsDirectory ? FlowDirection.RightToLeft : FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) }),
        });
        _files.Columns.Add(new DataGridTextColumn { Header = "تاریخ تغییر", Binding = new Binding(nameof(FileRow.DateText)), Width = new DataGridLength(110) });
        _files.DoubleTapped += (_, _) => ActivateSelected();
        _files.SelectionChanged += (_, _) => UpdatePreview();
        _files.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { ActivateSelected(); e.Handled = true; }
            else if (e.Key == Key.Back) { GoUp(); e.Handled = true; }
        };
        var open = new MenuItem { Header = "بازکردن" };
        open.Click += (_, _) => ActivateSelected();
        var reveal = new MenuItem { Header = "نمایش در Explorer" };
        reveal.Click += (_, _) => { if (_files.SelectedItem is FileRow r) Shell.Reveal(r.FullPath); };
        var copy = new MenuItem { Header = "کپی مسیر" };
        copy.Click += async (_, _) => { if (_files.SelectedItem is FileRow r) await Shell.CopyTextAsync(this, r.FullPath); };
        _files.ContextMenu = new ContextMenu { Items = { open, reveal, copy } };
    }

    // ---------- ناوبری ----------
    private void ResetTree()
    {
        _root = new TreeNode(_dir, true, Path.GetFileName(_dir), isProjectRoot: true) { IsExpanded = true };
        _syncing = true;
        _tree.ItemsSource = new[] { _root };
        _syncing = false;
    }

    public void Refresh()
    {
        BuildMeta();
        ResetTree();
        Navigate(Directory.Exists(_current) || IsStandardCategory(_current) ? _current : _dir);
    }

    private bool IsStandardCategory(string path) =>
        Path.GetDirectoryName(path) is { } parent && SamePath(parent, _dir) && ProjectFolders.Subfolders.Any(s => s.Name == Path.GetFileName(path));

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public void Navigate(string path, string? selectFile = null)
    {
        var exists = Directory.Exists(path);
        if (!exists && !IsStandardCategory(path)) return;
        _current = path;
        var atRoot = SamePath(path, _dir);
        var rows = new List<FileRow>();
        if (exists)
        {
            try
            {
                foreach (var d in Directory.GetDirectories(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    rows.Add(new FileRow(d, true, CountFiles(d)));
                foreach (var f in Directory.GetFiles(path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    rows.Add(new FileRow(f, false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _st.Toast(e.Message); }
        }
        _files.ItemsSource = rows;
        _path.Text = path;
        _up.IsEnabled = !atRoot;
        _title.Text = Path.GetFileName(path);
        var inRaw = ProjectFolders.IsInRawFolder(_dir, path);
        _subtitle.Text = atRoot
            ? $"{_p.Owner} — {_p.Location} · {_p.Type}"
            : ProjectFolders.Describe(Path.GetFileName(path)) + (inRaw ? " · فقط‌افزودنی: فایل‌ها بعد از کپی فقط‌خواندنی می‌شوند" : "");
        _emptyMsg.IsVisible = rows.Count == 0;
        _emptyMsg.Text = !exists
            ? "این پوشه هنوز روی دیسک ساخته نشده. با اولین فایلی که اینجا رها کنید ساخته می‌شود."
            : "این پوشه خالی است. فایل را از Explorer اینجا بکشید.";
        var missing = atRoot && exists ? ProjectFolders.MissingSubfolders(_dir).ToList() : new List<string>();
        _ghostLine.IsVisible = missing.Count > 0;
        _ghostLine.Text = "دسته‌های ساخته‌نشده (با اولین فایل ساخته می‌شوند): " + string.Join(" · ", missing.Select(ProjectFolders.Describe));
        _ghostLine.FlowDirection = FlowDirection.RightToLeft;
        SyncTree(path);
        if (selectFile != null) _files.SelectedItem = rows.FirstOrDefault(r => r.Name == selectFile);
        UpdatePreview();
    }

    private static int CountFiles(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Take(100_000).Count(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    private void GoUp()
    {
        if (!SamePath(_current, _dir) && Path.GetDirectoryName(_current) is { } parent) Navigate(parent);
    }

    private void SyncTree(string path)
    {
        _syncing = true;
        try
        {
            var node = _root;
            var rel = Path.GetRelativePath(_dir, path);
            if (rel != ".")
                foreach (var seg in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                {
                    node.IsExpanded = true;
                    node = node.Children.FirstOrDefault(c => c.Name == seg) ?? node;
                }
            if (node.IsDirectory && !node.IsGhost && !SamePath(path, _dir)) node.IsExpanded = true;
            _tree.SelectedItem = node;
        }
        finally { _syncing = false; }
    }

    private void OnTreeSelection(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _tree.SelectedItem is not TreeNode n || string.IsNullOrEmpty(n.FullPath)) return;
        if (n.IsDirectory) Navigate(n.FullPath);
        else if (Path.GetDirectoryName(n.FullPath) is { } dir) Navigate(dir, Path.GetFileName(n.FullPath));
    }

    private void ActivateSelected()
    {
        if (_files.SelectedItem is not FileRow r) return;
        if (r.IsDirectory) Navigate(r.FullPath);
        else Shell.Open(r.FullPath);
    }

    private void UpdatePreview() =>
        _preview.Child = _files.SelectedItem is FileRow { IsDirectory: false } r
            ? PreviewBuilder.ForFile(r.FullPath, Shell.Open)
            : PreviewBuilder.ForFolder(_p, _current, _st.W);

    // ---------- افزودن فایل ----------
    private async Task PickAndImportAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "فایل‌هایی که می‌خواهید به پروژه اضافه شوند", AllowMultiple = true });
        var paths = picked.Select(f => f.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
        if (paths.Count > 0) await ImportAsync(paths);
    }

    private async Task OnDropAsync(DragEventArgs e)
    {
        var paths = (e.DataTransfer.TryGetFiles() ?? Array.Empty<IStorageItem>())
            .Select(i => i.TryGetLocalPath()).Where(p => !string.IsNullOrEmpty(p)).Select(p => p!).ToList();
        if (paths.Count > 0) await ImportAsync(paths);
    }

    public async Task ImportAsync(IEnumerable<string> paths)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var fallback = ProjectFolders.Subfolders.Select(s => s.Name).FirstOrDefault(n => n == Path.GetFileName(_current) && SamePath(Path.GetDirectoryName(_current) ?? "", _dir));
        var dlg = new ImportDialog(_dir, $"{_p.Number} {_p.Owner}", paths, fallback);
        await dlg.ShowDialog(owner);
        if (dlg.Result is not { } r) return;
        _st.InvalidateIndex();
        Refresh();
        var msg = $"{Digits.Fa(r.Copied.Count)} فایل کپی شد";
        if (r.CreatedFolders.Count > 0) msg += " · پوشه ساخته شد: " + string.Join("، ", r.CreatedFolders.Select(ProjectFolders.Describe));
        if (r.Archived.Count > 0) msg += $" · {Digits.Fa(r.Archived.Count)} نسخه قبلی به آرشیو رفت";
        _st.Toast(msg);
        if (r.Skipped.Count > 0)
            await MessageDialog.ShowAsync(owner, "بعضی فایل‌ها کپی نشد", "این موارد کپی نشدند و دست‌نخورده ماندند:\n" + string.Join("\n", r.Skipped));
    }
}
