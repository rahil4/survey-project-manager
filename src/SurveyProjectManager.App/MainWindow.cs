using FlowDirection = Avalonia.Media.FlowDirection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.App.Views;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App;

/// <summary>پنجره اصلی: نوار تب‌ها، ستون بخش‌ها (سمت راست) و محتوا.</summary>
public sealed class MainWindow : Window
{
    private static readonly (string Key, string Label, string Icon)[] Nav =
    {
        ("home", "خانه", "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"),
        ("projects", "پروژه‌ها", "M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z"),
        ("clients", "متقاضیان", "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"),
        ("search", "جستجو", "M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"),
        ("backup", "پشتیبان", "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z"),
        ("legacy", "ورود پوشه", "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z"),
    };

    private const string MoonIcon = "M9 2c-1.05 0-2.05.16-3 .46 4.06 1.27 7 5.06 7 9.54 0 4.48-2.94 8.27-7 9.54.95.3 1.95.46 3 .46 5.52 0 10-4.48 10-10S14.52 2 9 2z";

    private readonly AppState _st;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly StackPanel _rail = new() { Spacing = 4, Margin = new Thickness(0, 10) };
    private readonly ContentControl _host = new();
    private readonly Border _toast = new() { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8) };
    private readonly TextBlock _toastText = new();
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    private readonly List<string> _openTabs = new();
    private readonly Dictionary<string, ProjectView> _views = new();
    private string _active = "home";
    private string _section = "home";
    private string? _sectionArg;

    public MainWindow() : this(new AppConfig(), false) { }

    public MainWindow(AppConfig cfg, bool persistConfig)
    {
        Digits.Persian = cfg.PersianDigits;
        var root = cfg.Root;
        if (root.Length > 0) { try { Directory.CreateDirectory(root); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
        var ws = root.Length > 0 && Directory.Exists(root) ? Workspace.Load(root) : new Workspace(root);
        _st = new AppState(cfg, persistConfig, ws)
        {
            Show = ShowSection, OpenProject = OpenProject, RefreshAll = RefreshAll, Toast = ShowToast,
            NewProject = NewProjectAsync, EditProject = EditProjectAsync, EditClient = EditClientAsync, ChooseRoot = ChooseRootAsync,
        };
        ApplyTheme(cfg.Theme);

        Title = "پوشه‌یار نقشه‌برداری";
        Width = 1320;
        Height = 800;
        MinWidth = 1000;
        MinHeight = 560;
        FlowDirection = FlowDirection.RightToLeft;
        FontSize = 14;
        FontFamily = new FontFamily("Segoe UI, Tahoma, Vazirmatn, Arial");

        _toast.Child = _toastText;
        UiBuild.BindRes(_toast, Border.BackgroundProperty, "FgBrush");
        UiBuild.BindRes(_toastText, TextBlock.ForegroundProperty, "BgBrush");
        _toastTimer.Tick += (_, _) => { _toast.IsVisible = false; _toastTimer.Stop(); };

        var strip = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 6, 10, 0) };
        UiBuild.BindRes(strip, Border.BackgroundProperty, "RailBrush");
        UiBuild.BindRes(strip, Border.BorderBrushProperty, "LineBrush");
        var stripGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        stripGrid.Children.Add(_tabs);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
        var search = UiBuild.Btn("جستجو  Ctrl K", () => _ = OpenSearchAsync());
        search.Padding = new Thickness(12, 2);
        search.MinHeight = 30;
        var digits = new Button { Padding = new Thickness(10, 2), MinHeight = 30, MinWidth = 36 };
        digits.Content = Digits.Persian ? "۱۲۳" : "123";
        digits.Click += (_, _) => { ToggleDigits(); digits.Content = Digits.Persian ? "۱۲۳" : "123"; };
        ToolTip.SetTip(digits, "تغییر ارقام فارسی/لاتین");
        var theme = new Button { Padding = new Thickness(8, 2), MinHeight = 30, Content = new PathIcon { Data = Geometry.Parse(MoonIcon), Width = 14, Height = 14 } };
        theme.Click += (_, _) => ToggleTheme();
        ToolTip.SetTip(theme, "تم تیره/روشن");
        tools.Children.Add(search);
        tools.Children.Add(digits);
        tools.Children.Add(theme);
        Grid.SetColumn(tools, 1);
        stripGrid.Children.Add(tools);
        strip.Child = stripGrid;

        var railBorder = new Border { Child = _rail, BorderThickness = new Thickness(1, 0, 0, 0), Width = 76 };
        UiBuild.BindRes(railBorder, Border.BackgroundProperty, "RailBrush");
        UiBuild.BindRes(railBorder, Border.BorderBrushProperty, "LineBrush");

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("76,*") };
        body.Children.Add(railBorder);
        var content = new Panel();
        content.Children.Add(_host);
        content.Children.Add(_toast);
        Grid.SetColumn(content, 1);
        body.Children.Add(content);

        var shell = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        shell.Children.Add(strip);
        Grid.SetRow(body, 1);
        shell.Children.Add(body);
        Content = shell;

        KeyDown += OnKeyDown;
        Opened += async (_, _) => await StartAsync();
        Render();
    }

    public AppState State => _st;
    public Control? CurrentContent => _host.Content as Control;
    public string ActiveKey => _active;
    public IReadOnlyList<string> OpenTabs => _openTabs;
    public ProjectView? ActiveProjectView => _views.GetValueOrDefault(_active);

    // ---------- شروع کار ----------
    private async Task StartAsync()
    {
        if (_st.Root.Length == 0 || !Directory.Exists(_st.Root))
        {
            await MessageDialog.ShowAsync(this, "پوشه‌یار نقشه‌برداری", "برای شروع، پوشه‌ای را که می‌خواهید پروژه‌ها در آن ساخته شوند انتخاب کنید (مثلاً D:\\Projects).");
            await ChooseRootAsync();
            if (_st.Root.Length == 0) { Close(); return; }
        }
        _ = _st.EnsureIndexAsync();
    }

    private async Task ChooseRootAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "پوشه اصلی پروژه‌ها را انتخاب کنید", AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;
        _st.Cfg.Root = path;
        _st.SaveConfig();
        _st.W = Workspace.Load(path);
        _openTabs.Clear();
        _views.Clear();
        _active = "home";
        _section = "home";
        _st.InvalidateIndex();
        _ = _st.EnsureIndexAsync();
        Render();
    }

    // ---------- ناوبری ----------
    public void ShowSection(string key, string? arg = null)
    {
        _section = key;
        _sectionArg = arg;
        _active = "home";
        Render();
    }

    public void OpenProject(string number, string? directory = null, string? file = null)
    {
        var p = _st.W.Find(number);
        if (p == null) return;
        if (!_views.TryGetValue(number, out var view))
        {
            if (!Directory.Exists(p.FullPath(_st.Root)))
            {
                _ = MessageDialog.ShowAsync(this, "پوشه پیدا نشد", "این پوشه روی دیسک نیست:\n" + p.FullPath(_st.Root));
                return;
            }
            view = new ProjectView(_st, p);
            _views[number] = view;
            _openTabs.Add(number);
        }
        _st.Cfg.AddRecent(number);
        _st.SaveConfig();
        _active = number;
        if (directory != null) view.Navigate(directory, file);
        Render();
    }

    public void CloseTab(string number)
    {
        _openTabs.Remove(number);
        _views.Remove(number);
        if (_active == number) _active = "home";
        Render();
    }

    public void RefreshAll()
    {
        foreach (var v in _views.Values) v.Refresh();
        Render();
    }

    private void RebuildProjectViews()
    {
        var states = _views.ToDictionary(kv => kv.Key, kv => kv.Value.CurrentFolder);
        _views.Clear();
        foreach (var number in _openTabs.ToList())
        {
            var p = _st.W.Find(number);
            if (p == null || !Directory.Exists(p.FullPath(_st.Root))) { _openTabs.Remove(number); continue; }
            var v = new ProjectView(_st, p);
            if (states.TryGetValue(number, out var folder) && folder.Length > 0) v.Navigate(folder);
            _views[number] = v;
        }
        if (!_openTabs.Contains(_active)) _active = "home";
    }

    private void Render()
    {
        RenderTabs();
        RenderRail();
        _host.Content = _active == "home" ? BuildSection() : _views[_active];
    }

    private Control BuildSection() => _section switch
    {
        "projects" => new ProjectsView(_st),
        "clients" => new ClientsView(_st),
        "client" => new ClientView(_st, _sectionArg ?? ""),
        "backup" => new BackupView(_st),
        "legacy" => new LegacyView(_st),
        _ => new HomeView(_st),
    };

    private void RenderTabs()
    {
        _tabs.Children.Clear();
        _tabs.Children.Add(MakeTab("home", "پوشه‌یار", null));
        foreach (var n in _openTabs)
        {
            var p = _st.W.Find(n);
            _tabs.Children.Add(MakeTab(n, p?.Owner ?? n, n));
        }
    }

    private Control MakeTab(string key, string title, string? number)
    {
        var selected = _active == key;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (number != null) row.Children.Add(new TextBlock { Text = number, Classes = { "ltr", "muted" }, FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = title, FontWeight = selected ? FontWeight.Medium : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 150, TextTrimming = TextTrimming.CharacterEllipsis });
        if (number != null)
        {
            var close = new Button { Content = "×", Padding = new Thickness(5, 0), MinHeight = 20, MinWidth = 20, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Classes = { "link" } };
            close.Click += (_, e) => { e.Handled = true; CloseTab(number); };
            ToolTip.SetTip(close, "بستن تب");
            row.Children.Add(close);
        }
        var tab = new Border { Child = row, Padding = new Thickness(12, 0), Height = 34, CornerRadius = new CornerRadius(8, 8, 0, 0), Cursor = new Cursor(StandardCursorType.Hand) };
        if (selected) UiBuild.BindRes(tab, Border.BackgroundProperty, "BgBrush");
        else tab.Background = Brushes.Transparent;
        tab.Tag = key;
        tab.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed) { _active = key; Render(); }
        };
        return tab;
    }

    private void RenderRail()
    {
        _rail.Children.Clear();
        foreach (var (key, label, icon) in Nav)
        {
            var on = _active == "home" && (_section == key || (key == "clients" && _section == "client"));
            var content = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new PathIcon { Data = Geometry.Parse(icon), Width = 22, Height = 22 });
            content.Children.Add(new TextBlock { Text = label, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center });
            var b = new Button { Content = content, Classes = { "nav" }, HorizontalAlignment = HorizontalAlignment.Center };
            if (on) b.Classes.Add("on");
            var k = key;
            b.Click += (_, _) => { if (k == "search") _ = OpenSearchAsync(); else ShowSection(k); };
            _rail.Children.Add(b);
        }
    }

    // ---------- ابزارها ----------
    public void ShowToast(string text)
    {
        _toastText.Text = text;
        _toast.IsVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public string ToastText => _toast.IsVisible ? _toastText.Text ?? "" : "";

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Control)) { _ = OpenSearchAsync(); e.Handled = true; }
    }

    public async Task OpenSearchAsync()
    {
        var current = _active == "home" ? null : _active;
        var dlg = new SearchPalette(_st, current);
        await dlg.ShowDialog(this);
        if (dlg.Chosen is not { } it) return;
        switch (it.Kind)
        {
            case "client": ShowSection("client", it.Tag); break;
            case "project": OpenProject(it.Tag); break;
            case "file" when it.File is { } f:
                OpenProject(f.ProjectNumber, Path.GetDirectoryName(f.FullPath), f.Name);
                break;
        }
    }

    private void ApplyTheme(string theme)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = theme switch { "dark" => ThemeVariant.Dark, "light" => ThemeVariant.Light, _ => ThemeVariant.Default };
    }

    public void ToggleTheme()
    {
        var dark = (Application.Current?.ActualThemeVariant ?? ThemeVariant.Light) == ThemeVariant.Dark;
        _st.Cfg.Theme = dark ? "light" : "dark";
        _st.SaveConfig();
        ApplyTheme(_st.Cfg.Theme);
        Render();
    }

    public void ToggleDigits()
    {
        Digits.Persian = !Digits.Persian;
        _st.Cfg.PersianDigits = Digits.Persian;
        _st.SaveConfig();
        RebuildProjectViews();
        Render();
    }

    // ---------- پروژه و متقاضی ----------
    public async Task NewProjectAsync(string? presetClientId)
    {
        if (_st.Root.Length == 0) return;
        var dlg = new ProjectDialog(_st, null, presetClientId);
        await dlg.ShowDialog(this);
        if (!dlg.Accepted || dlg.Request == null) return;
        try
        {
            var p = _st.W.CreateProject(dlg.Request, Jalali.Today().Year, _st.Cfg.Crs);
            _st.InvalidateIndex();
            ShowToast($"پروژه {p.Number} ساخته شد");
            OpenProject(p.Number);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await MessageDialog.ShowAsync(this, "ساخت پروژه ممکن نشد", e.Message + "\nنام لاتین پوشه را عوض کنید یا دسترسی پوشه را بررسی کنید.");
        }
    }

    public async Task EditProjectAsync(Project p)
    {
        var dlg = new ProjectDialog(_st, p);
        await dlg.ShowDialog(this);
        if (!dlg.Accepted) return;
        dlg.ApplyEditTo(p);
        _st.Save(this);
        RefreshAll();
    }

    public async Task EditClientAsync(Client? client)
    {
        var dlg = new ClientDialog(client);
        await dlg.ShowDialog(this);
        if (!dlg.Accepted) return;
        if (client == null)
        {
            var c = new Client();
            dlg.ApplyTo(c);
            _st.W.AddClient(c);
            ShowSection("client", c.Id);
        }
        else
        {
            dlg.ApplyTo(client);
            _st.Save(this);
            RefreshAll();
        }
    }
}
