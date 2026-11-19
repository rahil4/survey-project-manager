using FlowDirection = Avalonia.Media.FlowDirection;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.App.Controls;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>پنجره «افزودن فایل»: برای هر فایل پوشه مقصد پیشنهاد می‌شود و کاربر می‌تواند آن را عوض کند.</summary>
public sealed class ImportDialog : Window
{
    private readonly string _projectDir;
    private readonly TextBlock _summary = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _go;

    public List<ImportRow> Rows { get; }
    public ImportResult? Result { get; private set; }

    public ImportDialog(string projectDir, string projectLabel, IEnumerable<string> sources, string? fallbackFolder = null)
    {
        _projectDir = projectDir;
        Rows = ImportPlanner.Plan(projectDir, sources).Select(i => new ImportRow(projectDir, i)).ToList();
        // فایلی که نوعش مشخص نیست، به پوشه‌ای که کاربر داخل آن است می‌رود
        if (fallbackFolder != null)
            foreach (var r in Rows.Where(r => r.Item.Folder == null))
                r.SelectedChoice = r.Choices.FirstOrDefault(c => c.Value == fallbackFolder);
        Title = "افزودن فایل";
        FlowDirection = FlowDirection.RightToLeft;
        Width = 900;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 640;
        MinWidth = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var list = new ItemsControl { ItemsSource = Rows };
        list.ItemTemplate = new FuncDataTemplate<ImportRow>((row, _) => BuildRow(row));
        _go = UiBuild.Btn("", Execute, primary: true);
        foreach (var r in Rows) r.PropertyChanged += OnRowChanged;

        var header = new TextBlock { Text = $"{Digits.Fa(Rows.Count)} فایل برای {projectLabel}", FontSize = 16, FontWeight = FontWeight.Bold, Margin = new Thickness(18, 14) };
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,8,Auto"), Margin = new Thickness(18, 10) };
        footer.Children.Add(_summary);
        Grid.SetColumn(_go, 1);
        footer.Children.Add(_go);
        var cancel = UiBuild.Btn("انصراف", Close);
        Grid.SetColumn(cancel, 3);
        footer.Children.Add(cancel);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(header);
        root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = list, Padding = new Thickness(18, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = root;
        UpdateSummary();
    }

    private Control BuildRow(ImportRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("32,260,240,*"), Margin = new Thickness(0, 4), MinHeight = 40 };
        var check = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        check.Bind(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding(nameof(ImportRow.IsSelected)) { Mode = BindingMode.TwoWay });
        var icon = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        icon.Children.Add(new KindIcon { Kind = row.Item.IsDirectory ? "folder" : KindIcon.KindOf(row.Original) });
        var name = new TextBlock { FlowDirection = FlowDirection.LeftToRight, FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"), FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        name.Bind(TextBlock.TextProperty, new Binding(nameof(ImportRow.Name)));
        ToolTip.SetTip(name, row.Original);
        icon.Children.Add(name);
        var combo = new ComboBox { ItemsSource = row.Choices, PlaceholderText = "انتخاب کنید…", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(8, 0), FlowDirection = FlowDirection.LeftToRight };
        combo.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedItemProperty, new Binding(nameof(ImportRow.SelectedChoice)) { Mode = BindingMode.TwoWay });
        var note = new TextBlock { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        note.Bind(TextBlock.TextProperty, new Binding(nameof(ImportRow.Note)));
        note.Bind(TextBlock.ForegroundProperty, new Binding(nameof(ImportRow.NoteTone)) { Converter = NoteToneConverter.Instance });
        Grid.SetColumn(icon, 1); Grid.SetColumn(combo, 2); Grid.SetColumn(note, 3);
        grid.Children.Add(check); grid.Children.Add(icon); grid.Children.Add(combo); grid.Children.Add(note);
        return grid;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => UpdateSummary();

    private void UpdateSummary()
    {
        var chosen = Rows.Where(r => r.IsSelected && r.Item.Folder != null && !r.Item.Conflict).ToList();
        var newFolders = chosen.Select(r => r.Item.Folder!).Where(f => !Directory.Exists(Path.Combine(_projectDir, f))).Distinct().Count();
        var ask = Rows.Count(r => r.Item.Folder == null);
        var parts = new List<string> { "فایل‌ها کپی می‌شوند؛ نسخه اصلی دست‌نخورده می‌ماند." };
        if (newFolders > 0) parts.Add($"{Digits.Fa(newFolders)} پوشه جدید ساخته می‌شود.");
        if (ask > 0) parts.Add($"{Digits.Fa(ask)} فایل هنوز پوشه ندارد.");
        _summary.Text = string.Join(" ", parts);
        _go.Content = $"کپی {Digits.Fa(chosen.Count)} فایل";
        _go.IsEnabled = chosen.Count > 0;
    }

    public void Execute()
    {
        Result = ImportPlanner.Execute(_projectDir, Rows.Select(r => r.Item));
        Close();
    }
}

internal sealed class NoteToneConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly NoteToneConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var key = value switch { "reg" => "StRegFg", "field" => "StFieldFg", _ => "MutedBrush" };
        return Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var b) ? b : Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
