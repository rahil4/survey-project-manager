using SurveyProjectManager.Core;

namespace SurveyProjectManager.App;

/// <summary>وضعیت مشترک بین پنجره اصلی و صفحه‌ها: تنظیمات، پروژه‌ها، متقاضیان و ناوبری.</summary>
public sealed class AppState
{
    private List<FileEntry>? _index;

    public AppState(AppConfig cfg, bool persistConfig, Workspace workspace)
    {
        Cfg = cfg;
        PersistConfig = persistConfig;
        W = workspace;
    }

    public AppConfig Cfg { get; }
    public bool PersistConfig { get; }
    public Workspace W { get; set; }
    public string Root => W.Root;

    // ناوبری (توسط MainWindow تنظیم می‌شود)
    public Action<string, string?> Show { get; set; } = (_, _) => { };
    public Action<string, string?, string?> OpenProject { get; set; } = (_, _, _) => { };
    public Action RefreshAll { get; set; } = () => { };
    public Action<string> Toast { get; set; } = _ => { };
    public Func<string?, Task> NewProject { get; set; } = _ => Task.CompletedTask;
    public Func<Project, Task> EditProject { get; set; } = _ => Task.CompletedTask;
    public Func<Client?, Task> EditClient { get; set; } = _ => Task.CompletedTask;
    public Func<Task> ChooseRoot { get; set; } = () => Task.CompletedTask;

    public void SaveConfig()
    {
        if (PersistConfig) Cfg.Save();
    }

    public void Save(Avalonia.Controls.Window owner)
    {
        try { W.Save(); }
        catch (IOException e)
        {
            _ = Dialogs.MessageDialog.ShowAsync(owner, "ذخیره فهرست ممکن نشد",
                "اگر فایل projects.csv یا clients.csv در اکسل باز است آن را ببندید و دوباره امتحان کنید.\n" + e.Message);
        }
    }

    private Task<List<FileEntry>>? _indexTask;

    /// <summary>فهرست نام فایل‌ها؛ تا آماده نشده خالی است.</summary>
    public IReadOnlyList<FileEntry> Index => _index ?? new List<FileEntry>();

    /// <summary>ساخت فهرست فایل‌ها در پس‌زمینه (فقط یک بار تا زمانی که نامعتبر شود).</summary>
    public async Task EnsureIndexAsync()
    {
        if (_index != null) return;
        _indexTask ??= Task.Run(() => FileIndex.Build(W));
        _index = await _indexTask;
    }

    public void InvalidateIndex()
    {
        _index = null;
        _indexTask = null;
    }
}
