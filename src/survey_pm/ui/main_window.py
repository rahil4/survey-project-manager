"""پنجره اصلی: فهرست پروژه‌ها و ورود به هر پروژه."""

from __future__ import annotations

import shutil
from pathlib import Path

from PySide6.QtCore import QModelIndex, QSortFilterProxyModel, Qt
from PySide6.QtGui import QAction, QBrush, QColor, QStandardItem, QStandardItemModel
from PySide6.QtWidgets import (
    QAbstractItemView,
    QFileDialog,
    QHBoxLayout,
    QHeaderView,
    QLineEdit,
    QListWidget,
    QListWidgetItem,
    QMainWindow,
    QMenu,
    QMessageBox,
    QStackedWidget,
    QTableView,
    QToolBar,
    QVBoxLayout,
    QWidget,
)

from .. import __version__, shell
from ..config import Config, save_config
from ..jalali import today_jalali
from ..projects import create_project_folders, folder_relpath
from ..storage import (
    STATUS_LABEL,
    STATUSES,
    Project,
    csv_path,
    load_projects,
    next_number,
    save_projects,
)
from .project_dialog import ProjectDialog
from .project_view import ProjectView
from .styles import STATUS_COLORS

_COLUMNS = ("شماره", "مالک", "موقعیت / پلاک", "نوع کار", "وضعیت", "تحویل")


class _Filter(QSortFilterProxyModel):
    def __init__(self):
        super().__init__()
        self.status = ""
        self.text = ""

    def set_filters(self, status: str, text: str) -> None:
        self.status, self.text = status, text.strip()
        self.invalidate()

    def filterAcceptsRow(self, row: int, parent: QModelIndex) -> bool:
        model = self.sourceModel()
        first = model.item(row, 0)
        if self.status and first.data(Qt.UserRole + 1) != self.status:
            return False
        if self.text:
            haystack = " ".join(model.item(row, c).text() for c in range(model.columnCount()))
            return self.text in haystack
        return True


