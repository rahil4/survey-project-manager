using FlowDirection = Avalonia.Media.FlowDirection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

public sealed record BackupRow(string Date, string New, string Updated, string Size);

/// <summary>پشتیبان‌گیری افزایشی روی هارد یا پوشه دیگر.</summary>
public sealed class BackupView : UserControl
{
    private readonly AppState _st;
    private readonly TextBox _dest = new() { IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, FontSize = 12.5, Watermark = "مقصد پشتیبان انتخاب نشده است" };
    private readonly ComboBox _remind = new() { Width = 170 };
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 1, Height = 8 };
    private readonly Button _start;
    private readonly DataGrid _history = new() { IsReadOnly = true, AutoGenerateColumns = false, RowHeight = 34, MinHeight = 160, CanUserSortColumns = false };
    private bool _running;

    public BackupView(AppState st)
    {
        _st = st;
        _dest.Text = st.Cfg.BackupDest;
        foreach (var t in new[] { "یادآوری هر ۷ روز", "یادآوری هر ۳ روز", "بدون یادآوری" }) _remind.Items.Add(t);
        _remind.SelectedIndex = st.Cfg.BackupRemindDays switch { 3 => 1, 0 => 2, _ => 0 };
        _remind.SelectionChanged += (_, _) =>
        {
            st.Cfg.BackupRemindDays = _remind.SelectedIndex switch { 1 => 3, 2 => 0, _ => 7 };
            st.SaveConfig();
        };
        _start = UiBuild.Btn("شروع پشتیبان‌گیری", () => _ = RunAsync(), primary: true);

        _history.Columns.Add(new DataGridTextColumn { Header = "تاریخ", Binding = new Binding(nameof(BackupRow.Date)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _history.Columns.Add(new DataGridTextColumn { Header = "فایل جدید", Binding = new Binding(nameof(BackupRow.New)), Width = new DataGridLength(110) });
        _history.Columns.Add(new DataGridTextColumn { Header = "تغییرکرده", Binding = new Binding(nameof(BackupRow.Updated)), Width = new DataGridLength(110) });
        _history.Columns.Add(new DataGridTextColumn { Header = "حجم", Binding = new Binding(nameof(BackupRow.Size)), Width = new DataGridLength(110) });

        var left = new StackPanel { Spacing = 10 };
        left.Children.Add(UiBuild.Text("مقصد", "h2"));
        var pick = UiBuild.Btn("تغییر مقصد…", () => _ = PickAsync());
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto") };
        row.Children.Add(_dest);
        Grid.SetColumn(pick, 2);
        row.Children.Add(pick);
        left.Children.Add(row);
        left.Children.Add(_remind);
        left.Children.Add(_status);
        left.Children.Add(_bar);
        left.Children.Add(_start);

        var right = new StackPanel { Spacing = 10 };
        right.Children.Add(UiBuild.Text("سابقه", "h2"));
        right.Children.Add(_history);

        var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("1.2*,16,*") };
        cards.Children.Add(UiBuild.Card(left));
        var rc = UiBuild.Card(right);
        Grid.SetColumn(rc, 2);
        cards.Children.Add(rc);

        var root = new StackPanel { Spacing = 8, Margin = new Thickness(24, 20) };
        root.Children.Add(UiBuild.Text("پشتیبان‌گیری", "h1"));
        var sub = UiBuild.Text("فقط فایل‌های جدید و تغییرکرده کپی می‌شوند؛ پشتیبان قبلی دست‌نخورده می‌ماند و چیزی پاک نمی‌شود.", "muted");
        sub.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(sub);
        root.Children.Add(cards);
        Content = new ScrollViewer { Content = root };
        ShowIdle();
        LoadHistory();
    }

    private void ShowIdle()
    {
        var cfg = _st.Cfg;
        _bar.Value = 1;
        _status.Text = cfg.BackupDest.Length == 0
            ? "ابتدا مقصد پشتیبان (هارد اکسترنال یا پوشه ابری) را انتخاب کنید."
            : DateTime.TryParse(cfg.LastBackup, out var at)
                ? $"آخرین پشتیبان: {Digits.Fa(Jalali.Format(at))} {Digits.Fa(at.ToString("HH:mm"))}"
                : "هنوز پشتیبان‌گیری نشده است.";
        _start.IsEnabled = cfg.BackupDest.Length > 0 && !_running;
    }

    private void LoadHistory()
    {
        var dest = _st.Cfg.BackupDest;
        var rows = dest.Length > 0 && Directory.Exists(dest)
            ? BackupService.History(dest).Select(h => new BackupRow(
                Digits.Fa(Jalali.Format(h.At) + " " + h.At.ToString("HH:mm")), Digits.Fa(h.NewFiles), Digits.Fa(h.Updated), Digits.Fa(FileRow.FormatSize(h.Bytes)))).ToList()
            : new List<BackupRow>();
        _history.ItemsSource = rows;
    }

    private async Task PickAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "مقصد پشتیبان", AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;
        try { BackupService.Validate(_st.Root, path); }
        catch (ArgumentException e) { await MessageDialog.ShowAsync(owner, "مقصد نامعتبر", e.Message); return; }
        _st.Cfg.BackupDest = path;
        _st.SaveConfig();
        _dest.Text = path;
        ShowIdle();
        LoadHistory();
    }

    public async Task RunAsync()
    {
        if (_running || TopLevel.GetTopLevel(this) is not Window owner) return;
        _running = true;
        _start.IsEnabled = false;
        _bar.Value = 0;
        var progress = new Progress<(int Done, int Total)>(p =>
        {
            _bar.Value = p.Total == 0 ? 1 : (double)p.Done / p.Total;
            _status.Text = $"در حال کپی… {Digits.Fa(p.Done)} از {Digits.Fa(p.Total)} فایل";
        });
        try
        {
            var root = _st.Root;
            var dest = _st.Cfg.BackupDest;
            var r = await Task.Run(() => BackupService.Run(root, dest, progress));
            _st.Cfg.LastBackup = r.At.ToString("yyyy-MM-dd HH:mm");
            _st.SaveConfig();
            _status.Text = $"پشتیبان‌گیری کامل شد · {Digits.Fa(r.NewFiles)} فایل جدید · {Digits.Fa(r.Updated)} تغییرکرده · {Digits.Fa(FileRow.FormatSize(r.Bytes))}";
            _bar.Value = 1;
            _st.Toast("پشتیبان‌گیری کامل شد");
            LoadHistory();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = "پشتیبان‌گیری کامل نشد.";
            await MessageDialog.ShowAsync(owner, "خطا در پشتیبان‌گیری", e.Message);
        }
        finally
        {
            _running = false;
            _start.IsEnabled = _st.Cfg.BackupDest.Length > 0;
        }
    }
}
