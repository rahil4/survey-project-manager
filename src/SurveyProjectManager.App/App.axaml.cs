using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SurveyProjectManager.Core;

namespace SurveyProjectManager.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? Array.Empty<string>();
            var rootArg = ParseRootArg(args);
            var cfg = AppConfig.Load();
            var fromArgs = rootArg != null;
            if (fromArgs) cfg.Root = rootArg!;
            desktop.MainWindow = new MainWindow(cfg, persistConfig: !fromArgs);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static string? ParseRootArg(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "--root") return args[i + 1];
        return null;
    }
}
