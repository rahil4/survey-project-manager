using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace SurveyProjectManager.App.Controls;

/// <summary>آیکن کوچک پوشه یا فایل با رنگ نوع فایل (اتوکد، PDF، عکس، ...).</summary>
public sealed class KindIcon : Border
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<KindIcon, string>(nameof(Kind), "file");
    public static readonly StyledProperty<bool> GhostProperty = AvaloniaProperty.Register<KindIcon, bool>(nameof(Ghost));

    public KindIcon()
    {
        VerticalAlignment = VerticalAlignment.Center;
        CornerRadius = new CornerRadius(2);
        Apply();
    }

    public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public bool Ghost { get => GetValue(GhostProperty); set => SetValue(GhostProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty || change.Property == GhostProperty) Apply();
    }

    private void Apply()
    {
        var folder = Kind is "folder" or "lock";
        Width = folder ? 14 : 11;
        Height = folder ? 11 : 14;
        var key = Kind switch
        {
            "folder" => "KFolder", "lock" => "KLock", "cad" => "KCad", "pdf" => "KPdf", "img" => "KImg", "csv" => "KCsv", "raw" => "KRaw", _ => "KFile",
        };
        if (Ghost)
        {
            Background = Avalonia.Media.Brushes.Transparent;
            BorderThickness = new Thickness(1.5);
            this.Bind(BorderBrushProperty, this.GetResourceObservable("MutedBrush"));
        }
        else
        {
            BorderThickness = new Thickness(0);
            this.Bind(BackgroundProperty, this.GetResourceObservable(key));
        }
    }

    public static string KindOf(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "dwg" or "dxf" or "dwt" or "bak" => "cad",
            "pdf" => "pdf",
            "jpg" or "jpeg" or "png" or "tif" or "tiff" or "heic" or "bmp" or "gif" => "img",
            "csv" or "xlsx" or "xls" or "txt" or "pts" or "ods" => "csv",
            "job" or "jxl" or "rw5" or "rtk" or "sdr" or "raw" or "gsi" or "rnx" => "raw",
            _ when ext.Length == 3 && char.IsDigit(ext[0]) && char.IsDigit(ext[1]) && "ongpl".Contains(char.ToLowerInvariant(ext[2])) => "raw",
            _ => "file",
        };
    }
}