class MainWindow(QMainWindow):
    def __init__(self, cfg: Config, config_dir: Path | None = None):
        super().__init__()
        self.cfg = cfg
        self.config_dir = config_dir
        self.root = Path(cfg.root)
        self.projects: list[Project] = []
        self.setWindowTitle("پوشه‌یار نقشه‌برداری")
        self.resize(1280, 760)

        self.model = QStandardItemModel(0, len(_COLUMNS), self)
        self.model.setHorizontalHeaderLabels(_COLUMNS)
        self.proxy = _Filter()
        self.proxy.setSourceModel(self.model)

        self._build_toolbar()
        self.stack = QStackedWidget()
        self.stack.addWidget(self._build_list_page())
        self.project_view = ProjectView()
        self.project_view.backRequested.connect(self.show_list)
        self.stack.addWidget(self.project_view)
        container = QWidget()
        container.setObjectName("central")
        lay = QVBoxLayout(container)
        lay.addWidget(self.stack)
        self.setCentralWidget(container)
        self.reload()

    # ---- construction ----
    def _build_toolbar(self) -> None:
        bar = QToolBar()
        bar.setMovable(False)
        bar.setToolButtonStyle(Qt.ToolButtonTextOnly)
        self.addToolBar(Qt.TopToolBarArea, bar)
        self.toolbar = bar
        entries = [
            ("پروژه جدید", self.new_project),
            ("ویرایش", self.edit_project),
            ("بازکردن پوشه", self.reveal_project),
            ("کپی مسیر", self.copy_project_path),
            ("بایگانی ZIP", self.archive_project),
            ("فایل اکسل (CSV)", self.open_csv),
            ("تغییر پوشه اصلی", self.change_root),
        ]
        self.actions_: dict[str, QAction] = {}
        for text, slot in entries:
            act = bar.addAction(text)
            act.triggered.connect(lambda _=False, s=slot: s())
            self.actions_[text] = act

    def _build_list_page(self) -> QWidget:
        self.status_list = QListWidget()
        self.status_list.setFixedWidth(200)
        self.status_list.currentRowChanged.connect(self._apply_filter)

        self.search = QLineEdit()
        self.search.setPlaceholderText("جستجو: نام مالک، پلاک، شماره پروژه…")
        self.search.setClearButtonEnabled(True)
        self.search.textChanged.connect(self._apply_filter)

        self.table = QTableView()
        self.table.setModel(self.proxy)
        self.table.setSelectionBehavior(QAbstractItemView.SelectRows)
        self.table.setSelectionMode(QAbstractItemView.SingleSelection)
        self.table.setEditTriggers(QAbstractItemView.NoEditTriggers)
        self.table.setSortingEnabled(True)
        self.table.setShowGrid(False)
        self.table.verticalHeader().hide()
        self.table.setContextMenuPolicy(Qt.CustomContextMenu)
        hdr = self.table.horizontalHeader()
        hdr.setSectionResizeMode(QHeaderView.ResizeToContents)
        hdr.setSectionResizeMode(2, QHeaderView.Stretch)
        self.table.doubleClicked.connect(lambda _i: self.enter_project())
        self.table.customContextMenuRequested.connect(self._row_menu)
        self.table.activated.connect(lambda _i: self.enter_project())

        right = QVBoxLayout()
        right.addWidget(self.search)
        right.addWidget(self.table, 1)
        page = QWidget()
        row = QHBoxLayout(page)
        row.setContentsMargins(0, 0, 0, 0)
        row.addWidget(self.status_list)
        row.addLayout(right, 1)
        return page

    # ---- data ----
    def reload(self, select: str | None = None) -> None:
        self.projects = load_projects(self.root)
        self.projects.sort(key=lambda p: p.number, reverse=True)
        self.model.setRowCount(0)
        for p in self.projects:
            fg, bg = STATUS_COLORS.get(p.status, ("#000", "#fff"))
            cells = [p.number, p.owner, p.location, p.type, p.status_label, p.due or "—"]
            items = [QStandardItem(c) for c in cells]
            items[0].setData(p.number, Qt.UserRole)
            items[0].setData(p.status, Qt.UserRole + 1)
            items[0].setTextAlignment(Qt.AlignLeft | Qt.AlignVCenter)
            items[4].setForeground(QBrush(QColor(fg)))
            items[4].setBackground(QBrush(QColor(bg)))
            self.model.appendRow(items)
        self._fill_status_list()
        self.table.sortByColumn(0, Qt.DescendingOrder)
        if select:
            self._select_number(select)
        n = len(self.projects)
        self.statusBar().showMessage(f"{n} پروژه · پوشه اصلی: {self.root} · نسخه {__version__}")

    def _fill_status_list(self) -> None:
        current = self.status_list.currentRow()
        self.status_list.blockSignals(True)
        self.status_list.clear()
        counts = {k: sum(1 for p in self.projects if p.status == k) for k, _ in STATUSES}
        entries = [("", "همه پروژه‌ها", len(self.projects))] + [(k, l, counts[k]) for k, l in STATUSES]
        for key, label, count in entries:
            item = QListWidgetItem(f"{label}   ({count})")
            item.setData(Qt.UserRole, key)
            self.status_list.addItem(item)
        self.status_list.setCurrentRow(max(current, 0))
        self.status_list.blockSignals(False)
        self._apply_filter()

    def _apply_filter(self, *_args) -> None:
        item = self.status_list.currentItem()
        self.proxy.set_filters(item.data(Qt.UserRole) if item else "", self.search.text())

    def _select_number(self, number: str) -> None:
        for r in range(self.proxy.rowCount()):
            idx = self.proxy.index(r, 0)
            if self.proxy.data(idx, Qt.UserRole) == number:
                self.table.setCurrentIndex(idx)
                return

    def current_project(self) -> Project | None:
        if self.stack.currentWidget() is self.project_view:
            return self.project_view.project
        rows = self.table.selectionModel().selectedRows()
        idx = rows[0] if rows else self.table.currentIndex()
        if not idx.isValid():
            return None
        number = self.proxy.data(self.proxy.index(idx.row(), 0), Qt.UserRole)
        return next((p for p in self.projects if p.number == number), None)

    def _need_project(self) -> Project | None:
        p = self.current_project()
        if p is None:
            QMessageBox.information(self, "پوشه‌یار", "ابتدا یک پروژه را از فهرست انتخاب کنید.")
        return p

    # ---- actions ----
    def new_project(self) -> None:
        year = today_jalali()[0]
        number = next_number(self.projects, year)
        dlg = ProjectDialog(self, number=number, root_text=str(self.root))
        if dlg.exec() != ProjectDialog.Accepted:
            return
        project = Project(number=number, owner="", location="", type="")
        dlg.apply_to(project)
        project.folder = folder_relpath(number, dlg.slug_text())
        try:
            create_project_folders(self.root, project, self.cfg.crs)
        except FileExistsError:
            QMessageBox.warning(self, "پوشه تکراری", "پوشه‌ای با این نام از قبل وجود دارد. نام لاتین پوشه را عوض کنید.")
            return
        except OSError as exc:
            QMessageBox.critical(self, "خطا در ساخت پوشه", str(exc))
            return
        self.projects.append(project)
        self._save()
        self.reload(select=number)

    def edit_project(self) -> None:
        p = self._need_project()
        if not p:
            return
        dlg = ProjectDialog(self, number=p.number, root_text=str(self.root), project=p)
        if dlg.exec() == ProjectDialog.Accepted:
            dlg.apply_to(p)
            self._save()
            self.reload(select=p.number)

    def set_status(self, project: Project, status: str) -> None:
        project.status = status
        self._save()
        self.reload(select=project.number)

    def _save(self) -> None:
        try:
            save_projects(self.root, self.projects)
        except OSError as exc:
            QMessageBox.critical(
                self, "ذخیره فهرست ممکن نشد",
                f"اگر فایل projects.csv در اکسل باز است آن را ببندید و دوباره امتحان کنید.\n{exc}",
            )

    def enter_project(self) -> None:
        p = self.current_project()
        if not p:
            return
        if not p.path(self.root).is_dir():
            QMessageBox.warning(self, "پوشه پیدا نشد", f"این پوشه روی دیسک نیست:\n{p.path(self.root)}")
            return
        self.project_view.set_project(p, self.root)
        self.stack.setCurrentWidget(self.project_view)
        self.setWindowTitle(f"پوشه‌یار نقشه‌برداری — {p.number} {p.owner}")

    def show_list(self) -> None:
        self.stack.setCurrentIndex(0)
        self.setWindowTitle("پوشه‌یار نقشه‌برداری")

    def reveal_project(self) -> None:
        if (p := self._need_project()):
            shell.reveal(p.path(self.root))

    def copy_project_path(self) -> None:
        if (p := self._need_project()):
            shell.copy_text(str(p.path(self.root)))
            self.statusBar().showMessage("مسیر کپی شد", 3000)

    def archive_project(self) -> None:
        p = self._need_project()
        if not p:
            return
        src = p.path(self.root)
        out_dir = self.root / "_Archive"
        out_dir.mkdir(exist_ok=True)
        answer = QMessageBox.question(
            self, "بایگانی ZIP",
            f"از پوشه پروژه {p.number} یک فایل ZIP ساخته شود؟\nپوشه اصلی دست‌نخورده می‌ماند.",
        )
        if answer != QMessageBox.Yes:
            return
        try:
            archive = shutil.make_archive(str(out_dir / src.name), "zip", root_dir=src.parent, base_dir=src.name)
        except OSError as exc:
            QMessageBox.critical(self, "خطا در بایگانی", str(exc))
            return
        self.statusBar().showMessage(f"ساخته شد: {archive}", 8000)
        shell.reveal(Path(archive))

    def open_csv(self) -> None:
        path = csv_path(self.root)
        if not path.exists():
            save_projects(self.root, self.projects)
        shell.open_path(path)

    def change_root(self) -> None:
        folder = QFileDialog.getExistingDirectory(self, "پوشه اصلی پروژه‌ها را انتخاب کنید", str(self.root))
        if folder:
            self.root = Path(folder)
            self.cfg.root = folder
            save_config(self.cfg, self.config_dir)
            self.show_list()
            self.reload()

    def _row_menu(self, pos) -> None:
        p = self.current_project()
        if not p:
            return
        menu = QMenu(self)
        menu.addAction("ورود به پروژه", self.enter_project)
        menu.addAction("نمایش پوشه در Explorer", self.reveal_project)
        menu.addAction("ویرایش مشخصات", self.edit_project)
        sub = menu.addMenu("تغییر وضعیت")
        for key, label in STATUSES:
            act = sub.addAction(label)
            act.setCheckable(True)
            act.setChecked(p.status == key)
            act.triggered.connect(lambda _=False, k=key: self.set_status(p, k))
        menu.exec(self.table.viewport().mapToGlobal(pos))
