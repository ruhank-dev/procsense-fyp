"""Version compatibility between the headset and the laptop (P3-NET-10).

The headset sends X-ProcSense-Build, X-ProcSense-Engine and X-ProcSense-Content. The table in content/ai/compat.json says which engine and content versions the server
accepts; an empty or missing header is allowed (older clients, browsers), a version that is not listed is refused with 426 so the headset can show the banner."""
from __future__ import annotations

import json
from pathlib import Path

COMPAT_FILE = Path(__file__).resolve().parents[2] / "content" / "ai" / "compat.json"
API_VERSION = 1


def load_table(path: Path = COMPAT_FILE) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"api_version": API_VERSION, "engine_versions": [], "content_versions": []}


def check(headers, table: dict | None = None, settings=None) -> str | None:
    """None when the request may go on, else a short reason."""
    table = table or load_table()
    engine = headers.get("x-procsense-engine", "").strip()
    content = headers.get("x-procsense-content", "").strip()
    engines = list(table.get("engine_versions") or []) + list(getattr(settings, "supported_engine_versions", []) or [])
    contents = list(table.get("content_versions") or []) + list(getattr(settings, "supported_content_versions", []) or [])
    if engine and engines and engine not in engines:
        return f"engine_version_unsupported:{engine}"
    if content and contents and content not in contents:
        return f"content_version_unsupported:{content}"
    return None
