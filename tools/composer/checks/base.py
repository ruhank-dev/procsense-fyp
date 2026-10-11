"""Shared helpers for the Stage B checks: the existing case-file rules (tools/content/case_file_checks.py) sorted into the Stage B check numbers and defect codes."""
from __future__ import annotations

import re
import sys
from pathlib import Path

from . import Defect

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "content"))
import case_file_checks  # noqa: E402

# (pattern, check, code); the first match wins. Messages come from case_file_checks.problems()
CLASSES = [
    (r"schema:", 1, "SCHEMA"),
    (r"lacks the topic|exactly one fact is volunteered|hidden but has no disclose_on|first observation must be|every case needs its expected ASA|booked surgery time|problem ranks|duplicate", 2, "REQUIRED_FIELD"),
    (r"age \d+ does not match|wristband text|spoken identity|name or MRN repeats", 4, "IDENTITY_MISMATCH"),
    (r"NEWS2", 4, "NEWS2_MISMATCH"),
    (r"is inside .* but flagged|is outside .* but flagged|critical value but not flagged|differs from the walkthrough|cites a row", 4, "RESULT_FLAG_MISMATCH"),
    (r"cannot be later than now", 4, "TIME_IMPOSSIBLE"),
    (r"needs at least one correct", 4, "DECISION_OPTIONS_INVALID"),
    (r"only a female patient can be pregnant", 4, "PREGNANT_MALE"),
]

_cache: dict[int, tuple[object, list[tuple[int, str, str]]]] = {}


def classified(card: dict, ctx: dict) -> list[tuple[int, str, str]]:
    """(check, code, message) for every problem the existing rules find in this card (cached per card object)."""
    hit = _cache.get(id(card))
    if hit is not None and hit[0] is card:
        return hit[1]
    out = []
    for msg in case_file_checks.problems(card, ctx, ctx.get("others")):
        for pat, check, code in CLASSES:
            if re.search(pat, msg):
                out.append((check, code, msg))
                break
        else:
            out.append((3, "CONSTRAINT", msg))
    _cache.clear()
    _cache[id(card)] = (card, out)
    return out


def defects(card: dict, ctx: dict, check: int) -> list[Defect]:
    return [Defect(c, code, msg) for c, code, msg in classified(card, ctx) if c == check]
