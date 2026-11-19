using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>فهرست همه پروژه‌ها با فیلتر سال و وضعیت و جستجو.</summary>
public sealed class ProjectsView : UserControl
{
    private readonly AppState _st;
    private readonly List<ProjectRow> _rows;
    private readonly DataGrid _grid = new();
    private readonly TextBox _search = new() { Watermark = "جستجو: مالک، متقاضی، پلاک، شماره…", Width = 260 };
    private readonly WrapPanel _chips = new() { Margin = new Thickness(0, 12) };
    private string _year = "all", _status = "all";

    public ProjectsView(AppState st)
    {
        _st = st;
        _rows = st.W.Projects.Select(p => new ProjectRow(p, st.W, DateTime.Today)).ToList();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,8,Auto") };
        header.Children.Add(UiBuild.Text("پروژه‌ها", "h1"));
        Grid.SetColumn(_search, 1);
        header.Children.Add(_search);
        var add = UiBuild.Btn("+ پروژه جدید", () => _ = st.NewProject(null), primary: true);
        Grid.SetColumn(add, 3);
        header.Children.Add(add);

        BuildGrid();
        var hint = UiBuild.Text("دوبار کلیک روی پروژه، آن را در یک تب جدا باز می‌کند. راست‌کلیک: تغییر وضعیت و ویرایش.", "muted");
        var root = new DockPanel { Margin = new Thickness(24, 20) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_chips, Dock.Top);
        DockPanel.SetDock(hint, Dock.Bottom);
        hint.Margin = new Thickness(0, 8, 0, 0);
        root.Children.Add(header);
        root.Children.Add(_chips);
        root.Children.Add(hint);
        root.Children.Add(_grid);
        Content = root;

        _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Apply(); };
        BuildChips();
        Apply();
    }

    private void BuildGrid()
    {
        _grid.IsReadOnly = true;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.CanUserSortColumns = true;
        _grid.RowHeight = 38;
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "شماره", Width = new DataGridLength(110), SortMemberPath = nameof(ProjectRow.Number),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) => new TextBlock { Text = r.Number, Classes = { "ltr" }, Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center }),
        });
        _grid.Columns.Add(new DataGridTextColumn { Header = "مالک ملک", Binding = new Binding(nameof(ProjectRow.Owner)), Width = new DataGridLength(170) });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "متقاضی", Width = new DataGridLength(150), SortMemberPath = nameof(ProjectRow.ClientName),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) =>
            {
                if (!r.HasOtherClient) return UiBuild.Text("همان مالک", "muted");
                var b = new Button { Content = r.ClientName, Classes = { "link" }, VerticalAlignment = VerticalAlignment.Center };
                b.Click += (_, _) => _st.Show("client", r.ClientId);
                return b;
            }),
        });
        _grid.Columns.Add(new DataGridTextColumn { Header = "موقعیت / پلاک", Binding = new Binding(nameof(ProjectRow.Location)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "نوع کار", Binding = new Binding(nameof(ProjectRow.Type)), Width = new DataGridLength(140) });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "وضعیت", Width = new DataGridLength(120), SortMemberPath = nameof(ProjectRow.StatusLabel),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) => new Controls.Badge { Text = r.StatusLabel, Tone = r.Status, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 0) }),
        });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "تحویل", Width = new DataGridLength(220),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
                sp.Children.Add(new TextBlock { Text = r.Due, VerticalAlignment = VerticalAlignment.Center });
                if (r.HasBadge) sp.Children.Add(new Controls.Badge { Text = r.BadgeText, Tone = r.BadgeTone });
                return sp;
            }),
        });
        _grid.DoubleTapped += (_, _) => OpenSelected();
        _grid.KeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } };
        _grid.ContextMenu = BuildMenu();
    }

    private ProjectRow? Selected => _grid.SelectedItem as ProjectRow;

    private void OpenSelected()
    {
        if (Selected is { } r) _st.OpenProject(r.Number, null, null);
    }

    private ContextMenu BuildMenu()
    {
        var status = new MenuItem { Header = "تغییر وضعیت" };
        foreach (var (key, label) in Statuses.All)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) =>
            {
                if (Selected is not { } r) return;
                r.Project.Status = key;
                _st.Save(TopLevel.GetTopLevel(this) as Window ?? throw new InvalidOperationException());
                _st.RefreshAll();
            };
            status.Items.Add(item);
        }
        var open = new MenuItem { Header = "ورود به پروژه" };
        open.Click += (_, _) => OpenSelected();
        var client = new MenuItem { Header = "رفتن به صفحه متقاضی" };
        client.Click += (_, _) => { if (Selected is { } r && r.ClientId.Length > 0) _st.Show("client", r.ClientId); };
        var edit = new MenuItem { Header = "ویرایش مشخصات" };
        edit.Click += (_, _) => { if (Selected is { } r) _ = _st.EditProject(r.Project); };
        var reveal = new MenuItem { Header = "نمایش پوشه در Explorer" };
        reveal.Click += (_, _) => { if (Selected is { } r) Shell.Reveal(r.Project.FullPath(_st.Root)); };
        return new ContextMenu { Items = { open, client, edit, reveal, new Separator(), status } };
    }

    private void BuildChips()
    {
        _chips.Children.Clear();
        void Chip(string text, bool on, Action click)
        {
            var b = new Button { Content = text, Classes = { "chip" }, Margin = new Thickness(0, 0, 6, 6) };
            if (on) b.Classes.Add("on");
            b.Click += (_, _) => click();
            _chips.Children.Add(b);
        }
        Chip("همه سال‌ها", _year == "all", () => { _year = "all"; BuildChips(); Apply(); });
        foreach (var y in _st.W.Projects.Select(p => p.Year).Distinct().OrderByDescending(y => y))
            Chip(Digits.Fa(y), _year == y, () => { _year = y; BuildChips(); Apply(); });
        _chips.Children.Add(new Border { Width = 12 });
        Chip("همه وضعیت‌ها", _status == "all", () => { _status = "all"; BuildChips(); Apply(); });
        foreach (var (k, l) in Statuses.All)
            Chip($"{l} {Digits.Fa(_st.W.Projects.Count(p => p.Status == k))}", _status == k, () => { _status = k; BuildChips(); Apply(); });
    }

    private void Apply()
    {
        var q = TextNorm.Normalize(_search.Text ?? "");
        var rows = _rows.Where(r => (_year == "all" || r.Year == _year) && (_status == "all" || r.Status == _status) &&
                                    (q.Length == 0 || TextNorm.Normalize(r.Search).Contains(q, StringComparison.Ordinal))).ToList();
        _grid.ItemsSource = rows;
    }
}
