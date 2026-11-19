namespace SurveyProjectManager.Core;

/// <summary>تبدیل تاریخ میلادی به شمسی.</summary>
public static class Jalali
{
    private static readonly int[] GDays = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };

    public static (int Year, int Month, int Day) FromGregorian(int gy, int gm, int gd)
    {
        int gy2 = gm > 2 ? gy + 1 : gy;
        long days = 355666L + 365L * gy + (gy2 + 3) / 4 - (gy2 + 99) / 100 + (gy2 + 399) / 400 + gd + GDays[gm - 1];
        int jy = -1595 + 33 * (int)(days / 12053);
        days %= 12053;
        jy += 4 * (int)(days / 1461);
        days %= 1461;
        if (days > 365)
        {
            jy += (int)((days - 1) / 365);
            days = (days - 1) % 365;
        }
        int jm, jd;
        if (days < 186)
        {
            jm = 1 + (int)(days / 31);
            jd = 1 + (int)(days % 31);
        }
        else
        {
            jm = 7 + (int)((days - 186) / 30);
            jd = 1 + (int)((days - 186) % 30);
        }
        return (jy, jm, jd);
    }

    public static (int Year, int Month, int Day) From(DateTime date) => FromGregorian(date.Year, date.Month, date.Day);

    public static (int Year, int Month, int Day) Today() => From(DateTime.Today);

    public static readonly string[] MonthNames =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
    };

    public static DateTime ToGregorian(int jy, int jm, int jd)
    {
        jy += 1595;
        long days = -355668 + 365L * jy + (jy / 33) * 8 + ((jy % 33) + 3) / 4 + jd + (jm < 7 ? (jm - 1) * 31 : (jm - 7) * 30 + 186);
        int gy = 400 * (int)(days / 146097);
        days %= 146097;
        if (days > 36524)
        {
            gy += 100 * (int)(--days / 36524);
            days %= 36524;
            if (days >= 365) days++;
        }
        gy += 4 * (int)(days / 1461);
        days %= 1461;
        if (days > 365)
        {
            gy += (int)((days - 1) / 365);
            days = (days - 1) % 365;
        }
        int gd = (int)days + 1;
        var sal = new[] { 0, 31, (gy % 4 == 0 && gy % 100 != 0) || gy % 400 == 0 ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        int gm = 0;
        while (gm < 13 && gd > sal[gm]) gd -= sal[gm++];
        return new DateTime(gy, gm, gd);
    }

    /// <summary>متن «۱۴۰۵/۰۸/۲۸» (ارقام فارسی یا لاتین) را به تاریخ شمسی تبدیل می‌کند.</summary>
    public static bool TryParse(string? text, out (int Year, int Month, int Day) date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = TextNorm.AsciiDigits(text.Trim()).Split('/', '-', '.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var d))
            return false;
        if (y < 1200 || y > 1600 || m < 1 || m > 12 || d < 1 || d > (m <= 6 ? 31 : m <= 11 ? 30 : 30)) return false;
        date = (y, m, d);
        return true;
    }

    /// <summary>تعداد روز از امروز تا تاریخ تحویل؛ منفی یعنی عقب‌افتاده. اگر تاریخ خالی یا نامعتبر باشد null.</summary>
    public static int? DaysUntil(string? due, DateTime? today = null)
    {
        if (!TryParse(due, out var d)) return null;
        var target = ToGregorian(d.Year, d.Month, d.Day);
        return (int)(target.Date - (today ?? DateTime.Today).Date).TotalDays;
    }

    public static string Long(DateTime date)
    {
        var (y, m, d) = From(date);
        return $"{d} {MonthNames[m - 1]} {y}";
    }

    public static string Format(DateTime date)
    {
        var (y, m, d) = From(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }
}
