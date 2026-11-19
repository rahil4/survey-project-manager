using FlowDirection = Avalonia.Media.FlowDirection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App.Dialogs;

/// <summary>
/// ساخت پروژه جدید (انتخاب یا ساخت متقاضی، مالک ملک، ساخت از روی پروژه قبلی) و ویرایش مشخصات پروژه.
/// </summary>
public sealed class ProjectDialog : Window
{
    private readonly AppState _st;
    private readonly Project? _editing;
    private readonly string _number;

    public TextBox ClientBox { get; } = new() { Watermark = "نام را بنویسید؛ مثلاً احمدی" };
    public ListBox Suggestions { get; } = new() { IsVisible = false, MaxHeight = 140 };
    private readonly StackPanel _clientInfo = new() { Spacing = 6 };
    private readonly ComboBox _kind = new() { Width = 130 };
    private readonly TextBox _phone = new() { Width = 200, FlowDirection = FlowDirection.LeftToRight, Watermark = "تلفن (اختیاری)" };
    public CheckBox SameOwner { get; } = new() { Content = "مالک ملک همان متقاضی است", IsChecked = true };
    public TextBox OwnerBox { get; } = new() { Watermark = "مثلاً: خانم کریمی" };
    public TextBox Location { get; } = new() { Watermark = "کرج، پلاک ۸۸۱۲ اصلی" };
    public ComboBox TypeBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public ComboBox StatusBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public TextBox Due { get; } = new() { Watermark = "۱۴۰۵/۰۸/۱۵" };
    public TextBox Note { get; } = new();
    public TextBox Slug { get; } = new() { FlowDirection = FlowDirection.LeftToRight };
    public TextBlock Preview { get; } = new() { FlowDirection = FlowDirection.LeftToRight, TextWrapping = TextWrapping.Wrap };
    public ComboBox FromBox { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _docs = new() { Spacing = 2 };
    private readonly Border _docsCard;
    private readonly ComboBox _clientPicker = new() { HorizontalAlignment = HorizontalAlignment.Stretch };

    private Client? _selected;
    private bool _slugEdited, _settingSlug, _settingClientText;
    private List<Project> _previous = new();

    public bool Accepted { get; private set; }
    public NewProjectRequest? Request { get; private set; }

    /// <param name="editing">اگر null باشد پروژه جدید ساخته می‌شود.</param>
    public ProjectDialog(AppState st, Project? editing = null, string? presetClientId = null)
    {
        _st = st;
        _editing = editing;
        _number = editing?.Number ?? st.W.NextNumber(Jalali.Today().Year);
        Title = editing == null ? "پروژه جدید" : "ویرایش پروژه";
        FlowDirection = FlowDirection.RightToLeft;
        Width = 640;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        foreach (var t in Statuses.ProjectTypes) TypeBox.Items.Add(t);
        TypeBox.SelectedIndex = 0;
        foreach (var (_, label) in Statuses.All) StatusBox.Items.Add(label);
        StatusBox.SelectedIndex = 0;
        foreach (var k in Client.Kinds) _kind.Items.Add(k);
        _kind.SelectedIndex = 0;
        _docsCard = UiBuild.Card(_docs, new Thickness(10, 8));

        var body = editing == null ? BuildCreate(presetClientId) : BuildEdit(editing);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        buttons.Children.Add(UiBuild.Btn(editing == null ? "ساخت پروژه" : "ثبت", Submit, primary: true));
        buttons.Children.Add(UiBuild.Btn("انصراف", Close));
        body.Children.Add(buttons);
        Content = new ScrollViewer { Content = body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Opened += (_, _) => (editing == null ? ClientBox : (Control)OwnerBox).Focus();
    }

    // ---------- ساخت پروژه جدید ----------
    private StackPanel BuildCreate(string? presetClientId)
    {
        var body = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        body.Children.Add(new TextBlock { Text = $"شماره پروژه: {_number}", FontWeight = FontWeight.Bold, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Right });
        body.Children.Add(UiBuild.Labeled("متقاضی (کسی که سفارش می‌دهد)", ClientBox));
        Suggestions.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Client>((c, _) =>
        {
            if (c == null) return new TextBlock();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new TextBlock { Text = c.Name });
            row.Children.Add(UiBuild.KindPill(c.Kind));
            var n = _st.W.ProjectsOf(c.Id).Count();
            row.Children.Add(UiBuild.Text($"{Digits.Fa(n)} پروژه", "muted"));
            return row;
        });
        body.Children.Add(Suggestions);
        body.Children.Add(_clientInfo);
        body.Children.Add(SameOwner);
        var ownerRow = UiBuild.Labeled("مالک ملک (وقتی نماینده یا شخص دیگری سفارش می‌دهد)", OwnerBox);
        ownerRow.IsVisible = false;
        body.Children.Add(ownerRow);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        var loc = UiBuild.Labeled("موقعیت / پلاک", Location);
        var type = UiBuild.Labeled("نوع کار", TypeBox);
        Grid.SetColumn(type, 2);
        grid.Children.Add(loc);
        grid.Children.Add(type);
        body.Children.Add(grid);
        var fromRow = UiBuild.Labeled("ساخت از روی پروژه قبلی این متقاضی (اختیاری)", FromBox);
        fromRow.IsVisible = false;
        body.Children.Add(fromRow);
        _docsCard.IsVisible = false;
        body.Children.Add(_docsCard);
        body.Children.Add(UiBuild.Labeled("نام لاتین پوشه (قابل ویرایش)", Slug));
        Preview.Classes.Add("muted");
        body.Children.Add(Preview);
        var note = UiBuild.Card(new TextBlock
        {
            Text = "فقط پوشه اصلی پروژه و یک فایل راهنما (README) ساخته می‌شود. زیرپوشه‌ها (فایل‌های اتوکد، عکس‌ها، اسناد و …) هنگام اولین فایلی که به آن‌ها اضافه می‌شود ساخته می‌شوند.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12.5,
        }, new Thickness(10, 8));
        body.Children.Add(note);

        ClientBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !_settingClientText) OnClientTyped(); };
        SameOwner.IsCheckedChanged += (_, _) => { ownerRow.IsVisible = SameOwner.IsChecked != true; AutoSlug(); };
        OwnerBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) AutoSlug(); };
        Location.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) AutoSlug(); };
        Slug.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { if (!_settingSlug) _slugEdited = true; UpdatePreview(); } };
        Suggestions.SelectionChanged += (_, _) => { if (Suggestions.SelectedItem is Client c) PickClient(c); };
        FromBox.SelectionChanged += (_, _) => OnFromChanged(ownerRow);
        if (presetClientId != null && _st.W.Client(presetClientId) is { } preset) PickClient(preset);
        else RefreshClientInfo();
        AutoSlug();
        return body;
    }

    private void OnClientTyped()
    {
        var text = ClientBox.Text ?? "";
        if (_selected != null && text != _selected.Name) { _selected = null; RefreshFrom(); }
        var list = _selected == null ? ClientMatcher.Suggest(_st.W.Clients, text) : new List<Client>();
        Suggestions.SelectedItem = null;
        Suggestions.ItemsSource = list;
        Suggestions.IsVisible = list.Count > 0;
        RefreshClientInfo();
        AutoSlug();
    }

    public void PickClient(Client c)
    {
        _selected = c;
        _settingClientText = true;
        ClientBox.Text = c.Name;
        _settingClientText = false;
        Suggestions.IsVisible = false;
        Suggestions.ItemsSource = null;
        RefreshClientInfo();
        RefreshFrom();
        AutoSlug();
    }

    private void RefreshClientInfo()
    {
        _clientInfo.Children.Clear();
        var text = (ClientBox.Text ?? "").Trim();
        if (_selected != null)
        {
            var n = _st.W.ProjectsOf(_selected.Id).Count();
            var line = new StackPanel { Spacing = 2 };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            head.Children.Add(new TextBlock { Text = "متقاضی موجود", FontWeight = FontWeight.Bold });
            head.Children.Add(UiBuild.KindPill(_selected.Kind));
            head.Children.Add(new TextBlock { Text = _selected.Phone, FlowDirection = FlowDirection.LeftToRight, FontSize = 12.5 });
            line.Children.Add(head);
            line.Children.Add(UiBuild.Text($"{Digits.Fa(n)} پروژه ثبت‌شده؛ پروژه جدید به همین متقاضی وصل می‌شود.", "muted"));
            var change = new Button { Content = "تغییر متقاضی", Classes = { "link" }, HorizontalAlignment = HorizontalAlignment.Right };
            change.Click += (_, _) => { _selected = null; ClientBox.Text = ""; ClientBox.Focus(); RefreshFrom(); };
            line.Children.Add(change);
            _clientInfo.Children.Add(UiBuild.Card(line, new Thickness(10, 8)));
        }
        else if (text.Length > 0)
        {
            var line = new StackPanel { Spacing = 6 };
            var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            head.Children.Add(new TextBlock { Text = "متقاضی جدید", FontWeight = FontWeight.Bold });
            head.Children.Add(UiBuild.Text("با ثبت پروژه به فهرست متقاضیان اضافه می‌شود.", "muted"));
            line.Children.Add(head);
            var detail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            detail.Children.Add(_kind);
            detail.Children.Add(_phone);
            line.Children.Add(detail);
            _clientInfo.Children.Add(UiBuild.Card(line, new Thickness(10, 8)));
        }
    }

    private void RefreshFrom()
    {
        var host = FromBox.Parent as StackPanel;
        _previous = _selected == null ? new List<Project>() : _st.W.ProjectsOf(_selected.Id).ToList();
        FromBox.Items.Clear();
        FromBox.Items.Add("پروژه خالی");
        foreach (var p in _previous) FromBox.Items.Add($"{p.Number} · {p.Type} · {p.Location}");
        FromBox.SelectedIndex = 0;
        if (host != null) host.IsVisible = _previous.Count > 0;
        _docsCard.IsVisible = false;
    }

    private void OnFromChanged(Control ownerRow)
    {
        var idx = FromBox.SelectedIndex - 1;
        _docs.Children.Clear();
        _docsCard.IsVisible = false;
        if (idx < 0 || idx >= _previous.Count) return;
        var from = _previous[idx];
        Location.Text = from.Location;
        if (_selected != null && TextNorm.Normalize(from.Owner) != TextNorm.Normalize(_selected.Name))
        {
            SameOwner.IsChecked = false;
            OwnerBox.Text = from.Owner;
            ownerRow.IsVisible = true;
        }
        _docs.Children.Add(new TextBlock { Text = "مدارک قابل کپی به پوشه «اسناد دریافتی» پروژه جدید (با کپی ساخته می‌شود)", FontWeight = FontWeight.Medium });
        var files = Workspace.CopyableDocuments(_st.Root, from);
        foreach (var f in files)
            _docs.Children.Add(new CheckBox { Content = new TextBlock { Text = Path.GetFileName(f), FlowDirection = FlowDirection.LeftToRight }, IsChecked = true, Tag = f });
        if (files.Count == 0) _docs.Children.Add(UiBuild.Text("در پوشه‌های اسناد دریافتی و مدارک ثبتی آن پروژه فایلی نیست.", "muted"));
        _docs.Children.Add(UiBuild.Text($"فایل‌های پروژه {from.Number} دست‌نخورده می‌مانند و دو پروژه «مرتبط» ثبت می‌شوند.", "muted", wrap: true));
        _docsCard.IsVisible = true;
    }

    private void AutoSlug()
    {
        if (_editing != null) return;
        if (!_slugEdited)
        {
            _settingSlug = true;
            var owner = SameOwner.IsChecked == true || string.IsNullOrWhiteSpace(OwnerBox.Text) ? ClientBox.Text ?? "" : OwnerBox.Text!;
            Slug.Text = Translit.FolderSlug(owner, Location.Text ?? "");
            _settingSlug = false;
        }
        UpdatePreview();
    }

    private void UpdatePreview() =>
        Preview.Text = Path.Combine(_st.Root, ProjectFolders.RelativePath(_number, SlugText()).Replace('/', Path.DirectorySeparatorChar));

    public string SlugText() => Translit.SanitizeSlug(Slug.Text ?? "");

    // ---------- ویرایش پروژه ----------
    private StackPanel BuildEdit(Project p)
    {
        var body = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        body.Children.Add(new TextBlock { Text = $"پروژه {p.Number}", FontWeight = FontWeight.Bold, FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Right });
        foreach (var c in _st.W.Clients) _clientPicker.Items.Add(c.Name);
        var current = _st.W.ClientOf(p);
        _clientPicker.SelectedIndex = Math.Max(0, current == null ? 0 : _st.W.Clients.IndexOf(current));
        body.Children.Add(UiBuild.Labeled("متقاضی", _clientPicker));
        OwnerBox.Text = p.Owner;
        body.Children.Add(UiBuild.Labeled("مالک ملک", OwnerBox));
        Location.Text = p.Location;
        body.Children.Add(UiBuild.Labeled("موقعیت / پلاک", Location));
        if (!TypeBox.Items.Contains(p.Type)) TypeBox.Items.Add(p.Type);
        TypeBox.SelectedItem = p.Type;
        StatusBox.SelectedIndex = Math.Max(0, Statuses.All.ToList().FindIndex(s => s.Key == p.Status));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        var type = UiBuild.Labeled("نوع کار", TypeBox);
        var status = UiBuild.Labeled("وضعیت", StatusBox);
        Grid.SetColumn(status, 2);
        grid.Children.Add(type);
        grid.Children.Add(status);
        body.Children.Add(grid);
        Due.Text = p.Due;
        body.Children.Add(UiBuild.Labeled("تاریخ تحویل (شمسی، مثل 1405/08/15)", Due));
        Note.Text = p.Note;
        body.Children.Add(UiBuild.Labeled("یادداشت", Note));
        return body;
    }

    // ---------- ثبت ----------
    public void Submit()
    {
        if (_editing == null)
        {
            if (string.IsNullOrWhiteSpace(ClientBox.Text)) { ClientBox.Focus(); return; }
            var idx = FromBox.SelectedIndex - 1;
            Request = new NewProjectRequest
            {
                ClientId = _selected?.Id,
                ClientName = (ClientBox.Text ?? "").Trim(),
                ClientKind = _kind.SelectedItem as string ?? "حقیقی",
                ClientPhone = _phone.Text ?? "",
                OwnerIsClient = SameOwner.IsChecked == true,
                Owner = OwnerBox.Text ?? "",
                Location = Location.Text ?? "",
                Type = TypeBox.SelectedItem as string ?? "",
                Slug = SlugText(),
                FromProject = idx >= 0 && idx < _previous.Count ? _previous[idx].Number : null,
                DocsToCopy = _docs.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToList(),
            };
        }
        else if (string.IsNullOrWhiteSpace(OwnerBox.Text)) { OwnerBox.Focus(); return; }
        Accepted = true;
        Close();
    }

    public void ApplyEditTo(Project p)
    {
        p.Owner = (OwnerBox.Text ?? "").Trim();
        p.Location = (Location.Text ?? "").Trim();
        p.Type = TypeBox.SelectedItem as string ?? p.Type;
        p.Status = Statuses.All[Math.Max(0, StatusBox.SelectedIndex)].Key;
        p.Due = (Due.Text ?? "").Trim();
        p.Note = (Note.Text ?? "").Trim();
        if (_clientPicker.SelectedIndex >= 0 && _clientPicker.SelectedIndex < _st.W.Clients.Count)
            p.ClientId = _st.W.Clients[_clientPicker.SelectedIndex].Id;
    }
}
