using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SurveyProjectManager.App;

/// <summary>ساخت سریع اجزای رابط در کد.</summary>
public static class UiBuild
{
    public static TextBlock Text(string text, string? cls = null, bool wrap = false, bool trim = false)
    {
        var t = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (trim) t.TextTrimming = TextTrimming.CharacterEllipsis;
        if (cls != null) foreach (var c in cls.Split(' ')) t.Classes.Add(c);
        if (wrap) t.TextWrapping = TextWrapping.Wrap;
        return t;
    }

    public static Button Btn(string text, Action onClick, bool primary = false)
    {
        var b = new Button { Content = text, MinHeight = 32, Padding = new Thickness(14, 4) };
        if (primary) b.Classes.Add("pri");
        b.Click += (_, _) => onClick();
        return b;
    }

    public static StackPanel Labeled(string label, Control input)
    {
        var p = new StackPanel { Spacing = 3 };
        p.Children.Add(Text(label, "muted"));
        p.Children.Add(input);
        return p;
    }

    public static Border Card(Control child, Thickness? padding = null)
    {
        var b = new Border { Child = child };
        b.Classes.Add("card");
        if (padding != null) b.Padding = padding.Value;
        return b;
    }

    public static Border KindPill(string text)
    {
        var b = new Border { Child = new TextBlock { Text = text } };
        b.Classes.Add("kind");
        return b;
    }

    /// <summary>ویژگی را به یک منبع تم (روشن/تیره) وصل می‌کند تا با عوض شدن تم خودکار تغییر کند.</summary>
    public static void BindRes(Control c, AvaloniaProperty p, string key) => c.Bind(p, c.GetResourceObservable(key));

    public static Border Badge(string text, string tone)
    {
        var b = new Controls.Badge { Text = text, Tone = tone };
        return b;
    }

    public static IBrush Resource(Control c, string key) =>
        c.TryFindResource(key, c.ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Gray;
}
