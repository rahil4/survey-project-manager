"""ساخت ساختار پوشه پروژه روی دیسک."""

from __future__ import annotations

from pathlib import Path

from .storage import Project

# (نام پوشه، توضیح فارسی، فقط‌خواندنی؟)
SUBFOLDERS: list[tuple[str, str, bool]] = [
    ("01_Documents", "اسناد دریافتی", False),
    ("02_Field_Raw", "خام توتال / GPS / دوربین", True),
    ("03_Processing", "پردازش", False),
    ("04_CAD", "فایل‌های اتوکد", False),
    ("05_Final_PDF", "خروجی نهایی", False),
    ("06_Registration", "مدارک ثبتی", False),
    ("07_Photos", "عکس‌ها", False),
    ("99_Archive", "نسخه‌های قدیمی", False),
]
RAW_FOLDER = "02_Field_Raw"

README = """\
Project : {number}
Owner   : {owner}
Location: {location}
Type    : {type}
CRS     : {crs}
Control : (write base points / benchmarks here)

02_Field_Raw holds raw total-station / GPS / camera files.
Do not edit them: they are the reference for the whole job.
"""


def folder_relpath(number: str, slug: str) -> str:
    return f"{number.split('-')[0]}/{number}_{slug}"


def create_project_folders(root: Path, project: Project, crs: str) -> Path:
    """پوشه پروژه و زیرپوشه‌ها را می‌سازد. اگر پوشه از قبل هست خطا می‌دهد."""
    base = project.path(root)
    if base.exists():
        raise FileExistsError(str(base))
    for name, _desc, _ro in SUBFOLDERS:
        (base / name).mkdir(parents=True)
    (base / "README.txt").write_text(
        README.format(
            number=project.number,
            owner=project.owner,
            location=project.location,
            type=project.type,
            crs=crs,
        ),
        encoding="utf-8",
    )
    return base


def is_in_raw_folder(project_dir: Path, path: Path) -> bool:
    try:
        rel = path.resolve().relative_to(project_dir.resolve())
    except ValueError:
        return False
    return bool(rel.parts) and rel.parts[0] == RAW_FOLDER
