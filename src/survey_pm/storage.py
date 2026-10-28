"""فهرست پروژه‌ها در فایل projects.csv (قابل‌باز شدن در اکسل)."""

from __future__ import annotations

import csv
from dataclasses import dataclass, fields
from pathlib import Path

CSV_NAME = "projects.csv"

STATUSES: list[tuple[str, str]] = [
    ("collect", "دریافت مدارک"),
    ("field", "برداشت"),
    ("draw", "ترسیم"),
    ("check", "کنترل"),
    ("done", "تحویل‌شده"),
    ("reg", "در ثبت"),
]
STATUS_LABEL = dict(STATUSES)

PROJECT_TYPES = [
    "تفکیک",
    "افراز",
    "ثبتی / تحدید حدود",
    "توپوگرافی",
    "پیاده‌سازی",
    "پایان‌کار",
    "سایر",
]


@dataclass
class Project:
    number: str  # مثل 1405-014
    owner: str
    location: str
    type: str
    status: str = "collect"
    due: str = ""
    folder: str = ""  # مسیر نسبی از ریشه، با / (مثل 1405/1405-014_Ahmadi_Karaj)
    note: str = ""

    @property
    def status_label(self) -> str:
        return STATUS_LABEL.get(self.status, self.status)

    @property
    def year(self) -> str:
        return self.number.split("-")[0]

    def path(self, root: Path) -> Path:
        return root / Path(*self.folder.split("/"))


_FIELDS = [f.name for f in fields(Project)]


def csv_path(root: Path) -> Path:
    return root / CSV_NAME


def load_projects(root: Path) -> list[Project]:
    path = csv_path(root)
    if not path.exists():
        return []
    with path.open(newline="", encoding="utf-8-sig") as fh:
        rows = csv.DictReader(fh)
        return [Project(**{k: (row.get(k) or "") for k in _FIELDS}) for row in rows]


def save_projects(root: Path, projects: list[Project]) -> None:
    """ابتدا در فایل موقت می‌نویسد تا اگر اکسل فایل را باز کرده باشد خراب نشود."""
    path = csv_path(root)
    tmp = path.with_suffix(".csv.tmp")
    with tmp.open("w", newline="", encoding="utf-8-sig") as fh:
        writer = csv.DictWriter(fh, fieldnames=_FIELDS)
        writer.writeheader()
        for p in projects:
            writer.writerow({k: getattr(p, k) for k in _FIELDS})
    tmp.replace(path)


def next_number(projects: list[Project], year: int) -> str:
    prefix = f"{year}-"
    used = [
        int(p.number[len(prefix):])
        for p in projects
        if p.number.startswith(prefix) and p.number[len(prefix):].isdigit()
    ]
    return f"{year}-{max(used, default=0) + 1:03d}"
