using System.Text.RegularExpressions;

namespace SurveyProjectManager.Core;

public sealed record Suggestion(string? Folder, string Reason);

/// <summary>پیشنهاد پوشه مقصد بر اساس پسوند و نام فایل.</summary>
public static partial class FileClassifier
{
    private static readonly HashSet<string> Cad = new() { "dwg", "dxf", "dwt" };
    private static readonly HashSet<string> Raw = new() { "job", "jxl", "jobxml", "rw5", "rtk", "sdr", "raw", "gsi", "rnx", "crx", "ubx", "obs", "nav", "t01", "t02" };
    private static readonly HashSet<string> Photos = new() { "jpg", "jpeg", "png", "heic", "tif", "tiff", "bmp", "gif" };
    private static readonly HashSet<string> Tables = new() { "csv", "xlsx", "xls", "ods", "pts", "xyz", "txt" };

    private static readonly string[] FinalWords = { "final", "signed", "نهایی", "امضا" };
    private static readonly string[] RegistrationWords = { "utm", "sketch", "کروکی", "form", "فرم", "تحدید", "ثبت", "registration" };
    private static readonly string[] DocumentWords = { "deed", "سند", "شناسنامه", "national", "inquiry", "استعلام", "contract", "قرارداد", "id_", "id-", "copy", "scan" };

    [GeneratedRegex(@"(?<![a-z])(raw|field|ts|gps|rover|base)(?![a-z])")]
    private static partial Regex RawNameRx();

    [GeneratedRegex(@"^\d\d[ongpl]$", RegexOptions.IgnoreCase)]
    private static partial Regex RinexExt();

    private static bool Has(string name, string[] words) => words.Any(w => name.Contains(w, StringComparison.Ordinal));

    public static Suggestion Classify(string fileName, bool isDirectory = false)
    {
        if (isDirectory) return new Suggestion(null, "پوشه؛ مقصد را انتخاب کنید");
        var name = TextNorm.Normalize(fileName);
        var ext = Path.GetExtension(name).TrimStart('.');

        if (Cad.Contains(ext)) return new Suggestion("04_CAD", "فایل اتوکد");
        if (Raw.Contains(ext) || RinexExt().IsMatch(ext)) return new Suggestion("02_Field_Raw", "فایل خام توتال / GPS · بعد از کپی فقط‌خواندنی می‌شود");
        if (Photos.Contains(ext))
            return Has(name, DocumentWords) ? new Suggestion("01_Documents", "اسکن سند") : new Suggestion("07_Photos", "عکس");
        if (ext == "pdf")
        {
            if (Has(name, FinalWords)) return new Suggestion("05_Final_PDF", "نقشه نهایی");
            if (Has(name, RegistrationWords)) return new Suggestion("06_Registration", "مدارک ثبتی");
            if (Has(name, DocumentWords)) return new Suggestion("01_Documents", "سند یا مدرک دریافتی");
            return new Suggestion(null, "نوع مشخص نیست؛ پوشه را انتخاب کنید");
        }
        if (Tables.Contains(ext))
            return RawNameRx().IsMatch(name) ? new Suggestion("02_Field_Raw", "خروجی خام دستگاه · بعد از کپی فقط‌خواندنی می‌شود")
                                           : new Suggestion("03_Processing", "فایل نقاط یا محاسبات");
        return new Suggestion(null, "نوع مشخص نیست؛ پوشه را انتخاب کنید");
    }
}
