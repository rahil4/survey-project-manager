using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>پنجره ساخت پروژه جدید و ویرایش مشخصات پروژه.</summary>
public sealed class ProjectDialog : Window
{
    public TextBox OwnerBox { get; } = new();
    public TextBox Location { get; } = new() { Watermark = "مثلاً: کرج، پلاک ۸۸۱۲ اصلی" };
    public ComboBox TypeBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public ComboBox StatusBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public TextBox Due { get; } = new() { Watermark = "۱۴۰۵/۰۸/۱۵" };
    public TextBox Note { get; } = new();
    public TextBox Slug { get; } = new() { FlowDirection = Avalonia.Media.FlowDirection.LeftToRight };
    public TextBlock Preview { get; } = new() { FlowDirection = Avalonia.Media.FlowDirection.LeftToRight, Foreground = Avalonia.Media.Brushes.Gray, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public Button OkButton { get; } = new() { Content = "ثبت", Classes = { "primary" }, MinWidth = 90 };

    private readonly string _number;
    private readonly string _rootText;
    private readonly bool _editing;
    private bool _slugEdited;
    private bool _settingSlug;

    /// <summary>true اگر کاربر «ثبت» را زده باشد.</summary>
    public bool Accepted { get; private set; }

    public ProjectDialog(string number, string rootText, Project? project = null)
    {
        _number = number;
        _rootText = rootText;
        _editing = project != null;
        Title = _editing ? "ویرایش پروژه" : "پروژه جدید";
        FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        foreach (var t in Statuses.ProjectTypes) TypeBox.Items.Add(t);
        foreach (var (_, label) in Statuses.All) StatusBox.Items.Add(label);
        TypeBox.SelectedIndex = 0;
        StatusBox.SelectedIndex = 0;
        if (project != null)
        {
            OwnerBox.Text = project.Owner;
            Location.Text = project.Location;
            if (!TypeBox.Items.Contains(project.Type)) TypeBox.Items.Add(project.Type);
            TypeBox.SelectedItem = project.Type;
            StatusBox.SelectedIndex = Math.Max(0, Statuses.All.ToList().FindIndex(s => s.Key == project.Status));
            Due.Text = project.Due;
            Note.Text = project.Note;
        }

        var form = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), RowSpacing = 8 };
        void Row(string label, Control input)
        {
            form.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var r = form.RowDefinitions.Count - 1;
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, r);
            Grid.SetRow(input, r);
            Grid.SetColumn(input, 1);
            form.Children.Add(l);
            form.Children.Add(input);
        }
        Row("شماره پروژه", new TextBlock { Text = number, FlowDirection = Avalonia.Media.FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Right, FontWeight = Avalonia.Media.FontWeight.Bold });
        Row("نام مالک", OwnerBox);
        Row("موقعیت / پلاک", Location);
        Row("نوع کار", TypeBox);
        Row("وضعیت", StatusBox);
        Row("تاریخ تحویل", Due);
        Row("یادداشت", Note);
        if (!_editing) Row("نام لاتین پوشه", Slug);

        var cancel = new Button { Content = "انصراف", MinWidth = 90 };
        cancel.Click += (_, _) => Close();
        OkButton.Click += (_, _) => Submit();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { OkButton, cancel } };

        var root = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        root.Children.Add(form);
        if (!_editing)
        {
            root.Children.Add(Preview);
            root.Children.Add(new TextBlock
            {
                Text = "زیرپوشه‌ها: " + string.Join("  ", ProjectFolders.Subfolders.Select(s => s.Name)),
                FlowDirection = Avalonia.Media.FlowDirection.LeftToRight, FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            OnTextChanged(OwnerBox, AutoSlug);
            OnTextChanged(Location, AutoSlug);
            OnTextChanged(Slug, () => { if (!_settingSlug) _slugEdited = true; UpdatePreview(); });
            AutoSlug();
        }
        root.Children.Add(buttons);
        Content = root;
        Opened += (_, _) => OwnerBox.Focus();
    }

    private static void OnTextChanged(TextBox box, Action action) =>
        box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) action(); };

    private void AutoSlug()
    {
        if (!_slugEdited)
        {
            _settingSlug = true;
            Slug.Text = Translit.FolderSlug(OwnerBox.Text ?? "", Location.Text ?? "");
            _settingSlug = false;
        }
        UpdatePreview();
    }

    private void UpdatePreview() =>
        Preview.Text = Path.Combine(_rootText, ProjectFolders.RelativePath(_number, SlugText()).Replace('/', Path.DirectorySeparatorChar));

    public string SlugText() => Translit.SanitizeSlug(Slug.Text ?? "");

    private void Submit()
    {
        if (string.IsNullOrWhiteSpace(OwnerBox.Text))
        {
            OwnerBox.BorderBrush = Avalonia.Media.Brushes.Firebrick;
            OwnerBox.Focus();
            return;
        }
        Accepted = true;
        Close();
    }

    public void ApplyTo(Project p)
    {
        p.Owner = (OwnerBox.Text ?? "").Trim();
        p.Location = (Location.Text ?? "").Trim();
        p.Type = TypeBox.SelectedItem as string ?? "";
        p.Status = Statuses.All[Math.Max(0, StatusBox.SelectedIndex)].Key;
        p.Due = (Due.Text ?? "").Trim();
        p.Note = (Note.Text ?? "").Trim();
    }
}
