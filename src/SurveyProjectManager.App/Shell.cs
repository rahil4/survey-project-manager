using System.Diagnostics;
using Avalonia.Controls;

namespace SurveyProjectManager.App;

/// <summary>کارهای مربوط به سیستم‌عامل: بازکردن فایل، نمایش در Explorer، کپی در کلیپ‌بورد.</summary>
public static class Shell
{
    /// <summary>فایل را با برنامه پیش‌فرض (اتوکد، PDF‌خوان، ...) یا پوشه را در Explorer باز می‌کند.</summary>
    public static void Open(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else
                Process.Start(OperatingSystem.IsMacOS() ? "open" : "xdg-open", new[] { path });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // برنامه‌ای برای این نوع فایل نصب نیست؛ کاربر می‌تواند «نمایش در Explorer» را بزند.
        }
    }

    /// <summary>فایل را در Explorer انتخاب‌شده نشان می‌دهد؛ برای پوشه، خود پوشه را باز می‌کند.</summary>
    public static void Reveal(string path)
    {
        if (OperatingSystem.IsWindows() && (File.Exists(path) || Directory.Exists(path)))
        {
            var args = Directory.Exists(path) ? $"\"{path}\"" : $"/select,\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
        }
        else
        {
            Open(Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path);
        }
    }

    public static async Task CopyTextAsync(Control owner, string text)
    {
        var clipboard = TopLevel.GetTopLevel(owner)?.Clipboard;
        if (clipboard != null) await clipboard.SetTextAsync(text);
    }
}
