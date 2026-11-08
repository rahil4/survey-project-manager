using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>پیام ساده با دکمه تأیید، یا پرسش بله/خیر.</summary>
public sealed class MessageDialog : Window
{
    private MessageDialog(string title, string text, bool question)
    {
        Title = title;
        FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 360;
        MaxWidth = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var ok = new Button { Content = question ? "بله" : "تأیید", Classes = { "primary" }, MinWidth = 80 };
        ok.Click += (_, _) => Close(true);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
        buttons.Children.Add(ok);
        if (question)
        {
            var no = new Button { Content = "خیر", MinWidth = 80 };
            no.Click += (_, _) => Close(false);
            buttons.Children.Add(no);
        }
        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children = { new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons },
        };
    }

    public static Task ShowAsync(Window owner, string title, string text) =>
        new MessageDialog(title, text, false).ShowDialog<bool>(owner);

    public static async Task<bool> AskAsync(Window owner, string title, string text) =>
        await new MessageDialog(title, text, true).ShowDialog<bool>(owner);
}
