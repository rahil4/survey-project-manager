using Avalonia;
using Avalonia.Controls;
using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>صفحه یک متقاضی: مشخصات، پروژه‌ها به تفکیک سال و ملک‌هایی که چند بار سفارش داده شده.</summary>
public sealed class ClientView : UserControl
{
    public ClientView(AppState st, string clientId)
    {
        var w = st.W;
        var c = w.Client(clientId);
        if (c == null) { Content = UiBuild.Text("متقاضی پیدا نشد.", "muted"); return; }
        var projects = w.ProjectsOf(clientId).ToList();
        var rows = projects.Select(p => new ProjectRow(p, w, DateTime.Today)).ToList();
        var owners = projects.Select(p => TextNorm.Normalize(p.Owner)).Distinct().Count();
        var years = projects.Select(p => p.Year).Distinct().ToList();

        var root = new StackPanel { Spacing = 14, Margin = new Thickness(24, 20) };
        var back = UiBuild.Btn("→ متقاضیان", () => st.Show("clients", null));
        back.HorizontalAlignment = HorizontalAlignment.Right;
        root.Children.Add(back);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,8,Auto") };
        var info = new StackPanel { Spacing = 4 };
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        title.Children.Add(UiBuild.Text(c.Name, "h1"));
        title.Children.Add(UiBuild.KindPill(c.Kind));
        info.Children.Add(title);
        var contact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (c.Phone.Length > 0)
        {
            contact.Children.Add(UiBuild.Text("تلفن", "muted"));
            contact.Children.Add(new TextBlock { Text = c.Phone, Classes = { "ltr", "muted" } });
        }
        if (c.Note.Length > 0) contact.Children.Add(UiBuild.Text((c.Phone.Length > 0 ? "· " : "") + c.Note, "muted"));
        if (contact.Children.Count > 0) info.Children.Add(contact);
        info.Children.Add(new TextBlock { Text = $"{Digits.Fa(projects.Count)} پروژه در {Digits.Fa(years.Count)} سال · {Digits.Fa(owners)} مالک ملک", FontSize = 13 });
        head.Children.Add(info);
        var edit = UiBuild.Btn("ویرایش", () => _ = st.EditClient(c));
        Grid.SetColumn(edit, 1);
        head.Children.Add(edit);
        var add = UiBuild.Btn($"+ پروژه جدید برای {c.Name}", () => _ = st.NewProject(c.Id), primary: true);
        Grid.SetColumn(add, 3);
        head.Children.Add(add);
        root.Children.Add(UiBuild.Card(head));

        var repeated = w.RepeatedProperties(clientId);
        if (repeated.Count > 0)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(UiBuild.Text("ملک‌هایی که چند بار سفارش داده شده", "h2"));
            foreach (var (location, list) in repeated)
            {
                panel.Children.Add(new TextBlock { Text = location, FontWeight = FontWeight.Medium });
                var line = new WrapPanel();
                for (var i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    var chip = new Button { Classes = { "chip" }, Margin = new Thickness(0, 0, 6, 4) };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    sp.Children.Add(new TextBlock { Text = p.Number, Classes = { "ltr" } });
                    sp.Children.Add(new TextBlock { Text = p.Type });
                    chip.Content = sp;
                    chip.Click += (_, _) => st.OpenProject(p.Number, null, null);
                    line.Children.Add(chip);
                    if (i < list.Count - 1) line.Children.Add(UiBuild.Text("←", "muted"));
                }
                panel.Children.Add(line);
            }
            panel.Children.Add(UiBuild.Text("مدارک ملک (سند، کروکی، استعلام) را هنگام ساخت پروژه جدید می‌شود از پروژه قبلی کپی کرد.", "muted"));
            root.Children.Add(UiBuild.Card(panel));
        }

        var grid = new DataGrid
        {
            IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single, RowHeight = 38,
            MinHeight = 120, CanUserSortColumns = false,
        };
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "شماره", Width = new DataGridLength(110),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) => new TextBlock { Text = r.Number, Classes = { "ltr" }, Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center }),
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "مالک ملک", Width = new DataGridLength(220),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
                sp.Children.Add(new TextBlock { Text = r.Owner, VerticalAlignment = VerticalAlignment.Center });
                if (r.HasOtherClient) sp.Children.Add(UiBuild.KindPill("مالک دیگر"));
                return sp;
            }),
        });
        grid.Columns.Add(new DataGridTextColumn { Header = "موقعیت / پلاک", Binding = new Binding(nameof(ProjectRow.Location)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "نوع کار", Binding = new Binding(nameof(ProjectRow.Type)), Width = new DataGridLength(140) });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "وضعیت", Width = new DataGridLength(120),
            CellTemplate = new FuncDataTemplate<ProjectRow>((r, _) => new Controls.Badge { Text = r.StatusLabel, Tone = r.Status, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 0) }),
        });
        grid.Columns.Add(new DataGridTemplateColumn
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
        var view = new DataGridCollectionView(rows);
        view.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(ProjectRow.YearLabel)));
        grid.ItemsSource = view;
        grid.Height = Math.Max(140, 38 * rows.Count + 34 * Math.Max(1, years.Count) + 40);
        grid.DoubleTapped += (_, _) => { if (grid.SelectedItem is ProjectRow r) st.OpenProject(r.Number, null, null); };
        root.Children.Add(grid);
        root.Children.Add(UiBuild.Text("دوبار کلیک روی پروژه، آن را باز می‌کند.", "muted"));
        Content = new ScrollViewer { Content = root };
    }
}
