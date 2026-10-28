"""نمای داخل یک پروژه: درخت پوشه‌ها در کنار و محتوای پوشه انتخاب‌شده."""

from __future__ import annotations

import os
import shutil
import stat
from pathlib import Path

from PySide6.QtCore import QDir, QModelIndex, QSortFilterProxyModel, Qt, Signal
from PySide6.QtGui import QKeySequence, QShortcut
from PySide6.QtWidgets import (
    QAbstractItemView,
    QFileSystemModel,
    QHBoxLayout,
    QHeaderView,
    QLabel,
    QLineEdit,
    QMenu,
    QMessageBox,
    QPushButton,
    QSplitter,
    QTableView,
    QTreeView,
    QVBoxLayout,
    QWidget,
)

from .. import shell
from ..jalali import mtime_to_jalali
from ..projects import SUBFOLDERS, is_in_raw_folder
from ..storage import Project

_HEADERS = ("نام", "حجم", "نوع", "تاریخ تغییر")
_NOTES = {name: desc for name, desc, _ in SUBFOLDERS}


class JalaliFileModel(QFileSystemModel):
    """مدل فایل‌ها با تاریخ شمسی و عنوان‌های فارسی."""

    def headerData(self, section, orientation, role=Qt.DisplayRole):
        if orientation == Qt.Horizontal and role == Qt.DisplayRole and section < len(_HEADERS):
            return _HEADERS[section]
        return super().headerData(section, orientation, role)

    def data(self, index, role=Qt.DisplayRole):
        if role == Qt.DisplayRole:
            if index.column() == 3:
                return mtime_to_jalali(self.lastModified(index).toSecsSinceEpoch())
            if index.column() < 3:
                # متن لاتین در جدول راست‌به‌چپ به‌هم نریزد
                return "\u2066" + str(super().data(index, role)) + "\u2069"
        if role == Qt.ToolTipRole and index.column() == 0:
            note = _NOTES.get(self.fileName(index))
            if note and index.parent() == self.index(str(Path(self.filePath(index)).parent)):
                return note
        return super().data(index, role)


class FileSort(QSortFilterProxyModel):
    """مرتب‌سازی مثل Explorer: پوشه‌ها اول، بعد بر اساس نام، حجم یا تاریخ."""

    def lessThan(self, left: QModelIndex, right: QModelIndex) -> bool:
        m = self.sourceModel()
        l_dir, r_dir = m.isDir(left), m.isDir(right)
        if l_dir != r_dir:
            return l_dir == (self.sortOrder() == Qt.AscendingOrder)
        col = left.column()
        if col == 1:
            return m.size(left) < m.size(right)
        if col == 3:
            return m.lastModified(left) < m.lastModified(right)
        key = (lambda i: m.fileName(i).lower()) if col == 0 else (lambda i: m.type(i).lower())
        return key(left) < key(right)


class FileTable(QTableView):
    """جدول محتوای پوشه؛ فایل‌های کشیده‌شده از Explorer را در پوشه جاری کپی می‌کند."""

    filesDropped = Signal(list)

    def __init__(self):
        super().__init__()
        self.setAcceptDrops(True)
        self.setDragDropMode(QAbstractItemView.DropOnly)

    def dragEnterEvent(self, event):
        if event.mimeData().hasUrls():
            event.acceptProposedAction()

    dragMoveEvent = dragEnterEvent

    def dropEvent(self, event):
        paths = [Path(u.toLocalFile()) for u in event.mimeData().urls() if u.isLocalFile()]
        if paths:
            event.acceptProposedAction()
            self.filesDropped.emit(paths)


