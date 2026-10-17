"""The checks a prompt's answer must pass before the gateway lets it out (p1-backend-s06). A prompt can be called through the gateway only if it is listed here: no check, no route.

    VALIDATORS[prompt_id](variables, value) -> list of defects ("CODE: sentence"); empty means the answer may be used.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def _narrative(variables: dict, value: object) -> list[str]:
    try:                                      # imported when needed so the gateway starts without the generation package
        from generation import narrative
    except ImportError:
        from server.generation import narrative
    lexicon = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))
    return narrative.defects(variables["original"], value["text"], lexicon)   # type: ignore[index]


VALIDATORS = {"compose.narrative": _narrative}
