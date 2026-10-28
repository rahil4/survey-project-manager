"""تنظیمات برنامه (مسیر ریشه پروژه‌ها و سیستم مختصات پیش‌فرض)."""

from __future__ import annotations

import json
import os
import sys
from dataclasses import asdict, dataclass
from pathlib import Path

DEFAULT_CRS = "WGS84 / UTM zone 39N (EPSG:32639)"


def config_dir() -> Path:
    if sys.platform == "win32":
        base = Path(os.environ.get("APPDATA", Path.home()))
    else:
        base = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
    return base / "SurveyProjectManager"


@dataclass
class Config:
    root: str = ""
    crs: str = DEFAULT_CRS

    @property
    def root_path(self) -> Path | None:
        return Path(self.root) if self.root else None


def load_config(directory: Path | None = None) -> Config:
    path = (directory or config_dir()) / "config.json"
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return Config()
    return Config(root=str(data.get("root", "")), crs=str(data.get("crs", DEFAULT_CRS)))


def save_config(cfg: Config, directory: Path | None = None) -> None:
    folder = directory or config_dir()
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "config.json").write_text(
        json.dumps(asdict(cfg), ensure_ascii=False, indent=2), encoding="utf-8"
    )
