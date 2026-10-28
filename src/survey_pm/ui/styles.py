STYLESHEET = """
QWidget { font-size: 10pt; }
QMainWindow, QWidget#central { background: #f3f3f3; }
QPushButton { background: #ffffff; border: 1px solid #cfcfcf; border-radius: 4px; padding: 5px 14px; }
QPushButton:hover { background: #eef5fb; }
QPushButton:disabled { color: #aaa; }
QPushButton#primary { background: #0f6cbd; color: #ffffff; border-color: #0f6cbd; }
QPushButton#primary:hover { background: #0c5ca3; }
QLineEdit, QComboBox { background: #ffffff; border: 1px solid #cfcfcf; border-radius: 4px; padding: 4px 8px; }
QLineEdit:focus, QComboBox:focus { border-bottom: 2px solid #0f6cbd; }
QTableView, QTreeView, QListWidget { background: #ffffff; border: 1px solid #dcdcdc; alternate-background-color: #fafafa; }
QTableView::item:selected, QTreeView::item:selected, QListWidget::item:selected { background: #cce4f7; color: #0b4f87; }
QHeaderView::section { background: #f7f7f7; color: #555; border: 0; border-bottom: 1px solid #ddd; padding: 6px 8px; }
QListWidget { padding: 4px; }
QListWidget::item { padding: 6px 8px; border-radius: 4px; }
QLabel#folderTitle { font-size: 15pt; font-weight: bold; font-family: Consolas, monospace; }
QLabel#muted { color: #666; font-size: 9pt; }
QToolBar { background: #fafafa; border-bottom: 1px solid #e0e0e0; spacing: 6px; padding: 4px; }
QToolButton { padding: 5px 10px; border-radius: 4px; }
QToolButton:hover { background: #e6f0fa; }
QStatusBar { background: #f0f0f0; color: #555; }
"""

STATUS_COLORS = {
    "collect": ("#52606d", "#e9edf1"),
    "field": ("#8a5a00", "#fbefd2"),
    "draw": ("#0b5c9e", "#dcecf8"),
    "check": ("#6b3fa0", "#eee6f8"),
    "done": ("#1d7a46", "#dff3e7"),
    "reg": ("#a3361d", "#fbe5df"),
}
