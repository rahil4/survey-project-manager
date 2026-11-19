using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SurveyProjectManager.App.Controls;

/// <summary>برچسب رنگی (وضعیت پروژه، موعد). رنگ‌ها از تم برنامه (روشن/تیره) خوانده می‌شوند.</summary>
public sealed class Badge : Border
{
    public static readonly StyledProperty<string> ToneProperty = AvaloniaProperty.Register<Badge, string>(nameof(Tone), "collect");
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<Badge, string>(nameof(Text), "");

    private readonly TextBlock _label = new() { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };

    public Badge()
    {
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(10, 1);
        VerticalAlignment = VerticalAlignment.Center;
        Child = _label;
        Rebind();
    }

    public string Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty) Rebind();
        else if (change.Property == TextProperty) _label.Text = Text;
    }

    private void Rebind()
    {
        var key = Tone switch
        {
            "field" => "StField", "draw" => "StDraw", "check" => "StCheck", "done" => "StDone", "reg" => "StReg", _ => "StCollect",
        };
        this.Bind(BackgroundProperty, this.GetResourceObservable(key + "Bg"));
        _label.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(key + "Fg"));
    }

    public static string ToneOfStatus(string status) => status;
}
