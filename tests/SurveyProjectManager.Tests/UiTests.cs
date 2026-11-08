using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using SurveyProjectManager.App;
using SurveyProjectManager.App.Dialogs;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.Tests;

public class UiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "spm_ui_" + Guid.NewGuid().ToString("N"));

    public UiTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly (string Number, string Owner, string Location, string Type, string Status, string Due, string Slug)[] Sample =
    {
        ("1405-014", "آقای احمدی", "کرج، پلاک ۸۸۱۲ اصلی", "تفکیک", "draw", "1405/08/15", "Ahmadi_Karaj"),
        ("1405-013", "شرکت آرین‌ساز", "ملارد، قطعه ۲۲", "توپوگرافی", "check", "1405/08/10", "Arian_Malard"),
        ("1405-012", "خانم رضایی", "شهریار، پلاک ۱۲۰۴ فرعی", "افراز", "reg", "1405/07/28", "Rezaei_Shahriar"),
        ("1405-011", "آقای موسوی", "اسلامشهر، پلاک ۳۳۴", "ثبتی / تحدید حدود", "done", "1405/07/20", "Mousavi_Eslamshahr"),
        ("1405-010", "آقای صادقی", "نظرآباد، زمین کشاورزی", "پیاده‌سازی", "field", "1405/08/20", "Sadeghi_Nazarabad"),
    };

    private List<Project> Seed()
    {
        var list = new List<Project>();
        foreach (var s in Sample)
        {
            var p = new Project
            {
                Number = s.Number, Owner = s.Owner, Location = s.Location, Type = s.Type, Status = s.Status,
                Due = s.Due, Folder = ProjectFolders.RelativePath(s.Number, s.Slug),
            };
            ProjectFolders.Create(_root, p, "UTM 39N");
            list.Add(p);
        }
        var cad = Path.Combine(list[0].FullPath(_root), "04_CAD");
        foreach (var f in new[] { "Plan_v03.dwg", "Plan_v02.dwg", "Plan_v01.dwg", "Plan_v03.dxf", "Boundary.dwg" })
            File.WriteAllBytes(Path.Combine(cad, "1405-014_" + f), new byte[200_000]);
        File.WriteAllText(Path.Combine(list[0].FullPath(_root), "01_Documents", "Deed_Scan.pdf"), "x");
        ProjectStore.Save(_root, list);
        return list;
    }

    private MainWindow OpenWindow()
    {
        var w = new MainWindow(new AppConfig { Root = _root }, persistConfig: false) { Width = 1280, Height = 760 };
        w.Show();
        return w;
    }

    private static void Shot(Window w, string name)
    {
        var dir = Environment.GetEnvironmentVariable("SPM_SHOT_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        w.CaptureRenderedFrame()?.Save(Path.Combine(dir, name));
    }

    [AvaloniaFact]
    public void List_shows_projects_newest_first_and_filters()
    {
        Seed();
        var w = OpenWindow();
        var grid = w.FindControl<DataGrid>("ProjectGrid")!;
        var rows = ((IEnumerable<ProjectRow>)grid.ItemsSource!).ToList();
        Assert.Equal(5, rows.Count);
        Assert.Equal("1405-014", rows[0].Number);

        w.FindControl<TextBox>("SearchBox")!.Text = "رضایی";
        rows = ((IEnumerable<ProjectRow>)grid.ItemsSource!).ToList();
        Assert.Equal("1405-012", Assert.Single(rows).Number);

        w.FindControl<TextBox>("SearchBox")!.Text = "";
        w.FindControl<ListBox>("StatusList")!.SelectedIndex = 3; // ترسیم
        rows = ((IEnumerable<ProjectRow>)grid.ItemsSource!).ToList();
        Assert.Equal("1405-014", Assert.Single(rows).Number);
        Shot(w, "list.png");
    }

    [AvaloniaFact]
    public void Enter_project_browse_folders_and_copy_files()
    {
        var seeded = Seed();
        var w = OpenWindow();
        var grid = w.FindControl<DataGrid>("ProjectGrid")!;
        grid.SelectedItem = ((IEnumerable<ProjectRow>)grid.ItemsSource!).First();
        w.EnterProject();
        Assert.True(w.IsProjectPageVisible);

        var projectDir = seeded[0].FullPath(_root);
        Assert.Equal(projectDir, w.CurrentFolder);
        var files = w.FindControl<DataGrid>("FileGrid")!;
        Assert.Equal(ProjectFolders.Subfolders.Count + 1, ((IEnumerable<FileRow>)files.ItemsSource!).Count()); // + README.txt

        var cad = Path.Combine(projectDir, "04_CAD");
        w.Navigate(cad);
        Assert.Equal(cad, w.CurrentFolder);
        var rows = ((IEnumerable<FileRow>)files.ItemsSource!).ToList();
        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.StartsWith("اتوکد", r.Kind));

        var src = Path.Combine(_root, "extra.dwg");
        File.WriteAllText(src, "x");
        w.CopyFiles(new[] { src });
        Assert.True(File.Exists(Path.Combine(cad, "extra.dwg")));
        Assert.Equal(6, ((IEnumerable<FileRow>)files.ItemsSource!).Count());
        Shot(w, "project.png");

        w.ShowList();
        Assert.False(w.IsProjectPageVisible);
    }

    [AvaloniaFact]
    public void Status_change_persists_to_csv()
    {
        Seed();
        var w = OpenWindow();
        w.SetStatus(w.Projects.First(p => p.Number == "1405-010"), "done");
        Assert.Equal("done", ProjectStore.Load(_root).First(p => p.Number == "1405-010").Status);
    }

    [AvaloniaFact]
    public void Dialog_builds_slug_from_names()
    {
        var dlg = new ProjectDialog("1405-007", @"D:\Projects");
        dlg.OwnerBox.Text = "خانم رضایی";
        dlg.Location.Text = "شهریار، پلاک ۱";
        Assert.Equal("Rzayy_Shhryar", dlg.SlugText());
        Assert.Contains("1405-007_Rzayy_Shhryar", dlg.Preview.Text);
        dlg.Slug.Text = "Custom Name!";
        Assert.Equal("Custom_Name", dlg.SlugText());
    }
}
