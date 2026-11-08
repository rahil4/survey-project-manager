using Avalonia;
using Avalonia.Headless;
using SurveyProjectManager.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace SurveyProjectManager.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SurveyProjectManager.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
