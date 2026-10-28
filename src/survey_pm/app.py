from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import QApplication, QFileDialog, QMessageBox

from .config import load_config, save_config
from .ui.main_window import MainWindow
from .ui.styles import STYLESHEET


def _choose_root(parent=None) -> Path | None:
    QMessageBox.information(
        parent, "پوشه‌یار نقشه‌برداری",
        "برای شروع، پوشه‌ای را که می‌خواهید پروژه‌ها در آن ساخته شوند انتخاب کنید (مثلاً D:\\Projects).",
    )
    folder = QFileDialog.getExistingDirectory(parent, "پوشه اصلی پروژه‌ها")
    return Path(folder) if folder else None


def build_app(argv: list[str]) -> QApplication:
    app = QApplication(argv)
    app.setLayoutDirection(Qt.RightToLeft)
    app.setApplicationName("SurveyProjectManager")
    font = QFont("Segoe UI", 10)
    font.setFamilies(["Vazirmatn", "Segoe UI", "Tahoma"])
    app.setFont(font)
    app.setStyleSheet(STYLESHEET)
    return app


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="survey_pm")
    parser.add_argument("--root", help="پوشه اصلی پروژه‌ها (به‌جای مقدار ذخیره‌شده)")
    args, qt_args = parser.parse_known_args(argv if argv is not None else sys.argv[1:])
    app = build_app([sys.argv[0], *qt_args])

    cfg = load_config()
    if args.root:
        cfg.root = args.root
    if not cfg.root or not Path(cfg.root).is_dir():
        chosen = _choose_root()
        if chosen is None:
            return 0
        cfg.root = str(chosen)
    if not args.root:
        save_config(cfg)
    Path(cfg.root).mkdir(parents=True, exist_ok=True)

    window = MainWindow(cfg)
    window.show()
    return app.exec()
