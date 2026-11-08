using System.Text;

namespace SurveyProjectManager.Core;

/// <summary>تبدیل ساده حروف فارسی به لاتین برای ساختن نام پوشه.</summary>
public static class Translit
{
    private static readonly Dictionary<char, string> Map = new()
    {
        ['آ'] = "A", ['ا'] = "a", ['ب'] = "b", ['پ'] = "p", ['ت'] = "t", ['ث'] = "s", ['ج'] = "j",
        ['چ'] = "ch", ['ح'] = "h", ['خ'] = "kh", ['د'] = "d", ['ذ'] = "z", ['ر'] = "r", ['ز'] = "z",
        ['ژ'] = "zh", ['س'] = "s", ['ش'] = "sh", ['ص'] = "s", ['ض'] = "z", ['ط'] = "t", ['ظ'] = "z",
        ['ع'] = "a", ['غ'] = "gh", ['ف'] = "f", ['ق'] = "gh", ['ک'] = "k", ['ك'] = "k", ['گ'] = "g",
        ['ل'] = "l", ['م'] = "m", ['ن'] = "n", ['و'] = "v", ['ه'] = "h", ['ی'] = "y", ['ي'] = "y",
        ['ئ'] = "e", ['ء'] = "", ['ؤ'] = "o", ['أ'] = "a", ['إ'] = "e", ['ة'] = "h",
    };

    private const string Strip = "‌ًٌٍَُِّْ";
    private static readonly string[] Honorifics = { "آقای", "خانم", "شرکت", "مهندس", "دکتر", "اقای" };

    /// <summary>نام را به حروف لاتین (فقط A-Z، 0-9 و _) تبدیل می‌کند.</summary>
    public static string ToLatin(string text)
    {
        foreach (var word in Honorifics)
            text = text.Replace(word, " ");
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (Strip.Contains(ch)) sb.Append(ch == '‌' ? " " : "");
            else if (Map.TryGetValue(ch, out var latin)) sb.Append(latin);
            else if (char.IsAsciiLetterOrDigit(ch)) sb.Append(ch);
            else sb.Append(' ');
        }
        var words = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("_", words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    /// <summary>نام لاتین پوشه از روی نام مالک و شهر/محل (قبل از ویرگول).</summary>
    public static string FolderSlug(string owner, string location)
    {
        var city = location.Split('،', ',')[0];
        var parts = new[] { ToLatin(owner), ToLatin(city) }.Where(p => p.Length > 0);
        var slug = string.Join("_", parts);
        return slug.Length > 0 ? slug : "Project";
    }

    /// <summary>هر نویسه غیرمجاز نام پوشه را با _ عوض می‌کند.</summary>
    public static string SanitizeSlug(string text)
    {
        var cleaned = new string(text.Trim().Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        cleaned = cleaned.Trim('_');
        return cleaned.Length > 0 ? cleaned : "Project";
    }
}
