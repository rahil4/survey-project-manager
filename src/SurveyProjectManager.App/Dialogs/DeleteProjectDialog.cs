using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>تأیید حذف پروژه؛ کاربر هر بار انتخاب می‌کند فقط از فهرست حذف شود یا پوشه هم به سطل بازیافت برود.</summary>
public sealed class DeleteProjectDialog : Window
{
    public RadioButton ListOnly { get; } = new() { GroupName = "del", IsChecked = true };
    public RadioButton WithFolder { get; } = new() { GroupName = "del" };
    private readonly Button _ok;

    public bool Accepted { get; private set; }
    public bool DeleteFolder => WithFolder.IsChecked == true;

    public DeleteProjectDialog(AppState st, Project p)
    {
        Title = "حذف پروژه";
        FlowDirection = FlowDirection.RightToLeft;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var dir = p.FullPath(st.Root);
        var exists = Directory.Exists(dir);
        var (files, bytes, capped) = Workspace.FolderStats(dir);
        var related = st.W.RelatedTo(p).Count;

        var root = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        root.Children.Add(new TextBlock { Text = $"پروژه {p.Number} — {p.Owner}", FontWeight = FontWeight.Bold, FontSize = 16, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(UiBuild.Text($"{p.Location} · {p.Type}", "muted", wrap: true));
        var stats = exists
            ? $"پوشه پروژه: {Digits.Fa(files)}{(capped ? "+" : "")} فایل · {Digits.Fa(FileRow.FormatSize(bytes))}"
            : "پوشه این پروژه روی دیسک پیدا نشد.";
        root.Children.Add(new TextBlock { Text = stats, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(new TextBlock { Text = dir, Classes = { "ltr", "muted" }, TextWrapping = TextWrapping.Wrap });
        if (!Path.GetFullPath(dir).StartsWith(Path.GetFullPath(st.Root), StringComparison.OrdinalIgnoreCase))
            root.Children.Add(UiBuild.Text("این پروژه در پوشه‌ای خارج از پوشه اصلی ثبت شده است.", "muted", wrap: true));
        if (related > 0)
            root.Children.Add(UiBuild.Text($"{Digits.Fa(related)} پروژه مرتبط (همین ملک) دست‌نخورده می‌ماند و فقط لینک آن‌ها به این پروژه برداشته می‌شود.", "muted", wrap: true));

        ListOnly.Content = new TextBlock { Text = "فقط از فهرست حذف شود؛ پوشه و فایل‌ها روی دیسک دست‌نخورده می‌مانند (بعداً با «ورود پوشه‌های موجود» دوباره ثبت می‌شود)", TextWrapping = TextWrapping.Wrap };
        WithFolder.Content = new TextBlock
        {
            Text = RecycleBin.IsSupported ? "پوشه پروژه هم به سطل بازیافت ویندوز برود (تا وقتی سطل را خالی نکنید قابل بازگردانی است)" : "انتقال پوشه به سطل بازیافت فقط در ویندوز ممکن است",
            TextWrapping = TextWrapping.Wrap,
        };
        WithFolder.IsEnabled = RecycleBin.IsSupported && exists && Workspace.IsSafeToRecycle(st.Root, dir);
        root.Children.Add(ListOnly);
        root.Children.Add(WithFolder);

        _ok = UiBuild.Btn("حذف", Confirm);
        _ok.Classes.Add("danger");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(_ok);
        buttons.Children.Add(UiBuild.Btn("انصراف", Close));
        root.Children.Add(buttons);
        Content = root;
        KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) Close(); };
    }

    public void Confirm()
    {
        Accepted = true;
        Close();
    }
}
