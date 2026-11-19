using FlowDirection = Avalonia.Media.FlowDirection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SurveyProjectManager.App.Controls;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Views;

/// <summary>پیش‌نمایش فایل: عکس، PDF (صفحه اول)، جدول نقاط، متن فایل خام و اطلاعات فایل‌های دیگر.</summary>
public static class PreviewBuilder
{
    private const long MaxImageBytes = 40L * 1024 * 1024;
    private const long MaxTextBytes = 8L * 1024 * 1024;

    public static Control ForFolder(Project p, string dir, Workspace w)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = Path.GetFileName(dir), Classes = { "ltr" }, FontWeight = FontWeight.Bold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var kv = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12, RowSpacing = 2 };
        void Row(string k, Control v)
        {
            kv.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var key = UiBuild.Text(k, "muted");
            Grid.SetRow(key, kv.RowDefinitions.Count - 1);
            Grid.SetRow(v, kv.RowDefinitions.Count - 1);
            Grid.SetColumn(v, 1);
            kv.Children.Add(key);
            kv.Children.Add(v);
        }
        var client = w.ClientOf(p);
        Row("متقاضی", new TextBlock { Text = client?.Name ?? "—", FontSize = 12.5 });
        Row("مالک", new TextBlock { Text = p.Owner, FontSize = 12.5 });
        Row("موقعیت", new TextBlock { Text = p.Location, FontSize = 12.5, TextWrapping = TextWrapping.Wrap });
        Row("نوع کار", new TextBlock { Text = p.Type, FontSize = 12.5 });
        Row("وضعیت", new Badge { Text = p.StatusLabel, Tone = p.Status, HorizontalAlignment = HorizontalAlignment.Left });
        Row("تحویل", new TextBlock { Text = string.IsNullOrEmpty(p.Due) ? "—" : Digits.Fa(p.Due), FontSize = 12.5 });
        panel.Children.Add(kv);
        panel.Children.Add(UiBuild.Text("یک فایل را انتخاب کنید تا پیش‌نمایش آن اینجا دیده شود.", "muted", wrap: true));
        return new ScrollViewer { Content = panel };
    }

    public static Control ForFile(string path, Action<string> open)
    {
        var info = new FileInfo(path);
        var ext = info.Extension.TrimStart('.').ToLowerInvariant();
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = info.Name, Classes = { "ltr" }, FontWeight = FontWeight.Bold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        var body = new ContentControl();
        panel.Children.Add(body);

        switch (KindIcon.KindOf(path))
        {
            case "img" when info.Length <= MaxImageBytes && ext is "jpg" or "jpeg" or "png" or "bmp" or "gif" or "webp":
                body.Content = LoadingImage(() => LoadBitmap(path), "عکس باز نشد.", 480);
                break;
            case "pdf" when info.Length <= MaxImageBytes:
                body.Content = LoadingImage(() => RenderPdf(path), "پیش‌نمایش این PDF ساخته نشد؛ با «بازکردن» ببینید.", 480, paper: true);
                break;
            case "csv":
                body.Content = TableOrText(path, info) ?? Fallback(path, open, "فایل متنی یا جدول");
                break;
            case "raw":
                body.Content = TextHead(path, info, 8) ?? Fallback(path, open, "فایل خام دستگاه (باینری)");
                break;
            case "cad":
                body.Content = Fallback(path, open, "پیش‌نمایش " + ext.ToUpperInvariant() + " در برنامه ممکن نیست؛ با اتوکد باز می‌شود");
                break;
            default:
                body.Content = Fallback(path, open, "پیش‌نمایش این نوع فایل در برنامه ممکن نیست");
                break;
        }

        var kv = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        void Row(string k, string v)
        {
            kv.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var key = UiBuild.Text(k, "muted");
            var val = new TextBlock { Text = v, FontSize = 12.5 };
            Grid.SetRow(key, kv.RowDefinitions.Count - 1); Grid.SetRow(val, kv.RowDefinitions.Count - 1); Grid.SetColumn(val, 1);
            kv.Children.Add(key); kv.Children.Add(val);
        }
        Row("حجم", Digits.Fa(FileRow.FormatSize(info.Length)));
        Row("تغییر", Digits.Fa(Jalali.Format(info.LastWriteTime)));
        panel.Children.Add(kv);
        if (info.Attributes.HasFlag(FileAttributes.ReadOnly)) panel.Children.Add(UiBuild.Text("فقط‌خواندنی (فایل خام محافظت‌شده)", "muted"));
        return new ScrollViewer { Content = panel };
    }

    private static Control Fallback(string path, Action<string> open, string message)
    {
        var sp = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12) };
        sp.Children.Add(new KindIcon { Kind = KindIcon.KindOf(path), Width = 44, Height = 56, HorizontalAlignment = HorizontalAlignment.Center });
        sp.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontWeight = FontWeight.Medium });
        sp.Children.Add(UiBuild.Btn("بازکردن", () => open(path), primary: true));
        return UiBuild.Card(sp);
    }

    private static Control LoadingImage(Func<Bitmap?> load, string failText, int width, bool paper = false)
    {
        var holder = new Border { MinHeight = 80 };
        if (paper)
        {
            holder.Padding = new Thickness(6);
            UiBuild.BindRes(holder, Border.BackgroundProperty, "PaperBrush");
            holder.BoxShadow = BoxShadows.Parse("0 2 8 0 #40000000");
        }
        var status = UiBuild.Text("در حال بارگذاری…", "muted");
        holder.Child = status;
        _ = Task.Run(() =>
        {
            try { return load(); } catch (Exception) { return null; }
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            var bmp = t.IsCompletedSuccessfully ? t.Result : null;
            holder.Child = bmp != null ? new Image { Source = bmp, Stretch = Stretch.Uniform, MaxWidth = width } : UiBuild.Text(failText, "muted", wrap: true);
        }));
        return holder;
    }

    private static Bitmap? LoadBitmap(string path)
    {
        using var fs = File.OpenRead(path);
        return Bitmap.DecodeToWidth(fs, 480);
    }

    /// <summary>صفحه اول PDF را با PDFium به تصویر تبدیل می‌کند.</summary>
    public static Bitmap? RenderPdf(string path)
    {
        using var fs = File.OpenRead(path);
        using var skBitmap = PDFtoImage.Conversion.ToImage(fs, page: 0, options: new PDFtoImage.RenderOptions(Width: 480));
        using var data = skBitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var ms = new MemoryStream(data.ToArray());
        return new Bitmap(ms);
    }

    private static Control? TextHead(string path, FileInfo info, int lines)
    {
        var head = ReadHead(path, info, lines + 1);
        if (head == null) return null;
        var shown = head.Take(lines).ToList();
        var tb = new TextBlock { Text = string.Join("\n", shown), Classes = { "ltr" }, FontSize = 11.5, TextWrapping = TextWrapping.NoWrap };
        var holder = new ScrollViewer { Content = tb, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, MaxHeight = 220 };
        return UiBuild.Card(holder, new Thickness(10, 8));
    }

    /// <summary>چند خط اول فایل متنی؛ برای فایل باینری یا بزرگ null.</summary>
    private static List<string>? ReadHead(string path, FileInfo info, int lines)
    {
        if (info.Length > MaxTextBytes * 8) return null;
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[(int)Math.Min(info.Length, 64 * 1024)];
            var n = fs.Read(buf, 0, buf.Length);
            if (buf.AsSpan(0, n).Contains((byte)0)) return null;
            var text = Encoding.UTF8.GetString(buf, 0, n);
            return text.Split('\n').Select(l => l.TrimEnd('\r')).Take(lines).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    private static Control? TableOrText(string path, FileInfo info)
    {
        var head = ReadHead(path, info, 40);
        if (head == null) return null;
        var delim = new[] { ',', ';', '\t' }.OrderByDescending(d => head.Take(5).Sum(l => l.Count(c => c == d))).First();
        var rows = head.Where(l => l.Length > 0).Select(l => l.Split(delim)).ToList();
        if (rows.Count < 2 || rows.Max(r => r.Length) < 2 || info.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) && delim != '\t' && delim != ',')
            return TextHead(path, info, 12);
        var cols = Math.Min(5, rows.Max(r => r.Length));
        var grid = new Grid { FlowDirection = FlowDirection.LeftToRight, ColumnSpacing = 10 };
        for (var c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var shown = rows.Take(8).ToList();
        for (var r = 0; r < shown.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < cols; c++)
            {
                var cell = new TextBlock { Text = c < shown[r].Length ? shown[r][c].Trim() : "", FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right };
                if (r == 0) cell.FontWeight = FontWeight.Bold;
                Grid.SetRow(cell, r); Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        sp.Children.Add(UiBuild.Text($"{Digits.Fa(Math.Min(8, rows.Count) - 1)} ردیف اول", "muted"));
        return UiBuild.Card(sp, new Thickness(10, 8));
    }
}
