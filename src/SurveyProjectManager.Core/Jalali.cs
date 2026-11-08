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

    public static string Format(DateTime date)
    {
        var (y, m, d) = From(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }
}
