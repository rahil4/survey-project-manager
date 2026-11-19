using SurveyProjectManager.Core;

namespace SurveyProjectManager.Tests;

public class FeatureTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "spm_f_" + Guid.NewGuid().ToString("N"));

    public FeatureTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        foreach (var f in Directory.EnumerateFiles(_tmp, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_tmp, recursive: true);
    }

    private string Dir(params string[] parts) => Path.Combine(new[] { _tmp }.Concat(parts).ToArray());

    private string Touch(string path, string content = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    // ---------- تاریخ و متن ----------
    [Fact]
    public void Jalali_roundtrip_and_days_until()
    {
        for (var d = new DateTime(2020, 1, 1); d < new DateTime(2030, 1, 1); d = d.AddDays(37))
        {
            var (y, m, day) = Jalali.From(d);
            Assert.Equal(d, Jalali.ToGregorian(y, m, day));
        }
        Assert.Equal(5, Jalali.DaysUntil("1405/09/03", Jalali.ToGregorian(1405, 8, 28)));
        Assert.Equal(-8, Jalali.DaysUntil("۱۴۰۵/۰۸/۲۰", Jalali.ToGregorian(1405, 8, 28)));
        Assert.Null(Jalali.DaysUntil("", DateTime.Today));
        Assert.Null(Jalali.DaysUntil("abc", DateTime.Today));
        Assert.Equal("28 آبان 1405", Jalali.Long(Jalali.ToGregorian(1405, 8, 28)));
    }

    [Fact]
    public void TextNorm_unifies_arabic_letters_and_digits()
    {
        Assert.Equal("کریمی 1405", TextNorm.Normalize("كريمي ۱۴۰۵"));
        Assert.True(TextNorm.Contains("آقای احمدی", "احمدی"));
        Assert.Equal("احمدی", TextNorm.StripHonorific(TextNorm.Normalize("آقای احمدی")));
        Assert.Equal("۱۲ فایل", Digits.Fa("12 فایل"));
    }

    // ---------- متقاضی ----------
    private static List<Client> SampleClients() => new()
    {
        new Client { Id = "c1", Name = "آقای احمدی" }, new Client { Id = "c2", Name = "مهندس طاهری", Kind = "نماینده" },
        new Client { Id = "c3", Name = "خانم رضایی" },
    };

    [Fact]
    public void ClientMatcher_suggests_and_ignores_honorifics()
    {
        var cs = SampleClients();
        Assert.Equal("c1", Assert.Single(ClientMatcher.Suggest(cs, "احمد")).Id);
        Assert.Equal("c1", ClientMatcher.Suggest(cs, "احمدی")[0].Id);
        Assert.Equal("c1", ClientMatcher.FindExact(cs, "خانم احمدی")?.Id); // لقب نادیده گرفته می‌شود
        Assert.Empty(ClientMatcher.Suggest(cs, ""));
        Assert.Null(ClientMatcher.FindExact(cs, "کریمی"));
        Assert.Equal("c4", ClientStore.NextId(cs.Append(new Client { Id = "c3" })));
    }

    [Fact]
    public void Workspace_migrates_v1_projects_to_clients_and_persists()
    {
        // فایل نسخه ۱: ستون متقاضی ندارد
        File.WriteAllText(ProjectStore.CsvPath(_tmp),
            "number,owner,location,type,status,due,folder,note\r\n" +
            "1405-001,آقای احمدی,کرج,تفکیک,draw,,1405/1405-001_A,\r\n" +
            "1404-009,آقای احمدی,کرج,توپوگرافی,done,,1404/1404-009_A,\r\n" +
            "1405-002,خانم رضایی,شهریار,افراز,reg,,1405/1405-002_R,\r\n");
        var w = Workspace.Load(_tmp);
        Assert.Equal(2, w.Clients.Count);
        Assert.Equal(2, w.ProjectsOf(w.Clients.First(c => c.Name.Contains("احمدی")).Id).Count());
        Assert.All(w.Projects, p => Assert.NotEqual("", p.ClientId));
        // دوباره بارگذاری: چیزی تکراری ساخته نمی‌شود
        Assert.Equal(2, Workspace.Load(_tmp).Clients.Count);
    }

    [Fact]
    public void Workspace_related_projects_and_repeated_properties()
    {
        var w = new Workspace(_tmp);
        w.Clients.Add(new Client { Id = "c1", Name = "احمدی" });
        w.Projects.Add(new Project { Number = "1404-087", ClientId = "c1", Location = "کرج، پلاک ۸۸۱۲ اصلی" });
        w.Projects.Add(new Project { Number = "1405-014", ClientId = "c1", Location = "کرج، پلاک 8812 اصلی" });
        w.Projects.Add(new Project { Number = "1405-020", ClientId = "c1", Location = "شهریار" });
        var rel = w.RelatedTo(w.Projects[1]);
        Assert.Equal("1404-087", Assert.Single(rel).Number); // ارقام فارسی/لاتین یکی حساب می‌شوند
        var rep = Assert.Single(w.RepeatedProperties("c1"));
        Assert.Equal(new[] { "1404-087", "1405-014" }, rep.Projects.Select(p => p.Number));
        Workspace.Link(w.Projects[2], w.Projects[0]);
        Assert.Contains("1404-087", w.RelatedTo(w.Projects[2]).Select(p => p.Number));
        Assert.Contains("1405-020", w.RelatedTo(w.Projects[0]).Select(p => p.Number)); // لینک دوطرفه
    }

    [Fact]
    public void CreateProject_uses_existing_client_copies_docs_and_links_previous()
    {
        var w = new Workspace(_tmp);
        w.Clients.Add(new Client { Id = "c1", Name = "آقای احمدی", Phone = "0912" });
        var old = new Project { Number = "1404-087", ClientId = "c1", Owner = "آقای احمدی", Location = "کرج", Type = "توپوگرافی", Folder = "1404/1404-087_A" };
        ProjectFolders.Create(_tmp, old, "UTM");
        var deed = Touch(Path.Combine(old.FullPath(_tmp), "01_Documents", "Deed_Scan.pdf"));
        Touch(Path.Combine(old.FullPath(_tmp), "04_CAD", "plan.dwg"));
        w.Projects.Add(old);

        var docs = Workspace.CopyableDocuments(_tmp, old);
        Assert.Equal(new[] { deed }, docs);

        var p = w.CreateProject(new NewProjectRequest
        {
            ClientName = "احمدی", ClientId = "c1", OwnerIsClient = false, Owner = "خانم کریمی", Location = "کرج", Type = "تفکیک",
            Slug = "Ahmadi Karaj!", FromProject = "1404-087", DocsToCopy = docs,
        }, 1405, "UTM");

        Assert.Equal("1405-001", p.Number);
        Assert.Equal("c1", p.ClientId);
        Assert.Equal("خانم کریمی", p.Owner);
        Assert.Equal("1405/1405-001_Ahmadi_Karaj", p.Folder);
        var dir = p.FullPath(_tmp);
        Assert.True(File.Exists(Path.Combine(dir, "01_Documents", "Deed_Scan.pdf")));
        Assert.Equal(new[] { "01_Documents" }, Directory.GetDirectories(dir).Select(Path.GetFileName)); // فقط پوشه لازم
        Assert.Contains("1404-087", w.RelatedTo(p).Select(x => x.Number));
        Assert.Single(w.Clients); // متقاضی تکراری ساخته نشد
        Assert.Equal(2, Workspace.Load(_tmp).Projects.Count);
    }

    [Fact]
    public void CreateProject_makes_new_client_when_unknown()
    {
        var w = new Workspace(_tmp);
        var p = w.CreateProject(new NewProjectRequest { ClientName = "شرکت نو", ClientKind = "حقوقی", ClientPhone = "021", Location = "ملارد", Type = "توپوگرافی", Slug = "Nou" }, 1405, "UTM");
        var c = Assert.Single(w.Clients);
        Assert.Equal(("c1", "حقوقی", "021"), (c.Id, c.Kind, c.Phone));
        Assert.Equal("شرکت نو", p.Owner);
        Assert.Throws<ArgumentException>(() => w.CreateProject(new NewProjectRequest { ClientName = " " }, 1405, "UTM"));
    }

    // ---------- افزودن هوشمند فایل ----------
    [Theory]
    [InlineData("1405-014_Plan_v03.dwg", "04_CAD")]
    [InlineData("TS_day3.job", "02_Field_Raw")]
    [InlineData("gps.25o", "02_Field_Raw")]
    [InlineData("IMG_0412.JPG", "07_Photos")]
    [InlineData("Deed_Scan.jpg", "01_Documents")]
    [InlineData("points_final.csv", "03_Processing")]
    [InlineData("TS_raw_export.csv", "02_Field_Raw")]
    [InlineData("Final_Signed.pdf", "05_Final_PDF")]
    [InlineData("UTM_Sketch.pdf", "06_Registration")]
    [InlineData("Registry_Inquiry.pdf", "01_Documents")]
    [InlineData("Letter_1405.pdf", null)]
    [InlineData("archive.zip", null)]
    public void Classifier_suggests_folder(string name, string? folder) =>
        Assert.Equal(folder, FileClassifier.Classify(name).Folder);

    [Fact]
    public void ImportPlan_new_folder_conflict_and_ask()
    {
        var proj = Dir("p");
        Directory.CreateDirectory(proj);
        Touch(Path.Combine(proj, "07_Photos", "IMG_1.jpg"));
        var a = Touch(Dir("src", "IMG_1.jpg")); var b = Touch(Dir("src", "TS_day3.job")); var c = Touch(Dir("src", "Letter.pdf"));
        var items = ImportPlanner.Plan(proj, new[] { a, b, c });
        Assert.True(items[0].Conflict); Assert.False(items[0].Selected);
        Assert.True(items[1].NewFolder); Assert.True(items[1].Selected);
        Assert.Null(items[2].Folder); Assert.False(items[2].Selected);

        items[2].Folder = "01_Documents";
        ImportPlanner.Refresh(proj, items[2]);
        items[2].Selected = true;
        var r = ImportPlanner.Execute(proj, items);
        Assert.Equal(new[] { "TS_day3.job", "Letter.pdf" }, r.Copied);
        Assert.Empty(r.Skipped); // مورد تکراری انتخاب نشده بود و دست‌نخورده ماند
        Assert.Equal(new[] { "02_Field_Raw", "01_Documents" }, r.CreatedFolders);
        Assert.True(File.GetAttributes(Path.Combine(proj, "02_Field_Raw", "TS_day3.job")).HasFlag(FileAttributes.ReadOnly));
        Assert.False(Directory.Exists(Path.Combine(proj, "04_CAD"))); // پوشه‌های بدون فایل ساخته نشدند
    }

    [Fact]
    public void ImportPlan_versions_cad_and_archives_older()
    {
        var proj = Dir("p");
        Touch(Path.Combine(proj, "04_CAD", "1405-014_Plan_v02.dwg"));
        Touch(Path.Combine(proj, "04_CAD", "1405-014_Plan_v03.dwg"));
        Touch(Path.Combine(proj, "04_CAD", "1405-014_Boundary.dxf"));
        var v4 = Touch(Dir("src", "Plan_v04.dwg"));
        var plain = Touch(Dir("src2", "1405-014_Plan.dwg"));

        var item = ImportPlanner.Plan(proj, new[] { v4 })[0];
        Assert.Equal("Plan_v04.dwg", item.TargetName);
        Assert.Equal(2, item.ArchiveOlder.Count); // v02 و v03 (پیشوند شماره پروژه نادیده گرفته شد)
        Assert.Contains("v04", item.VersionNote);

        var item2 = ImportPlanner.Plan(proj, new[] { plain })[0];
        Assert.Equal("1405-014_Plan_v04.dwg", item2.TargetName); // بدون شماره نسخه: خودکار v04
        Assert.Equal(2, item2.ArchiveOlder.Count);

        var r = ImportPlanner.Execute(proj, new[] { item2 });
        Assert.Equal(new[] { "1405-014_Plan_v04.dwg" }, r.Copied);
        Assert.Equal(2, r.Archived.Count);
        Assert.True(File.Exists(Path.Combine(proj, "99_Archive", "1405-014_Plan_v03.dwg")));
        Assert.False(File.Exists(Path.Combine(proj, "04_CAD", "1405-014_Plan_v03.dwg")));
        Assert.True(File.Exists(Path.Combine(proj, "04_CAD", "1405-014_Boundary.dxf"))); // فایل نامرتبط دست‌نخورده
    }

    // ---------- پشتیبان ----------
    [Fact]
    public void Backup_is_incremental_and_never_deletes()
    {
        var root = Dir("root"); var dest = Dir("backup");
        var a = Touch(Path.Combine(root, "p1", "a.dwg"), "one");
        Touch(Path.Combine(root, "p1", "02_Field_Raw", "raw.job"), "raw");
        FileOps.MakeReadOnly(Path.Combine(root, "p1", "02_Field_Raw", "raw.job"));

        var r1 = BackupService.Run(root, dest);
        Assert.Equal((2, 0), (r1.NewFiles, r1.Updated));
        Assert.Equal("one", File.ReadAllText(Path.Combine(dest, "p1", "a.dwg")));

        var r2 = BackupService.Run(root, dest);
        Assert.Equal((0, 0, 2), (r2.NewFiles, r2.Updated, r2.Unchanged));

        File.WriteAllText(a, "changed-longer");
        Touch(Path.Combine(root, "p2", "b.pdf"));
        var rawSrc = Path.Combine(root, "p1", "02_Field_Raw", "raw.job");
        File.SetAttributes(rawSrc, FileAttributes.Normal);
        File.Delete(rawSrc);
        var r3 = BackupService.Run(root, dest);
        Assert.Equal((1, 1), (r3.NewFiles, r3.Updated));
        Assert.True(File.Exists(Path.Combine(dest, "p1", "02_Field_Raw", "raw.job"))); // حذف از مبدأ، از پشتیبان پاک نمی‌کند
        Assert.Equal(3, BackupService.History(dest).Count);
        Assert.Throws<ArgumentException>(() => BackupService.Run(root, Dir("root", "inside")));
        Assert.Throws<ArgumentException>(() => BackupService.Run(root, root));
        Assert.Throws<ArgumentException>(() => BackupService.Run(root, ""));
    }

    // ---------- ورود پوشه‌های قدیمی ----------
    [Fact]
    public void Legacy_scan_register_in_place_and_copy_into_standard()
    {
        var legacy = Dir("Old");
        Touch(Path.Combine(legacy, "Old_Karimi_Plot12", "plan.dwg"));
        Touch(Path.Combine(legacy, "Old_Karimi_Plot12", "scans", "deed.pdf"));
        Touch(Path.Combine(legacy, "Ahmadi_Karaj_8812", "IMG_1.jpg"));
        Touch(Path.Combine(legacy, "Backup_2022", "x.zip"));
        Directory.CreateDirectory(Path.Combine(legacy, "Empty"));
        var found = LegacyScanner.Scan(legacy);
        Assert.Equal(new[] { "Ahmadi_Karaj_8812", "Backup_2022", "Empty", "Old_Karimi_Plot12" }, found.Select(f => f.FolderName).ToArray());
        Assert.Equal(new[] { true, false, false, true }, found.Select(f => f.LooksLikeProject).ToArray());
        Assert.Equal("Ahmadi Karaj", found.First(f => f.FolderName.StartsWith("Ahmadi")).Owner);

        var root = Dir("root");
        Directory.CreateDirectory(root);
        var w = new Workspace(root);
        var inPlace = w.ImportLegacy(found.Where(f => f.FolderName.StartsWith("Ahmadi")), LegacyMode.RegisterInPlace, 1405, "UTM");
        var p = Assert.Single(inPlace);
        Assert.Equal(Path.Combine(legacy, "Ahmadi_Karaj_8812"), p.FullPath(root)); // پوشه همان‌جا ماند
        Assert.True(Directory.Exists(Path.Combine(legacy, "Ahmadi_Karaj_8812")));

        var copied = w.ImportLegacy(found.Where(f => f.FolderName.StartsWith("Old_")), LegacyMode.CopyIntoStandard, 1405, "UTM");
        var dir = Assert.Single(copied).FullPath(root);
        Assert.True(File.Exists(Path.Combine(dir, "04_CAD", "plan.dwg")));
        Assert.True(File.Exists(Path.Combine(dir, "01_Documents", "deed.pdf")));
        Assert.True(File.Exists(Path.Combine(legacy, "Old_Karimi_Plot12", "plan.dwg"))); // مبدأ دست‌نخورده
        Assert.Equal(2, Workspace.Load(root).Projects.Count);
    }

    // ---------- جستجو ----------
    [Fact]
    public void Search_finds_clients_projects_and_files_current_project_first()
    {
        var w = new Workspace(_tmp);
        w.Clients.Add(new Client { Id = "c1", Name = "آقای احمدی", Phone = "0912 345" });
        foreach (var (n, slug) in new[] { ("1405-001", "A"), ("1405-002", "B") })
        {
            var p = new Project { Number = n, ClientId = "c1", Owner = "احمدی", Location = "کرج", Type = "تفکیک", Folder = $"1405/{n}_{slug}" };
            w.Projects.Add(p);
            Touch(Path.Combine(p.FullPath(_tmp), "04_CAD", $"{n}_Plan_v03.dwg"));
        }
        var idx = FileIndex.Build(w);
        Assert.Equal(2, idx.Count);
        Assert.Equal("04_CAD", idx[0].Folder);

        var r = FileIndex.Search(w, idx, "plan", currentProject: "1405-002");
        Assert.Equal("1405-002", r.Files[0].ProjectNumber);
        Assert.Equal(2, r.Files.Count);
        Assert.Single(FileIndex.Search(w, idx, "۰۹۱۲").Clients);   // ارقام فارسی
        Assert.Equal(2, FileIndex.Search(w, idx, "احمدی").Projects.Count);
        Assert.Empty(FileIndex.Search(w, idx, "zzz").Files);
    }
}
