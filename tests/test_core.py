import datetime as dt

from survey_pm.config import Config, load_config, save_config
from survey_pm.jalali import format_jalali, gregorian_to_jalali, today_jalali
from survey_pm.projects import (
    RAW_FOLDER,
    SUBFOLDERS,
    create_project_folders,
    folder_relpath,
    is_in_raw_folder,
)
from survey_pm.storage import Project, load_projects, next_number, save_projects
from survey_pm.translit import folder_slug, to_latin


def test_jalali_known_dates():
    assert gregorian_to_jalali(2026, 10, 28) == (1405, 8, 6)
    assert gregorian_to_jalali(2025, 3, 21) == (1404, 1, 1)
    assert gregorian_to_jalali(2024, 3, 20) == (1403, 1, 1)
    assert format_jalali(today_jalali(dt.date(2026, 3, 21))) == "1405/01/01"


def test_translit():
    assert to_latin("احمدی") == "Ahmdy"
    assert folder_slug("آقای رضایی", "کرج، پلاک ۱۲") == "Rzayy_Krj"
    assert folder_slug("", "") == "Project"


def test_next_number():
    ps = [Project("1405-001", "a", "b", "c"), Project("1405-009", "a", "b", "c"),
          Project("1404-120", "a", "b", "c")]
    assert next_number(ps, 1405) == "1405-010"
    assert next_number(ps, 1406) == "1406-001"
    assert next_number([], 1405) == "1405-001"


def test_csv_roundtrip(tmp_path):
    ps = [Project("1405-001", "آقای احمدی", "کرج، پلاک ۱", "تفکیک", "draw",
                  "1405/08/15", "1405/1405-001_Ahmdy_Kraj", "یادداشت, با ویرگول")]
    save_projects(tmp_path, ps)
    assert load_projects(tmp_path) == ps
    assert load_projects(tmp_path / "missing") == []


def test_create_folders_and_raw_guard(tmp_path):
    p = Project("1405-002", "رضایی", "شهریار", "افراز",
                folder=folder_relpath("1405-002", "Rzaay_Shhryar"))
    base = create_project_folders(tmp_path, p, "UTM39")
    assert sorted(d.name for d in base.iterdir() if d.is_dir()) == sorted(n for n, _, _ in SUBFOLDERS)
    assert "UTM39" in (base / "README.txt").read_text(encoding="utf-8")
    assert is_in_raw_folder(base, base / RAW_FOLDER / "x.job")
    assert not is_in_raw_folder(base, base / "04_CAD" / "x.dwg")
    assert not is_in_raw_folder(base, tmp_path / "other")


def test_config_roundtrip(tmp_path):
    assert load_config(tmp_path).root == ""
    save_config(Config(root="D:/Projects", crs="X"), tmp_path)
    assert load_config(tmp_path) == Config(root="D:/Projects", crs="X")
