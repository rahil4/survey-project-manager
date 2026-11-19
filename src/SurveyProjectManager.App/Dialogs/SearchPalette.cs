using FlowDirection = Avalonia.Media.FlowDirection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.App.Controls;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

public sealed class SearchItem
{
    public string Kind { get; init; } = "";      // header | client | project | file
    public string Title { get; init; } = "";
    public string Sub { get; init; } = "";
    public string Tag { get; init; } = "";
    public FileEntry? File { get; init; }
    public bool IsHeader => Kind == "header";
}

/// <summary>جستجوی سراسری (Ctrl+K): متقاضی، پروژه و نام فایل در همه پروژه‌ها.</summary>
public sealed class SearchPalette : Window
{
    private readonly AppState _st;
    private readonly string? _current;
    private readonly TextBox _box = new() { Watermark = "متقاضی، مالک، پلاک، شماره پروژه یا نام فایل…", FontSize = 16, Padding = new Thickness(16, 12), BorderThickness = new Thickness(0, 0, 0, 1) };
    private readonly ListBox _list = new() { MaxHeight = 380, Background = Brushes.Transparent };
    private IReadOnlyList<FileEntry> _index = Array.Empty<FileEntry>();

    public string Query { get => _box.Text ?? ""; set => _box.Text = value; }

    public IReadOnlyList<SearchItem> Items => (_list.ItemsSource as List<SearchItem>) ?? new List<SearchItem>();

    /// <summary>گزینه انتخاب‌شده (پس از بسته شدن پنجره خوانده می‌شود).</summary>
    public SearchItem? Chosen { get; private set; }

    public SearchPalette(AppState st, string? currentProject)
    {
        _st = st;
        _current = currentProject;
        Title = "جستجو";
        FlowDirection = FlowDirection.RightToLeft;
        Width = 640;
        SizeToContent = SizeToContent.Height;
        SystemDecorations = SystemDecorations.BorderOnly;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SearchItem>((it, _) => BuildItem(it));
        var root = new StackPanel();
        root.Children.Add(_box);
        root.Children.Add(_list);
        Content = root;

        _box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };
        _box.KeyDown += OnBoxKey;
        _list.DoubleTapped += (_, _) => Choose();
        _list.Tapped += (_, _) => { if (_list.SelectedItem is SearchItem { IsHeader: false }) Choose(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Opened += async (_, _) =>
        {
            _box.Focus();
            Refresh();
            await _st.EnsureIndexAsync();
            _index = _st.Index;
            Refresh();
        };
        Deactivated += (_, _) => { if (Chosen == null && IsVisible) Close(); };
    }

    private Control BuildItem(SearchItem? it)
    {
        if (it == null) return new TextBlock();
        if (it.IsHeader)
            return new TextBlock { Text = it.Title, FontSize = 11.5, Margin = new Thickness(6, 8, 0, 0), IsHitTestVisible = false, Classes = { "muted" } };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(6, 2) };
        if (it.Kind == "file") row.Children.Add(new KindIcon { Kind = KindIcon.KindOf(it.Title) });
        else if (it.Kind == "client") row.Children.Add(UiBuild.KindPill("متقاضی"));
        else row.Children.Add(new TextBlock { Text = it.Tag, Classes = { "ltr", "muted" }, FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = it.Title, FlowDirection = it.Kind == "file" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft, VerticalAlignment = VerticalAlignment.Center });
        if (it.Sub.Length > 0) row.Children.Add(UiBuild.Text(it.Sub, "muted"));
        return row;
    }

    public void Refresh()
    {
        var q = _box.Text ?? "";
        var r = FileIndex.Search(_st.W, _index, q, _current);
        var items = new List<SearchItem>();
        if (r.Clients.Count > 0)
        {
            items.Add(new SearchItem { Kind = "header", Title = "متقاضیان" });
            items.AddRange(r.Clients.Select(c => new SearchItem { Kind = "client", Title = c.Name, Sub = $"{Digits.Fa(_st.W.ProjectsOf(c.Id).Count())} پروژه", Tag = c.Id }));
        }
        if (r.Projects.Count > 0)
        {
            items.Add(new SearchItem { Kind = "header", Title = q.Trim().Length == 0 ? "پروژه‌های اخیر" : "پروژه‌ها" });
            items.AddRange(r.Projects.Select(p => new SearchItem { Kind = "project", Title = p.Owner, Sub = p.Location, Tag = p.Number }));
        }
        if (r.Files.Count > 0)
        {
            items.Add(new SearchItem { Kind = "header", Title = "فایل‌ها" });
            items.AddRange(r.Files.Select(f => new SearchItem
            {
                Kind = "file", Title = f.Name, Tag = f.ProjectNumber, File = f,
                Sub = $"{_st.W.Find(f.ProjectNumber)?.Owner} · {f.Folder}",
            }));
        }
        if (items.Count == 0 && q.Trim().Length > 0) items.Add(new SearchItem { Kind = "header", Title = "چیزی پیدا نشد." });
        _list.ItemsSource = items;
        _list.SelectedItem = items.FirstOrDefault(i => !i.IsHeader);
    }

    private void OnBoxKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Choose(); e.Handled = true; }
        else if (e.Key is Key.Down or Key.Up)
        {
            var items = (_list.ItemsSource as List<SearchItem>) ?? new();
            var idx = items.IndexOf((SearchItem?)_list.SelectedItem!);
            var step = e.Key == Key.Down ? 1 : -1;
            for (var i = idx + step; i >= 0 && i < items.Count; i += step)
                if (!items[i].IsHeader) { _list.SelectedItem = items[i]; _list.ScrollIntoView(items[i]); break; }
            e.Handled = true;
        }
    }

    public void Choose()
    {
        if (_list.SelectedItem is SearchItem { IsHeader: false } it)
        {
            Chosen = it;
            Close();
        }
    }
}
