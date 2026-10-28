"""کارهای مربوط به ویندوز: بازکردن فایل، نمایش در Explorer، کپی در کلیپ‌بورد."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from PySide6.QtCore import QUrl
from PySide6.QtGui import QDesktopServices
from PySide6.QtWidgets import QApplication


def open_path(path: Path) -> None:
    """فایل را با برنامه پیش‌فرض (اتوکد، PDF‌خوان، ...) یا پوشه را در Explorer باز می‌کند."""
    QDesktopServices.openUrl(QUrl.fromLocalFile(str(path)))


def reveal(path: Path) -> None:
    """فایل را در Explorer انتخاب‌شده نشان می‌دهد؛ برای پوشه، خود پوشه را باز می‌کند."""
    if sys.platform == "win32" and path.exists():
        if path.is_dir():
            subprocess.Popen(["explorer", str(path)])
        else:
            subprocess.Popen(["explorer", f"/select,{path}"])
    else:
        open_path(path if path.is_dir() else path.parent)


def copy_text(text: str) -> None:
    QApplication.clipboard().setText(text)
