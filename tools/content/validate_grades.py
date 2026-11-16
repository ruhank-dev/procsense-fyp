"""Validator for content/scoring/grades.json (P3-RES-10 thresholds, P3-EXM-01 hint limit).

    uv run --project tools tools/content/validate_grades.py

The letter bands A, B, C, D are all present, whole numbers from 0 to 100 and strictly descending (A > B > C > D); the pass mark is a whole number from 0 to 100 and not
above band A; hint_penalty is a whole number from 0 to 100; hint_limit, when present, is a whole number from 0 to 10 (ProcSense.Core.Flow.GradeTable reads 3 when it is
missing). No other bands and no unknown keys. Exits 1 with the list of problems.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GRADES = ROOT / "content" / "scoring" / "grades.json"
LETTERS = ["A", "B", "C", "D"]
KEYS = {"schema", "note", "bands", "pass_mark", "hint_penalty", "hint_limit"}


def _whole(v) -> bool:
    return isinstance(v, int) and not isinstance(v, bool)


def problems(data: dict | None = None) -> list[str]:
    data = data if data is not None else json.loads(GRADES.read_text(encoding="utf-8"))
    p: list[str] = []
    if data.get("schema") != "procsense.grades/1":
        p.append("schema must be procsense.grades/1")
    for k in sorted(set(data) - KEYS):
        p.append(f"unknown key {k}")
    bands = data.get("bands")
    if not isinstance(bands, dict):
        return p + ["bands must be an object"]
    for k in sorted(set(bands) - set(LETTERS)):
        p.append(f"bands.{k}: not a letter grade (A-D; F is everything below D)")
    values = []
    for letter in LETTERS:
        v = bands.get(letter)
        if not _whole(v) or not 0 <= v <= 100:
            p.append(f"bands.{letter}: must be a whole number from 0 to 100")
        else:
            values.append((letter, v))
    for (a, va), (b, vb) in zip(values, values[1:]):
        if va <= vb:
            p.append(f"bands: {a} ({va}) must be above {b} ({vb})")
    pm = data.get("pass_mark")
    if not _whole(pm) or not 0 <= pm <= 100:
        p.append("pass_mark: must be a whole number from 0 to 100")
    elif _whole(bands.get("A")) and pm > bands["A"]:
        p.append(f"pass_mark ({pm}) is above band A ({bands['A']}): nobody could pass")
    hp = data.get("hint_penalty")
    if not _whole(hp) or not 0 <= hp <= 100:
        p.append("hint_penalty: must be a whole number from 0 to 100")
    if "hint_limit" in data and (not _whole(data["hint_limit"]) or not 0 <= data["hint_limit"] <= 10):
        p.append("hint_limit: must be a whole number from 0 to 10")
    return p


def main() -> int:
    found = problems()
    for x in found:
        print("grades.json: " + x)
    if not found:
        print("grades.json: ok")
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
