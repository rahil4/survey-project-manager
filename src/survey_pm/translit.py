"""تبدیل ساده حروف فارسی به لاتین برای ساختن نام پوشه."""

from __future__ import annotations

import re

_MAP = {
    "آ": "A", "ا": "a", "ب": "b", "پ": "p", "ت": "t", "ث": "s", "ج": "j",
    "چ": "ch", "ح": "h", "خ": "kh", "د": "d", "ذ": "z", "ر": "r", "ز": "z",
    "ژ": "zh", "س": "s", "ش": "sh", "ص": "s", "ض": "z", "ط": "t", "ظ": "z",
    "ع": "a", "غ": "gh", "ف": "f", "ق": "gh", "ک": "k", "ك": "k", "گ": "g",
    "ل": "l", "م": "m", "ن": "n", "و": "v", "ه": "h", "ی": "y", "ي": "y",
    "ئ": "e", "ء": "", "ؤ": "o", "أ": "a", "إ": "e", "ة": "h",
}
_STRIP = "‌ًٌٍَُِّْ"
_HONORIFICS = ("آقای", "خانم", "شرکت", "مهندس", "دکتر", "اقای")


def to_latin(text: str) -> str:
    """نام را به حروف لاتین (فقط A-Z، 0-9 و _) تبدیل می‌کند."""
    for word in _HONORIFICS:
        text = text.replace(word, " ")
    out: list[str] = []
    for ch in text:
        if ch in _STRIP:
            out.append(" " if ch == "‌" else "")
        elif ch in _MAP:
            out.append(_MAP[ch])
        elif ch.isascii() and ch.isalnum():
            out.append(ch)
        else:
            out.append(" ")
    words = "".join(out).split()
    return "_".join(w[:1].upper() + w[1:] for w in words)


def folder_slug(owner: str, location: str) -> str:
    """نام لاتین پوشه از روی نام مالک و شهر/محل (قبل از ویرگول)."""
    city = re.split(r"[،,]", location)[0]
    parts = [p for p in (to_latin(owner), to_latin(city)) if p]
    return "_".join(parts) or "Project"
