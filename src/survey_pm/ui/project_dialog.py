"""پنجره ساخت پروژه جدید و ویرایش مشخصات پروژه."""

from __future__ import annotations

from PySide6.QtCore import Qt
from PySide6.QtWidgets import (
    QComboBox,
    QDialog,
    QDialogButtonBox,
    QFormLayout,
    QLabel,
    QLineEdit,
    QVBoxLayout,
)

from ..projects import SUBFOLDERS, folder_relpath
from ..storage import PROJECT_TYPES, STATUSES, Project
from ..translit import folder_slug


class ProjectDialog(QDialog):
    """در حالت ساخت، نام لاتین پوشه را هم می‌پرسد؛ در حالت ویرایش، پوشه تغییر نمی‌کند."""

    def __init__(self, parent, *, number: str, root_text: str, project: Project | None = None):
        super().__init__(parent)
        self.editing = project is not None
        self.number = number
        self.setWindowTitle("ویرایش پروژه" if self.editing else "پروژه جدید")
        self.setMinimumWidth(460)

        self.owner = QLineEdit(project.owner if project else "")
        self.location = QLineEdit(project.location if project else "")
        self.location.setPlaceholderText("مثلاً: کرج، پلاک ۸۸۱۲ اصلی")
        self.type = QComboBox()
        self.type.addItems(PROJECT_TYPES)
        self.status = QComboBox()
        for key, label in STATUSES:
            self.status.addItem(label, key)
        self.due = QLineEdit(project.due if project else "")
        self.due.setPlaceholderText("۱۴۰۵/۰۸/۱۵")
        self.note = QLineEdit(project.note if project else "")
        self.slug = QLineEdit()
        self.slug.setPlaceholderText("Name_City")
        self.slug.setLayoutDirection(Qt.LeftToRight)

        if project:
            if project.type not in PROJECT_TYPES:
                self.type.addItem(project.type)
            self.type.setCurrentText(project.type)
            self.status.setCurrentIndex(max(0, self.status.findData(project.status)))

        form = QFormLayout()
        form.addRow("شماره پروژه", QLabel(number))
        form.addRow("نام مالک", self.owner)
        form.addRow("موقعیت / پلاک", self.location)
        form.addRow("نوع کار", self.type)
        form.addRow("وضعیت", self.status)
        form.addRow("تاریخ تحویل", self.due)
        form.addRow("یادداشت", self.note)
        if not self.editing:
            form.addRow("نام لاتین پوشه", self.slug)

        self.preview = QLabel()
        self.preview.setStyleSheet("color: #5c6b7a;")
        self.preview.setLayoutDirection(Qt.LeftToRight)
        self.preview.setWordWrap(True)
        self.preview.setTextInteractionFlags(Qt.TextSelectableByMouse)
        self._root_text = root_text
        self._slug_edited = False

        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel)
        buttons.button(QDialogButtonBox.Ok).setText("ثبت")
        buttons.button(QDialogButtonBox.Cancel).setText("انصراف")
        buttons.accepted.connect(self._accept)
        buttons.rejected.connect(self.reject)

        layout = QVBoxLayout(self)
        layout.addLayout(form)
        if not self.editing:
            layout.addWidget(self.preview)
            layout.addWidget(QLabel("زیرپوشه‌ها: " + "  ".join(n for n, _, _ in SUBFOLDERS)))
            self.owner.textChanged.connect(self._autoslug)
            self.location.textChanged.connect(self._autoslug)
            self.slug.textEdited.connect(self._slug_manual)
            self.slug.textChanged.connect(self._update_preview)
            self._autoslug()
        layout.addWidget(buttons)
        self.owner.setFocus()

    def _autoslug(self) -> None:
        if not self._slug_edited:
            self.slug.setText(folder_slug(self.owner.text(), self.location.text()))
        self._update_preview()

    def _slug_manual(self) -> None:
        self._slug_edited = True

    def _update_preview(self) -> None:
        self.preview.setText(f"{self._root_text}\\{folder_relpath(self.number, self.slug_text()).replace('/', chr(92))}")

    def slug_text(self) -> str:
        cleaned = "".join(c if (c.isascii() and (c.isalnum() or c in "_-")) else "_" for c in self.slug.text().strip())
        return cleaned.strip("_") or "Project"

    def _accept(self) -> None:
        if not self.owner.text().strip():
            self.owner.setFocus()
            self.owner.setStyleSheet("border: 1px solid #c42b1c;")
            return
        self.accept()

    def apply_to(self, project: Project) -> None:
        project.owner = self.owner.text().strip()
        project.location = self.location.text().strip()
        project.type = self.type.currentText()
        project.status = self.status.currentData()
        project.due = self.due.text().strip()
        project.note = self.note.text().strip()
