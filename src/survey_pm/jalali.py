"""تبدیل تاریخ میلادی به شمسی (الگوریتم جلالی)."""

from __future__ import annotations

import datetime as dt

_G_DAYS = (0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334)


def gregorian_to_jalali(gy: int, gm: int, gd: int) -> tuple[int, int, int]:
    gy2 = gy + 1 if gm > 2 else gy
    days = (
        355666
        + 365 * gy
        + (gy2 + 3) // 4
        - (gy2 + 99) // 100
        + (gy2 + 399) // 400
        + gd
        + _G_DAYS[gm - 1]
    )
    jy = -1595 + 33 * (days // 12053)
    days %= 12053
    jy += 4 * (days // 1461)
    days %= 1461
    if days > 365:
        jy += (days - 1) // 365
        days = (days - 1) % 365
    if days < 186:
        jm = 1 + days // 31
        jd = 1 + days % 31
    else:
        jm = 7 + (days - 186) // 30
        jd = 1 + (days - 186) % 30
    return jy, jm, jd


def today_jalali(today: dt.date | None = None) -> tuple[int, int, int]:
    today = today or dt.date.today()
    return gregorian_to_jalali(today.year, today.month, today.day)


def format_jalali(parts: tuple[int, int, int]) -> str:
    y, m, d = parts
    return f"{y:04d}/{m:02d}/{d:02d}"


def mtime_to_jalali(timestamp: float) -> str:
    return format_jalali(today_jalali(dt.date.fromtimestamp(timestamp)))
