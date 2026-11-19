using FlowDirection = Avalonia.Media.FlowDirection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>ورود پوشه‌های پروژه‌های قدیمی به فهرست (سه مرحله).</summary>
public sealed class LegacyView : UserControl
{
    private readonly AppState _st;
    private readonly ContentControl _body = new();
    private string _folder = "";
    private List<LegacyEntry> _entries = new();
    private LegacyMode _mode = LegacyMode.RegisterInPlace;
    private int _importedCount;

    public LegacyView(AppState st)
    {
        _st = st;
        Content = new ScrollViewer { Content = _body };
        ShowStep(1);
    }

    private StackPanel Frame(int step, string title, Control content)
    {
        var root = new StackPanel { Spacing = 10, Margin = new Thickness(24, 20) };
        root.Children.Add(UiBuild.Text("ورود پوشه‌های موجود", "h1"));
        if (step == 1) root.Children.Add(UiBuild.Text("پروژه‌های قدیمی را بدون جابه‌جا کردن فایل‌ها در فهرست ثبت کنید.", "muted"));
        var bars = new Grid { ColumnDefinitions = new ColumnDefinitions("*,6,*,6,*"), Height = 4, Margin = new Thickness(0, 6) };
        for (var i = 0; i < 3; i++)
        {
            var b = new Border { CornerRadius = new CornerRadius(2) };
            UiBuild.BindRes(b, Border.BackgroundProperty, i < step ? "AccentBrush" : "LineBrush");
            Grid.SetColumn(b, i * 2);
            bars.Children.Add(b);
        }
        root.Children.Add(bars);
        var inner = new StackPanel { Spacing = 12 };
        inner.Children.Add(UiBuild.Text(title, "h2"));
        inner.Children.Add(content);
        root.Children.Add(UiBuild.Card(inner));
        return root;
    }

    private void ShowStep(int step)
    {
        if (step == 1) _body.Content = Step1();
        else if (step == 2) _body.Content = Step2();
        else _body.Content = Step3();
    }

    private Control Step1()
    {
        var path = new TextBox { IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, Text = _folder, Watermark = "پوشه‌ای که پروژه‌های قدیمی در آن است", MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Left };
        var pick = UiBuild.Btn("انتخاب پوشه…", async () =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;
            var f = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "پوشه پروژه‌های قدیمی", AllowMultiple = false });
            var p = f.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(p)) { _folder = p; path.Text = p; }
        });
        var scan = UiBuild.Btn("بررسی پوشه", () =>
        {
            if (!Directory.Exists(_folder)) { _st.Toast("ابتدا پوشه را انتخاب کنید."); return; }
            _entries = LegacyScanner.Scan(_folder);
            ShowStep(2);
        }, primary: true);
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(path);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(pick);
        row.Children.Add(scan);
        sp.Children.Add(row);
        return Frame(1, "پوشه‌ای را که پروژه‌های قدیمی در آن است انتخاب کنید", sp);
    }

    public void ScanFolder(string folder)
    {
        _folder = folder;
        _entries = LegacyScanner.Scan(folder);
        ShowStep(2);
    }

    private Control Step2()
    {
        var found = _entries.Count(e => e.LooksLikeProject);
        var rows = new ItemsControl { ItemsSource = _entries };
        rows.ItemTemplate = new FuncDataTemplate<LegacyEntry>((e, _) =>
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("32,230,200,90,*"), Margin = new Thickness(0, 4), MinHeight = 38 };
            var check = new CheckBox { IsEnabled = e.LooksLikeProject, VerticalAlignment = VerticalAlignment.Center };
            check.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(LegacyEntry.Selected)) { Mode = BindingMode.TwoWay });
            var name = new TextBlock { Text = e.FolderName, Classes = { "ltr" }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var owner = new TextBox { IsEnabled = e.LooksLikeProject, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Watermark = "مالک / متقاضی" };
            owner.Bind(TextBox.TextProperty, new Binding(nameof(LegacyEntry.Owner)) { Mode = BindingMode.TwoWay });
            var count = new TextBlock { Text = $"{Digits.Fa(e.FileCount)} فایل", VerticalAlignment = VerticalAlignment.Center };
            var note = UiBuild.Text(e.Note, "muted");
            Grid.SetColumn(name, 1); Grid.SetColumn(owner, 2); Grid.SetColumn(count, 3); Grid.SetColumn(note, 4);
            g.Children.Add(check); g.Children.Add(name); g.Children.Add(owner); g.Children.Add(count); g.Children.Add(note);
            return g;
        });
        var inPlace = new RadioButton { Content = "فقط در فهرست ثبت شود؛ فایل‌ها همان‌جا می‌مانند (پیشنهادی)", IsChecked = _mode == LegacyMode.RegisterInPlace, GroupName = "mode" };
        var copy = new RadioButton { Content = "کپی در ساختار استاندارد؛ فقط پوشه‌هایی که فایل دارند ساخته می‌شوند و پوشه قدیمی دست‌نخورده می‌ماند", IsChecked = _mode == LegacyMode.CopyIntoStandard, GroupName = "mode" };
        inPlace.IsCheckedChanged += (_, _) => { if (inPlace.IsChecked == true) _mode = LegacyMode.RegisterInPlace; };
        copy.IsCheckedChanged += (_, _) => { if (copy.IsChecked == true) _mode = LegacyMode.CopyIntoStandard; };
        var go = UiBuild.Btn("ثبت پروژه‌های انتخاب‌شده", () => _ = ImportAsync(), primary: true);
        var back = UiBuild.Btn("بازگشت", () => ShowStep(1));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(go);
        buttons.Children.Add(back);

        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(rows);
        sp.Children.Add(UiBuild.Text("روش ثبت", "muted"));
        sp.Children.Add(inPlace);
        sp.Children.Add(copy);
        sp.Children.Add(buttons);
        return Frame(2, $"{Digits.Fa(found)} پروژه پیدا شد" + (_entries.Count > found ? $" ({Digits.Fa(_entries.Count - found)} پوشه پروژه به نظر نمی‌رسد)" : ""), sp);
    }

    public async Task ImportAsync()
    {
        var selected = _entries.Where(e => e.Selected).ToList();
        if (selected.Count == 0) { _st.Toast("پروژه‌ای انتخاب نشده است."); return; }
        try
        {
            var created = await Task.Run(() => _st.W.ImportLegacy(selected, _mode, Jalali.Today().Year, _st.Cfg.Crs));
            _importedCount = created.Count;
            _st.InvalidateIndex();
            ShowStep(3);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (TopLevel.GetTopLevel(this) is Window w) await MessageDialog.ShowAsync(w, "خطا در ورود پروژه‌ها", e.Message);
        }
    }

    private Control Step3()
    {
        var sp = new StackPanel { Spacing = 10 };
        sp.Children.Add(new TextBlock { Text = $"{Digits.Fa(_importedCount)} پروژه به فهرست اضافه شد", FontWeight = FontWeight.Bold });
        sp.Children.Add(UiBuild.Text("مالک و پلاک را بعداً می‌توانید با «ویرایش» کامل کنید. متقاضی هر پروژه از روی نام مالک ساخته یا وصل شد.", "muted", wrap: true));
        sp.Children.Add(UiBuild.Btn("دیدن فهرست پروژه‌ها", () => _st.Show("projects", null), primary: true));
        return Frame(3, "تمام شد", sp);
    }
}
