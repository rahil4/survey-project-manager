using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>صفحه اصلی: خلاصه کار امروز، موعدها، پروژه‌های اخیر و وضعیت پشتیبان.</summary>
public sealed class HomeView : UserControl
{
    private static readonly Dictionary<string, string> SegKeys = new()
    {
        ["collect"] = "SegCollect", ["field"] = "SegField", ["draw"] = "SegDraw", ["check"] = "SegCheck", ["done"] = "SegDone", ["reg"] = "SegReg",
    };

    public HomeView(AppState st)
    {
        var root = new StackPanel { Spacing = 16, Margin = new Thickness(24, 20) };
        var year = Jalali.Today().Year;
        var thisYear = st.W.Projects.Where(p => p.Year == year.ToString()).ToList();

        // ---- بالای صفحه ----
        var hero = new StackPanel { Spacing = 4 };
        hero.Children.Add(UiBuild.Text("خلاصه کار امروز", "h1"));
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 18 };
        meta.Children.Add(UiBuild.Text(Digits.Fa(Jalali.Long(DateTime.Today)), "muted"));
        meta.Children.Add(UiBuild.Text($"{Digits.Fa(thisYear.Count)} پروژه در {Digits.Fa(year)} · {Digits.Fa(st.W.Clients.Count)} متقاضی", "muted"));
        meta.Children.Add(new TextBlock { Text = st.Cfg.Crs, Classes = { "ltr", "muted" } });
        hero.Children.Add(meta);

