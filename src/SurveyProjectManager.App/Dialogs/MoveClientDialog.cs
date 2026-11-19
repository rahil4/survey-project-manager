using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>انتقال همه پروژه‌های یک متقاضی به متقاضی دیگر (برای ادغام دو متقاضی تکراری).</summary>
public sealed class MoveClientDialog : Window
{
    private readonly List<Client> _others;
    public ComboBox Target { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public bool Accepted { get; private set; }
    public Client? SelectedTarget => Target.SelectedIndex >= 0 && Target.SelectedIndex < _others.Count ? _others[Target.SelectedIndex] : null;

    public MoveClientDialog(AppState st, Client from)
    {
        Title = "انتقال پروژه‌ها";
        FlowDirection = FlowDirection.RightToLeft;
        Width = 480;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        _others = st.W.Clients.Where(c => c.Id != from.Id).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        foreach (var c in _others) Target.Items.Add($"{c.Name} ({Digits.Fa(st.W.ProjectsOf(c.Id).Count())} پروژه)");
        Target.SelectedIndex = 0;
        var n = st.W.ProjectsOf(from.Id).Count();

        var root = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        root.Children.Add(new TextBlock { Text = $"انتقال {Digits.Fa(n)} پروژه از «{from.Name}»", FontWeight = FontWeight.Bold, FontSize = 15, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(UiBuild.Labeled("به این متقاضی منتقل شود", Target));
        root.Children.Add(UiBuild.Text("فقط متقاضی پروژه‌ها عوض می‌شود؛ مالک ملک، پوشه‌ها و فایل‌ها تغییری نمی‌کنند. بعد از انتقال می‌توانید متقاضی قبلی را حذف کنید.", "muted", wrap: true));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(UiBuild.Btn("انتقال", () => { Accepted = true; Close(); }, primary: true));
        buttons.Children.Add(UiBuild.Btn("انصراف", Close));
        root.Children.Add(buttons);
        Content = root;
    }
}
