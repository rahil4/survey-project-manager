using System.Text;

namespace SurveyProjectManager.Core;

/// <summary>یکسان‌سازی متن فارسی برای جستجو و مقایسه نام‌ها.</summary>
public static class TextNorm
{
    private static readonly string[] Honorifics = { "آقای", "اقای", "خانم", "شرکت", "مهندس", "دکتر", "حاج", "حاجی" };

    /// <summary>ارقام فارسی و عربی را به لاتین تبدیل می‌کند.</summary>
    public static string AsciiDigits(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch is >= '۰' and <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (ch is >= '٠' and <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>حروف عربی→فارسی، ارقام→لاتین، حذف نیم‌فاصله و اعراب، حروف کوچک، فاصله‌های اضافه حذف می‌شود.</summary>
    public static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch0 in AsciiDigits(s))
        {
            var ch = ch0 switch { 'ك' => 'ک', 'ي' => 'ی', 'ى' => 'ی', 'ۀ' => 'ه', 'ة' => 'ه', 'أ' => 'ا', 'إ' => 'ا', _ => ch0 };
            if (ch is '‌' or '‍') { sb.Append(' '); continue; }
            if (ch is >= 'ً' and <= 'ْ') continue;
            sb.Append(char.ToLowerInvariant(ch));
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string StripHonorific(string normalized)
    {
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !Honorifics.Contains(w)).ToArray();
        return words.Length == 0 ? normalized : string.Join(' ', words);
    }

    public static bool Contains(string haystack, string query)
    {
        var q = Normalize(query);
        return q.Length == 0 || Normalize(haystack).Contains(q, StringComparison.Ordinal);
    }
}

/// <summary>تبدیل ارقام لاتین به فارسی برای نمایش (قابل خاموش شدن).</summary>
public static class Digits
{
    public static bool Persian { get; set; } = true;

    public static string Fa(string s)
    {
        if (!Persian || s.Length == 0) return s;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s) sb.Append(ch is >= '0' and <= '9' ? (char)('۰' + (ch - '0')) : ch);
        return sb.ToString();
    }

    public static string Fa(int n) => Fa(n.ToString());
}