        var bar = new Grid { Height = 12, Margin = new Thickness(0, 10, 0, 4), ColumnSpacing = 2 };
        var legend = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        var col = 0;
        foreach (var (key, label) in Statuses.All)
        {
            var n = thisYear.Count(p => p.Status == key);
            if (n > 0)
            {
                bar.ColumnDefinitions.Add(new ColumnDefinition(n, GridUnitType.Star));
                var seg = new Border { CornerRadius = new CornerRadius(col == 0 ? 6 : 2) };
                UiBuild.BindRes(seg, Border.BackgroundProperty, SegKeys[key]);
                Grid.SetColumn(seg, col++);
                bar.Children.Add(seg);
            }
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 16, 0) };
            var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
            UiBuild.BindRes(dot, Border.BackgroundProperty, SegKeys[key]);
            item.Children.Add(dot);
            item.Children.Add(UiBuild.Text($"{label} {Digits.Fa(n)}", "muted"));
            legend.Children.Add(item);
        }
        if (col == 0) bar.Children.Add(new Border { Background = Brushes.Gray, Opacity = .2, CornerRadius = new CornerRadius(6) });
        hero.Children.Add(bar);
        hero.Children.Add(legend);
        var soft = new Border { Child = hero };
        soft.Classes.Add("soft");
        root.Children.Add(soft);

        // ---- کارت‌ها ----
        var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 16 };
        cards.Children.Add(DueCard(st, thisYear));
        var recent = RecentCard(st);
        Grid.SetColumn(recent, 1);
        cards.Children.Add(recent);
        var backup = BackupCard(st);
        Grid.SetColumn(backup, 2);
        cards.Children.Add(backup);
        root.Children.Add(cards);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(UiBuild.Btn("+ پروژه جدید", () => _ = st.NewProject(null), primary: true));
        actions.Children.Add(UiBuild.Btn("متقاضیان", () => st.Show("clients", null)));
        actions.Children.Add(UiBuild.Btn("ورود پوشه‌های موجود…", () => st.Show("legacy", null)));
        actions.Children.Add(UiBuild.Btn("همه پروژه‌ها", () => st.Show("projects", null)));
        root.Children.Add(actions);
        Content = new ScrollViewer { Content = root };
    }

    private static Control ListRow(AppState st, Project p, Control trailing, string subtitle)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Margin = new Thickness(0, 6), Background = Brushes.Transparent, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = p.Owner });
        left.Children.Add(UiBuild.Text(subtitle, "muted", trim: true));
        Grid.SetColumn(left, 1);
        Grid.SetColumn(trailing, 2);
        grid.Children.Add(left);
        grid.Children.Add(trailing);
        grid.PointerPressed += (_, _) => st.OpenProject(p.Number, null, null);
        return grid;
    }

    private static Control DueCard(AppState st, List<Project> thisYear)
    {
        var list = new StackPanel();
        list.Children.Add(UiBuild.Text("موعد تحویل", "h2"));
        var due = thisYear.Where(p => p.Status != "done" && Jalali.DaysUntil(p.Due) != null)
            .OrderBy(p => Jalali.DaysUntil(p.Due)).Take(5).ToList();
        foreach (var p in due)
        {
            var days = Jalali.DaysUntil(p.Due)!.Value;
            var (text, tone) = days < 0 ? ($"{Digits.Fa(-days)} روز عقب‌افتاده", "reg") : days <= 3 ? ($"{Digits.Fa(days)} روز مانده", "field") : ($"{Digits.Fa(days)} روز مانده", "collect");
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
            var status = new Controls.Badge { Text = p.StatusLabel, Tone = p.Status };
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new TextBlock { Text = p.Owner });
            left.Children.Add(UiBuild.Text($"{p.Location} · {p.Type}", "muted", trim: true));
            Grid.SetColumn(left, 1);
            var badge = new Controls.Badge { Text = text, Tone = tone };
            Grid.SetColumn(badge, 2);
            row.Children.Add(status);
            row.Children.Add(left);
            row.Children.Add(badge);
            row.Margin = new Thickness(0, 6);
            row.Background = Brushes.Transparent;
            row.PointerPressed += (_, _) => st.OpenProject(p.Number, null, null);
            list.Children.Add(row);
        }
        if (due.Count == 0) list.Children.Add(UiBuild.Text("پروژه‌ای با تاریخ تحویل ثبت نشده است.", "muted", wrap: true));
        return UiBuild.Card(list);
    }

    private static Control RecentCard(AppState st)
    {
        var list = new StackPanel();
        list.Children.Add(UiBuild.Text("اخیراً باز شده", "h2"));
        var recent = st.Cfg.Recents.Select(n => st.W.Find(n)).Where(p => p != null).Take(5).ToList();
        if (recent.Count == 0)
            recent = st.W.Projects.Take(5).ToList()!;
        foreach (var p in recent)
        {
            var num = new TextBlock { Text = p!.Number, Classes = { "ltr", "muted" }, VerticalAlignment = VerticalAlignment.Center };
            list.Children.Add(ListRow(st, p, num, p.Location));
        }
        return UiBuild.Card(list);
    }

    private static Control BackupCard(AppState st)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(UiBuild.Text("پشتیبان‌گیری", "h2"));
        var cfg = st.Cfg;
        var hasDest = cfg.BackupDest.Length > 0;
        var last = DateTime.TryParse(cfg.LastBackup, out var at) ? at : (DateTime?)null;
        string status;
        double fraction = 1;
        if (last == null) status = hasDest ? "هنوز پشتیبان‌گیری نشده است." : "مقصد پشتیبان هنوز انتخاب نشده است.";
        else
        {
            var days = (int)(DateTime.Now - last.Value).TotalDays;
            status = days == 0 ? "آخرین پشتیبان: امروز" : $"آخرین پشتیبان: {Digits.Fa(days)} روز پیش";
            if (cfg.BackupRemindDays > 0)
            {
                fraction = Math.Min(1, days / (double)cfg.BackupRemindDays);
                var left = cfg.BackupRemindDays - days;
                panel.Children.Add(new TextBlock { Text = status, FontWeight = FontWeight.Medium });
                var bar = new ProgressBar { Minimum = 0, Maximum = 1, Value = fraction, Height = 8 };
                panel.Children.Add(bar);
                panel.Children.Add(UiBuild.Text(left > 0 ? $"{Digits.Fa(left)} روز تا یادآوری بعدی" : "زمان پشتیبان‌گیری گذشته است", "muted"));
                panel.Children.Add(UiBuild.Btn("پشتیبان‌گیری", () => st.Show("backup", null), primary: true));
                return UiBuild.Card(panel);
            }
        }
        panel.Children.Add(new TextBlock { Text = status, TextWrapping = TextWrapping.Wrap });
        if (hasDest) panel.Children.Add(new TextBlock { Text = cfg.BackupDest, Classes = { "ltr", "muted" }, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(UiBuild.Btn(hasDest ? "پشتیبان‌گیری" : "انتخاب مقصد", () => st.Show("backup", null), primary: true));
        return UiBuild.Card(panel);
    }
}
