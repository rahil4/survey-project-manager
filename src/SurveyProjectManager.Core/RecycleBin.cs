using System.Runtime.InteropServices;

namespace SurveyProjectManager.Core;

/// <summary>فرستادن پوشه به سطل بازیافت ویندوز (قابل بازگردانی).</summary>
public static class RecycleBin
{
    public static bool IsSupported => OperatingSystem.IsWindows();

    private const uint FO_DELETE = 3;
    private const ushort FOF_SILENT = 0x4;
    private const ushort FOF_NOCONFIRMATION = 0x10;
    private const ushort FOF_ALLOWUNDO = 0x40;
    private const ushort FOF_NOERRORUI = 0x400;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    public static void Send(string path)
    {
        if (!IsSupported) throw new PlatformNotSupportedException("سطل بازیافت فقط در ویندوز در دسترس است.");
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = Path.GetFullPath(path).TrimEnd('\\') + "\0\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
        };
        var result = SHFileOperation(ref op);
        if (result != 0 || op.fAnyOperationsAborted)
            throw new IOException($"انتقال به سطل بازیافت انجام نشد (کد {result:X}).");
    }
}
