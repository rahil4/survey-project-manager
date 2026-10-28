import os

import pytest

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")
pytest.importorskip("PySide6")

from pathlib import Path  # noqa: E402

from PySide6.QtCore import QMimeData, QUrl  # noqa: E402

from survey_pm.app import build_app  # noqa: E402
from survey_pm.config import Config  # noqa: E402
from survey_pm.storage import Project, load_projects  # noqa: E402
from survey_pm.ui.main_window import MainWindow  # noqa: E402
from survey_pm.ui.project_dialog import ProjectDialog  # noqa: E402


@pytest.fixture(scope="module")
def app():
    return build_app(["test"])


def _make_window(tmp_path, app):
    return MainWindow(Config(root=str(tmp_path / "Projects")), config_dir=tmp_path / "cfg")


def _add_project(win, tmp_path):
    (tmp_path / "Projects").mkdir(exist_ok=True)
    dlg = ProjectDialog(win, number="1405-001", root_text=str(win.root))
    dlg.owner.setText("آقای احمدی")
    dlg.location.setText("کرج، پلاک ۱")
    p = Project(number="1405-001", owner="", location="", type="")
    dlg.apply_to(p)
    from survey_pm.projects import create_project_folders, folder_relpath
    from survey_pm.storage import save_projects

    p.folder = folder_relpath(p.number, dlg.slug_text())
    create_project_folders(win.root, p, "UTM39")
    save_projects(win.root, [p])
    win.reload(select=p.number)
    return p


def test_dialog_slug_and_validation(app, tmp_path):
    win = _make_window(tmp_path, app)
    dlg = ProjectDialog(win, number="1405-007", root_text="D:\\Projects")
    dlg.owner.setText("خانم رضایی")
    dlg.location.setText("شهریار، پلاک ۱")
    assert dlg.slug_text() == "Rzayy_Shhryar"
    assert "1405-007_Rzayy_Shhryar" in dlg.preview.text()
    dlg.slug.setText("Custom Name!")
    dlg.slug.textEdited.emit("Custom Name!")
    assert dlg.slug_text() == "Custom_Name"


def test_enter_project_and_drop(app, tmp_path, monkeypatch):
    from PySide6.QtWidgets import QMessageBox

    warned = []
    monkeypatch.setattr(QMessageBox, "warning", lambda *a, **k: warned.append(a[2]))
    win = _make_window(tmp_path, app)
    p = _add_project(win, tmp_path)
    assert win.current_project() == p
    win.enter_project()
    view = win.project_view
    assert win.stack.currentWidget() is view
    assert view.current == p.path(win.root)

    cad = view.project_dir / "04_CAD"
    view.navigate(cad)
    assert view.title.text() == "04_CAD"
    src = tmp_path / "plan.dwg"
    src.write_text("x")
    view._on_drop([src])
    assert (cad / "plan.dwg").exists()
    view._on_drop([src])  # تکراری: رونویسی نمی‌شود
    assert (cad / "plan.dwg").read_text() == "x"
    assert warned and "plan.dwg" in warned[0]

    raw = view.project_dir / "02_Field_Raw"
    view.navigate(raw)
    view._on_drop([src])
    assert not os.access(raw / "plan.dwg", os.W_OK) or os.geteuid() == 0
    view.go_up()
    assert view.current == view.project_dir
    win.show_list()
    assert win.stack.currentIndex() == 0


def test_status_change_persists(app, tmp_path):
    win = _make_window(tmp_path, app)
    p = _add_project(win, tmp_path)
    win.set_status(win.projects[0], "draw")
    assert load_projects(win.root)[0].status == "draw"
