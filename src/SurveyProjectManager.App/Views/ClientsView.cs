using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>فهرست متقاضیان.</summary>
public sealed class ClientsView : UserControl
{
    private readonly AppState _st;
    private readonly List<ClientRow> _rows;
    private readonly DataGrid _grid = new();
    private readonly TextBox _search = new() { Watermark = "جستجوی نام یا تلفن…", Width = 240 };

    public ClientsView(AppState st)
    {
        _st = st;
        _rows = st.W.Clients.Select(c => new ClientRow(c, st.W)).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,8,Auto,8,Auto") };
        header.Children.Add(UiBuild.Text("متقاضیان", "h1"));
        Grid.SetColumn(_search, 1);
        header.Children.Add(_search);
        var newClient = UiBuild.Btn("+ متقاضی جدید", () => _ = st.EditClient(null));
        Grid.SetColumn(newClient, 3);
        header.Children.Add(newClient);
        var newProject = UiBuild.Btn("+ پروژه جدید", () => _ = st.NewProject(null), primary: true);
        Grid.SetColumn(newProject, 5);
        header.Children.Add(newProject);
        var info = UiBuild.Text("متقاضی کسی است که سفارش می‌دهد. مالک ملک ممکن است فرد دیگری باشد.", "muted");
        info.Margin = new Thickness(0, 4, 0, 12);

        _grid.IsReadOnly = true;
        _grid.AutoGenerateColumns = false;
        _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.RowHeight = 40;
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "متقاضی", Width = new DataGridLength(220), SortMemberPath = nameof(ClientRow.Name),
            CellTemplate = new FuncDataTemplate<ClientRow>((r, _) => new TextBlock { Text = r.Name, FontWeight = Avalonia.Media.FontWeight.Medium, Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center }),
        });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "نوع", Width = new DataGridLength(110),
            CellTemplate = new FuncDataTemplate<ClientRow>((r, _) => { var k = UiBuild.KindPill(r.Kind); k.HorizontalAlignment = HorizontalAlignment.Right; k.Margin = new Thickness(8, 0); return k; }),
        });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "تلفن", Width = new DataGridLength(160),
            CellTemplate = new FuncDataTemplate<ClientRow>((r, _) => new TextBlock { Text = r.Phone, Classes = { "ltr" }, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right }),
        });
        _grid.Columns.Add(new DataGridTextColumn { Header = "پروژه‌ها", Binding = new Binding(nameof(ClientRow.ProjectsText)), Width = new DataGridLength(200) });
        _grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "آخرین پروژه", Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<ClientRow>((r, _) =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
                if (!r.HasLast) return sp;
                sp.Children.Add(new TextBlock { Text = r.LastNumber, Classes = { "ltr", "muted" }, VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new TextBlock { Text = r.LastType, VerticalAlignment = VerticalAlignment.Center });
                sp.Children.Add(new Controls.Badge { Text = r.LastStatusLabel, Tone = r.LastStatus });
                return sp;
            }),
        });
        _grid.DoubleTapped += (_, _) => OpenSelected();
        _grid.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; }
            else if (e.Key == Key.Delete && _grid.SelectedItem is ClientRow r) { _ = _st.DeleteClient(r.Client); e.Handled = true; }
        };
        var open = new MenuItem { Header = "باز کردن صفحه متقاضی" };
        open.Click += (_, _) => OpenSelected();
        var edit = new MenuItem { Header = "ویرایش" };
        edit.Click += (_, _) => { if (_grid.SelectedItem is ClientRow r) _ = _st.EditClient(r.Client); };
        var move = new MenuItem { Header = "انتقال پروژه‌ها به متقاضی دیگر…" };
        move.Click += (_, _) => { if (_grid.SelectedItem is ClientRow r) _ = _st.MoveClientProjects(r.Client); };
        var del = new MenuItem { Header = "حذف متقاضی…" };
        del.Click += (_, _) => { if (_grid.SelectedItem is ClientRow r) _ = _st.DeleteClient(r.Client); };
        _grid.ContextMenu = new ContextMenu { Items = { open, edit, move, new Separator(), del } };
        _search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Apply(); };

        var root = new DockPanel { Margin = new Thickness(24, 20) };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(info, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(info);
        root.Children.Add(_grid);
        Content = root;
        Apply();
    }

    private void OpenSelected()
    {
        if (_grid.SelectedItem is ClientRow r) _st.Show("client", r.Id);
    }

    private void Apply()
    {
        var q = TextNorm.Normalize(_search.Text ?? "");
        _grid.ItemsSource = _rows.Where(r => q.Length == 0 || TextNorm.Normalize(r.Search).Contains(q, StringComparison.Ordinal)).ToList();
    }
}