class ProjectView(QWidget):
    backRequested = Signal()

    def __init__(self):
        super().__init__()
        self.project: Project | None = None
        self.project_dir = Path()
        self.current = Path()

        self.model = JalaliFileModel(self)
        self.model.setReadOnly(True)
        self.model.setFilter(QDir.AllEntries | QDir.NoDotAndDotDot)
        self.proxy = FileSort(self)
        self.proxy.setSourceModel(self.model)

        self.back_btn = QPushButton("→ همه پروژه‌ها")
        self.up_btn = QPushButton("↑ بالاتر")
        self.path_edit = QLineEdit()
        self.path_edit.setReadOnly(True)
        self.path_edit.setLayoutDirection(Qt.LeftToRight)
        copy_btn = QPushButton("کپی مسیر")
        explorer_btn = QPushButton("بازکردن در Explorer")
        explorer_btn.setObjectName("primary")

        bar = QHBoxLayout()
        for w in (self.back_btn, self.up_btn):
            bar.addWidget(w)
        bar.addWidget(self.path_edit, 1)
        bar.addWidget(copy_btn)
        bar.addWidget(explorer_btn)

        self.tree = QTreeView()
        self.tree.setModel(self.proxy)
        self.tree.setHeaderHidden(True)
        for col in (1, 2, 3):
            self.tree.setColumnHidden(col, True)
        self.tree.setLayoutDirection(Qt.LeftToRight)
        self.tree.setMinimumWidth(240)
        self.root_btn = QPushButton()
        self.root_btn.setLayoutDirection(Qt.LeftToRight)
        self.root_btn.setToolTip("ریشه پروژه")
        left = QWidget()
        ll = QVBoxLayout(left)
        ll.setContentsMargins(0, 0, 0, 0)
        ll.addWidget(self.root_btn)
        ll.addWidget(self.tree, 1)

        self.table = FileTable()
        self.table.setModel(self.proxy)
        self.table.setSelectionBehavior(QAbstractItemView.SelectRows)
        self.table.setSelectionMode(QAbstractItemView.SingleSelection)
        self.table.setEditTriggers(QAbstractItemView.NoEditTriggers)
        self.table.setShowGrid(False)
        self.table.verticalHeader().hide()
        self.table.setSortingEnabled(True)
        self.table.sortByColumn(0, Qt.AscendingOrder)
        self.table.setContextMenuPolicy(Qt.CustomContextMenu)
        hdr = self.table.horizontalHeader()
        hdr.setSectionResizeMode(0, QHeaderView.Stretch)
        for col in (1, 2, 3):
            hdr.setSectionResizeMode(col, QHeaderView.ResizeToContents)

        self.title = QLabel()
        self.title.setObjectName("folderTitle")
        self.title.setLayoutDirection(Qt.LeftToRight)
        self.subtitle = QLabel()
        self.subtitle.setObjectName("muted")
        self.hint = QLabel("دوبار کلیک: ورود به پوشه یا بازکردن فایل با برنامه پیش‌فرض · فایل را از Explorer اینجا بکشید تا کپی شود")
        self.hint.setObjectName("muted")
        self.hint.setWordWrap(True)

        right = QWidget()
        rl = QVBoxLayout(right)
        rl.setContentsMargins(0, 0, 0, 0)
        rl.addWidget(self.title)
        rl.addWidget(self.subtitle)
        rl.addWidget(self.table, 1)
        rl.addWidget(self.hint)

        split = QSplitter()
        split.addWidget(left)
        split.addWidget(right)
        split.setStretchFactor(1, 1)
        split.setLayoutDirection(Qt.LeftToRight)
        right.setLayoutDirection(Qt.RightToLeft)

        lay = QVBoxLayout(self)
        lay.addLayout(bar)
        lay.addWidget(split, 1)

        self.back_btn.clicked.connect(self.backRequested)
        self.up_btn.clicked.connect(self.go_up)
        copy_btn.clicked.connect(lambda: shell.copy_text(str(self.current)))
        explorer_btn.clicked.connect(lambda: shell.reveal(self.current))
        self.tree.clicked.connect(self._on_tree_clicked)
        self.root_btn.clicked.connect(lambda: self.navigate(self.project_dir))
        self.table.doubleClicked.connect(self._on_table_activated)
        self.table.customContextMenuRequested.connect(self._context_menu)
        self.table.filesDropped.connect(self._on_drop)
        QShortcut(QKeySequence(Qt.Key_Backspace), self, activated=self.go_up)
        QShortcut(QKeySequence(Qt.Key_Return), self.table, activated=self._activate_selected)

    # ---- navigation ----
    def set_project(self, project: Project, root: Path) -> None:
        self.project = project
        self.project_dir = project.path(root)
        self.model.setRootPath(str(self.project_dir))
        self.tree.setRootIndex(self._to_view(self.project_dir))
        self.root_btn.setText("\u2066" + self.project_dir.name + "\u2069")
        self.navigate(self.project_dir)

    def navigate(self, path: Path) -> None:
        if not path.is_dir():
            return
        self.current = path
        self.model.setRootPath(str(path))
        self.table.setRootIndex(self._to_view(path))
        tree_index = self._to_view(path)
        self.tree.setCurrentIndex(tree_index)
        if path != self.project_dir:
            self.tree.expand(tree_index)
        self.path_edit.setText(str(path))
        self.up_btn.setEnabled(path != self.project_dir)
        at_root = path == self.project_dir
        self.title.setText(path.name)
        if at_root and self.project:
            self.subtitle.setText(f"{self.project.owner} — {self.project.location} · {self.project.type}")
        else:
            note = _NOTES.get(path.name, "")
            raw = " · فقط‌افزودنی (فایل خام)" if is_in_raw_folder(self.project_dir, path) else ""
            self.subtitle.setText(note + raw)

    def go_up(self) -> None:
        if self.current != self.project_dir:
            self.navigate(self.current.parent)

    def refresh(self) -> None:
        if self.current.is_dir():
            self.navigate(self.current)

    def _to_view(self, path: Path) -> QModelIndex:
        return self.proxy.mapFromSource(self.model.index(str(path)))

    def _path_of(self, index: QModelIndex) -> Path:
        return Path(self.model.filePath(self.proxy.mapToSource(index)))

    def _on_tree_clicked(self, index: QModelIndex) -> None:
        self.navigate(self._path_of(index))

    def _selected_path(self) -> Path | None:
        idx = self.table.currentIndex()
        return self._path_of(idx) if idx.isValid() else None

    def _on_table_activated(self, index: QModelIndex) -> None:
        path = self._path_of(index)
        self.navigate(path) if path.is_dir() else shell.open_path(path)

    def _activate_selected(self) -> None:
        idx = self.table.currentIndex()
        if idx.isValid():
            self._on_table_activated(idx)

    def _context_menu(self, pos) -> None:
        path = self._selected_path()
        if not path:
            return
        menu = QMenu(self)
        menu.addAction("بازکردن", lambda: self._on_table_activated(self.table.currentIndex()))
        menu.addAction("نمایش در Explorer", lambda: shell.reveal(path))
        menu.addAction("کپی مسیر", lambda: shell.copy_text(str(path)))
        menu.exec(self.table.viewport().mapToGlobal(pos))

    # ---- drop (copy) ----
    def _on_drop(self, sources: list[Path]) -> None:
        in_raw = is_in_raw_folder(self.project_dir, self.current)
        copied, skipped = [], []
        for src in sources:
            dest = self.current / src.name
            if dest.exists() or not src.exists():
                skipped.append(src.name)
                continue
            try:
                if src.is_dir():
                    shutil.copytree(src, dest)
                else:
                    shutil.copy2(src, dest)
                if in_raw:
                    _make_read_only(dest)
                copied.append(src.name)
            except OSError as exc:
                skipped.append(f"{src.name} ({exc.strerror or exc})")
        self.refresh()
        if skipped:
            QMessageBox.warning(
                self,
                "بعضی فایل‌ها کپی نشد",
                "این موارد از قبل در پوشه هستند یا کپی نشدند و دست‌نخورده ماندند:\n" + "\n".join(skipped),
            )


def _make_read_only(path: Path) -> None:
    """فایل‌های خام بعد از کپی فقط‌خواندنی می‌شوند تا اشتباهی ویرایش نشوند."""
    targets = [path] if path.is_file() else [p for p in path.rglob("*") if p.is_file()]
    for p in targets:
        os.chmod(p, stat.S_IREAD | stat.S_IRGRP | stat.S_IROTH)
