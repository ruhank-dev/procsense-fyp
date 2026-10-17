"""Which AI features are on (p1-ai-language-s09): read from content/ai/features.json, made by tools/eval/ai_gate.py from the evaluation results.

    from server import ai_features
    if not ai_features.enabled("patient_dialogue"): ...answer with the fallback signal...

A feature is on only if every suite of it passed on the committed results; a missing result means off (SKIPPED never passes). `AI_FEATURES_OVERRIDE` in the settings can name features to force on (comma
separated, or `all`) for development and tests; it is empty in production. The file is read once.
"""
from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path

from server.config import get_settings

FILE = Path(__file__).resolve().parents[1] / "content" / "ai" / "features.json"


@lru_cache
def _file() -> dict:
    try:
        return json.loads(FILE.read_text(encoding="utf-8"))["features"]
    except (OSError, ValueError, KeyError):
        return {}


def enabled(feature: str) -> bool:
    forced = get_settings().ai_features_override.strip().lower()
    if forced == "all" or feature in {x.strip() for x in forced.split(",") if x.strip()}:
        return True
    return bool(_file().get(feature, {}).get("enabled", False))


def status() -> dict:
    return {k: {"enabled": enabled(k), "status": v["status"]} for k, v in sorted(_file().items())}
