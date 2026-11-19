using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using SurveyProjectManager.App;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.App.Views;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.Tests;

public class UiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spm_ui_" + Guid.NewGuid().ToString("N"));

    public UiTests()
    {
        Directory.CreateDirectory(_root);
        Digits.Persian = true;
    }

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private static NewProjectRequest Req(string client, string owner, string loc, string type, string slug, string kind = "حقیقی") => new()
    {
        ClientName = client, ClientKind = kind, OwnerIsClient = owner == client, Owner = owner, Location = loc, Type = type, Slug = slug,
    };

    /// <summary>داده نمونه: ۴ متقاضی، ۷ پروژه در دو سال، چند فایل در پروژه ۱۴۰۵-۰۰۲.</summary>
    private Workspace Seed()
    {
        var w = new Workspace(_root);
        var today = DateTime.Today;
        string Due(int days) => Jalali.Format(today.AddDays(days));
        var specs = new (int Year, NewProjectRequest R, string Status, int? DueDays)[]
        {
            (1404, Req("آقای احمدی", "آقای احمدی", "کرج، پلاک ۸۸۱۲ اصلی", "توپوگرافی", "Ahmadi_Karaj"), "done", -300),
            (1404, Req("مهندس طاهری", "آقای فتحی", "ساوجبلاغ، پلاک ۴۴۱", "افراز", "Fathi_Savojbolagh", "نماینده"), "done", -320),
            (1405, Req("آقای احمدی", "آقای احمدی", "کرج، پلاک ۸۸۱۲ اصلی", "تفکیک", "Ahmadi_Karaj"), "draw", 3),
            (1405, Req("مهندس طاهری", "خانم کریمی", "شهریار، پلاک ۱۲۴۸ فرعی", "تفکیک", "Karimi_Shahriar", "نماینده"), "field", 8),
            (1405, Req("مهندس طاهری", "آقای صادقی", "نظرآباد، زمین کشاورزی", "پیاده‌سازی", "Sadeghi_Nazarabad", "نماینده"), "field", -8),
            (1405, Req("شرکت آرین‌ساز", "شرکت آرین‌ساز", "ملارد، قطعه ۲۲", "توپوگرافی", "Arian_Malard", "حقوقی"), "check", 2),
            (1405, Req("خانم رضایی", "خانم رضایی", "شهریار، پلاک ۱۲۰۴ فرعی", "افراز", "Rezaei_Shahriar"), "reg", 14),
        };
        foreach (var s in specs)
        {
            var p = w.CreateProject(s.R, s.Year, "UTM 39N");
            p.Status = s.Status;
            p.Due = s.DueDays is { } d ? Due(d) : "";
        }
        w.Find("1405-001")!.Related = "1404-001";
        // محتوای پروژه ۱۴۰۵-۰۰۱ (آقای احمدی)
        var dir = w.Find("1405-001")!.FullPath(_root);
        void F(string folder, string name, int size = 2000) { Directory.CreateDirectory(Path.Combine(dir, folder)); File.WriteAllBytes(Path.Combine(dir, folder, name), new byte[size]); }
        foreach (var n in new[] { "Plan_v03.dwg", "Plan_v02.dwg", "Plan_v01.dwg" }) F("04_CAD", "1405-001_" + n, 2_400_000);
        F("04_CAD", "1405-001_Boundary.dxf", 890_000);
        F("01_Documents", "Deed_Scan.pdf", 1_200_000);
        F("01_Documents", "Registry_Inquiry.pdf", 310_000);
        F("02_Field_Raw", "TS_day1.job", 88_000);
        Directory.CreateDirectory(Path.Combine(dir, "03_Processing"));
        File.WriteAllText(Path.Combine(dir, "03_Processing", "points_adjusted.csv"),
            "Point,E,N,Z\nP1,498412.331,3962120.447,1352.18\nP2,498455.902,3962198.015,1353.60\nP3,498521.114,3962185.226,1351.94\n");
        for (var i = 1; i <= 3; i++) F("07_Photos", $"IMG_040{i}.jpg", 4_000_000);
        w.Save();
        return w;
    }

    private MainWindow OpenWindow(string? theme = null)
    {
        var cfg = new AppConfig { Root = _root, BackupDest = Path.Combine(_root + "_backup"), LastBackup = DateTime.Now.AddDays(-3).ToString("yyyy-MM-dd HH:mm"), Theme = theme ?? "light" };
        var w = new MainWindow(cfg, persistConfig: false) { Width = 1320, Height = 800 };
        w.Show();
        return w;
    }

    private static void Shot(Window w, string name)
    {
        var dir = Environment.GetEnvironmentVariable("SPM_SHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        w.UpdateLayout();
        w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name));
    }

    [AvaloniaFact]
    public void Home_and_sections_render()
    {
        var ws = Seed();
        var w = OpenWindow();
        Assert.IsType<HomeView>(w.CurrentContent);
        Shot(w, "home.png");

        w.ShowSection("projects");
        Assert.IsType<ProjectsView>(w.CurrentContent);
        Shot(w, "projects.png");

        w.ShowSection("clients");
        Assert.IsType<ClientsView>(w.CurrentContent);
        Shot(w, "clients.png");

        var taheri = ws.Clients.First(c => c.Name.Contains("طاهری"));
        w.ShowSection("client", taheri.Id);
        Assert.IsType<ClientView>(w.CurrentContent);
        Shot(w, "client.png");

        w.ShowSection("backup");
        Assert.IsType<BackupView>(w.CurrentContent);
        Shot(w, "backup.png");

        w.ShowSection("legacy");
        Assert.IsType<LegacyView>(w.CurrentContent);
    }

    [AvaloniaFact]
    public void Open_project_in_tab_shows_tree_with_ghost_folders_and_preview()
    {
        var ws = Seed();
        var w = OpenWindow();
        w.OpenProject("1405-001");
        Assert.Equal("1405-001", w.ActiveKey);
        Assert.Equal(new[] { "1405-001" }, w.OpenTabs);
        var view = w.ActiveProjectView!;
        Assert.Equal(ws.Find("1405-001")!.FullPath(_root), view.CurrentFolder);

        // ریشه: فقط پوشه‌هایی که فایل دارند + README
        Assert.Equal(new[] { "01_Documents", "02_Field_Raw", "03_Processing", "04_CAD", "07_Photos", "README.txt" }, view.Rows.Select(r => r.Name).ToArray());
        // درخت: دسته‌های ساخته‌نشده به‌صورت کم‌رنگ
        var ghosts = view.RootNode.Children.Where(c => c.IsGhost).Select(c => c.Name).ToArray();
        Assert.Equal(new[] { "05_Final_PDF", "06_Registration", "99_Archive" }, ghosts);
        Assert.False(Directory.Exists(Path.Combine(view.CurrentFolder, "05_Final_PDF")));

        view.Navigate(Path.Combine(view.CurrentFolder, "04_CAD"));
        Assert.Equal(4, view.Rows.Count);
        view.SelectedRow = view.Rows.First(r => r.Name.EndsWith("Boundary.dxf"));
        Shot(w, "project.png");

        // رفتن به دسته ساخته‌نشده: خطا نمی‌دهد و لیست خالی است
        view.Navigate(Path.Combine(ws.Find("1405-001")!.FullPath(_root), "05_Final_PDF"));
        Assert.Empty(view.Rows);
        Shot(w, "project_ghost.png");

        w.CloseTab("1405-001");
        Assert.Empty(w.OpenTabs);
        Assert.Equal("home", w.ActiveKey);
    }

    [AvaloniaFact]
    public async Task Search_palette_finds_client_project_and_file_current_project_first()
    {
        var ws = Seed();
        var w = OpenWindow();
        ws.Projects.ToList().ForEach(_ => { });
        var st = w.State;
        st.InvalidateIndex();
        await st.EnsureIndexAsync();
        var palette = new SearchPalette(st, "1405-001");
        palette.Show();
        palette.Query = "plan";
        palette.Refresh();
        var files = palette.Items.Where(i => i.Kind == "file").ToList();
        Assert.True(files.Count >= 3);
        Assert.All(files, f => Assert.Equal("1405-001", f.Tag));
        palette.Query = "طاهری";
        palette.Refresh();
        Assert.Contains(palette.Items, i => i.Kind == "client");
        Assert.Contains(palette.Items, i => i.Kind == "project");
        Shot(palette, "search.png");
        palette.Close();
    }

    [AvaloniaFact]
    public void New_project_dialog_suggests_client_fills_from_previous_and_builds_request()
    {
        var ws = Seed();
        var w = OpenWindow();
        // سند و استعلام پروژه قبلی برای کپی
        var prev = ws.Find("1404-001")!;
        var prevDir = prev.FullPath(_root);
        Directory.CreateDirectory(Path.Combine(prevDir, "01_Documents"));
        File.WriteAllText(Path.Combine(prevDir, "01_Documents", "Deed_Scan.pdf"), "x");

        var dlg = new ProjectDialog(w.State);
        dlg.Show();
        dlg.ClientBox.Text = "احمد";
        Assert.Equal("c1", Assert.Single((IEnumerable<Client>)dlg.Suggestions.ItemsSource!).Id);
        Shot(dlg, "dialog_suggest.png");

        dlg.PickClient(ws.Clients.First(c => c.Id == "c1"));
        Assert.True(dlg.FromBox.IsVisible || dlg.FromBox.Parent is Control { IsVisible: true });
        Assert.Equal(3, dlg.FromBox.ItemCount); // پروژه خالی + ۲ پروژه احمدی
        dlg.FromBox.SelectedIndex = 2; // پروژه قدیمی‌تر (۱۴۰۴)
        Assert.Equal("کرج، پلاک ۸۸۱۲ اصلی", dlg.Location.Text);
        Shot(dlg, "dialog_from.png");
        dlg.Submit();
        Assert.True(dlg.Accepted);
        Assert.Equal("c1", dlg.Request!.ClientId);
        Assert.Equal("1404-001", dlg.Request.FromProject);
        Assert.Equal("Ahmdy_Krj", dlg.Request.Slug);
        Assert.Single(dlg.Request.DocsToCopy);
        dlg.Close();

        // متقاضی جدید + مالک جدا
        var dlg2 = new ProjectDialog(w.State);
        dlg2.Show();
        dlg2.ClientBox.Text = "مهندس تازه";
        dlg2.SameOwner.IsChecked = false;
        dlg2.OwnerBox.Text = "خانم نوری";
        dlg2.Submit();
        Assert.Null(dlg2.Request!.ClientId);
        Assert.False(dlg2.Request.OwnerIsClient);
        Assert.Equal("خانم نوری", dlg2.Request.Owner);
        dlg2.Close();
    }

    [AvaloniaFact]
    public void Import_dialog_plans_copies_and_creates_folders_on_demand()
    {
        var ws = Seed();
        var proj = ws.Find("1405-001")!.FullPath(_root);
        var src = Path.Combine(_root + "_src");
        Directory.CreateDirectory(src);
        var files = new[] { "Plan_v04.dwg", "Final_Signed.pdf", "Letter.pdf", "IMG_0501.jpg" }.Select(n => { var p = Path.Combine(src, n); File.WriteAllText(p, "x"); return p; }).ToList();
        try
        {
            var dlg = new ImportDialog(proj, "1405-001 آقای احمدی", files, fallbackFolder: "01_Documents");
            dlg.Show();
            var by = dlg.Rows.ToDictionary(r => r.Original);
            Assert.Equal("04_CAD", by["Plan_v04.dwg"].Item.Folder);
            Assert.Equal(3, by["Plan_v04.dwg"].Item.ArchiveOlder.Count);
            Assert.True(by["Final_Signed.pdf"].Item.NewFolder);
            Assert.Equal("01_Documents", by["Letter.pdf"].Item.Folder); // نوع نامشخص → پوشه‌ای که کاربر داخل آن بود
            Shot(dlg, "import.png");
            dlg.Execute();
            var r = dlg.Result!;
            Assert.Equal(4, r.Copied.Count);
            Assert.Contains("05_Final_PDF", r.CreatedFolders);
            Assert.Equal(3, r.Archived.Count);
            Assert.True(File.Exists(Path.Combine(proj, "05_Final_PDF", "Final_Signed.pdf")));
            Assert.Equal(3, Directory.GetFiles(Path.Combine(proj, "99_Archive")).Length);
            dlg.Close();
        }
        finally { Directory.Delete(src, recursive: true); }
    }

    [AvaloniaFact]
    public void Theme_and_digits_toggles()
    {
        Seed();
        var w = OpenWindow();
        w.ToggleTheme();
        Assert.Equal(Avalonia.Styling.ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
        w.ShowSection("home");
        Shot(w, "home_dark.png");
        w.OpenProject("1405-001");
        Shot(w, "project_dark.png");
        w.ToggleTheme();
        Assert.Equal(Avalonia.Styling.ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);
        w.ToggleDigits();
        Assert.False(Digits.Persian);
        Assert.Equal("12", Digits.Fa("12"));
        w.ToggleDigits();
        Assert.Equal("۱۲", Digits.Fa("12"));
        Assert.Equal("1405-001", w.ActiveKey); // تب‌ها بعد از تغییر ارقام حفظ می‌شوند
    }

    [AvaloniaFact]
    public async Task Backup_view_runs_incrementally()
    {
        Seed();
        var w = OpenWindow();
        var dest = _root + "_backup";
        try
        {
            w.State.Cfg.BackupDest = dest;
            var view = new BackupView(w.State);
            var host = new Window { Content = view, Width = 900, Height = 600 };
            host.Show();
            await view.RunAsync();
            Assert.True(File.Exists(Path.Combine(dest, "projects.csv")));
            Assert.True(File.Exists(Path.Combine(dest, "backup_log.csv")));
            Assert.NotEqual("", w.State.Cfg.LastBackup);
            host.Close();
        }
        finally { if (Directory.Exists(dest)) { foreach (var f in Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(dest, true); } }
    }

    [AvaloniaFact]
    public async Task Legacy_view_registers_selected_folders()
    {
        Seed();
        var old = _root + "_old";
        Directory.CreateDirectory(Path.Combine(old, "Karimi_Plot12"));
        File.WriteAllText(Path.Combine(old, "Karimi_Plot12", "plan.dwg"), "x");
        Directory.CreateDirectory(Path.Combine(old, "Backup_2022"));
        File.WriteAllText(Path.Combine(old, "Backup_2022", "x.zip"), "x");
        try
        {
            var w = OpenWindow();
            var before = w.State.W.Projects.Count;
            w.ShowSection("legacy");
            var view = Assert.IsType<LegacyView>(w.CurrentContent);
            view.ScanFolder(old);
            Shot(w, "legacy.png");
            await view.ImportAsync();
            Assert.Equal(before + 1, w.State.W.Projects.Count); // فقط پوشه‌ای که پروژه به نظر می‌رسید
            Assert.Equal(before + 1, Workspace.Load(_root).Projects.Count);
            Shot(w, "legacy_done.png");
        }
        finally { Directory.Delete(old, recursive: true); }
    }

    [AvaloniaFact]
    public void Preview_renders_pdf_first_page_and_images()
    {
        // یک PDF حداقلی یک‌صفحه‌ای
        var pdf = Path.Combine(_root, "t.pdf");
        File.WriteAllText(pdf, MinimalPdf);
        var bmp = PreviewBuilder.RenderPdf(pdf);
        Assert.NotNull(bmp);
        Assert.True(bmp!.PixelSize.Width > 100);
    }

    private const string MinimalPdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 300 200]/Contents 4 0 R/Resources<<>>>>endobj\n4 0 obj<</Length 44>>stream\n0 0 150 100 re f\nendstream\nendobj\ntrailer<</Root 1 0 R/Size 5>>\n%%EOF";
}
