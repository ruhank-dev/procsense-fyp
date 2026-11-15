"""Validator for content/ai/staff_exam_policy.json (owner decision D3, staff voices in the examination).

    uv run --project tools tools/content/validate_staff_exam_policy.py

Every role of the policy is a staff role of content/ai/intents.json with practice lines in content/ai/staff_intent_lines.json; every intent of that role
has exactly one examination line (so whatever intent the model chooses, an examination-safe line exists) and no unknown intent is listed; every role has
address words (a staff member speaks in the examination only when spoken to); no examination line holds a forbidden word, a number or a measurement (the
built-in list below always applies, the file's list can only add to it); every practice line that differs from its examination line has a review entry
with a verdict and a reason. Exits 1 with the list of problems.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
AI = ROOT / "content" / "ai"
BASE_FORBIDDEN = ["hint", "score", "points", "correct", "next step", "you should", "you need to", "remember to", "checklist", "consent", "fasting", "allerg", "airway",
                  "sign in", "time out", "concern", "diagnos"]
DIGIT = re.compile(r"\d")
VERDICTS = {"replaced", "kept", "kept_reworded"}


def load(name: str) -> dict:
    return json.loads((AI / name).read_text(encoding="utf-8"))


def problems(policy: dict | None = None, intents: dict | None = None, practice: dict | None = None) -> list[str]:
    policy = policy if policy is not None else load("staff_exam_policy.json")
    intents = intents if intents is not None else load("intents.json")["roles"]
    practice = practice if practice is not None else load("staff_intent_lines.json")["roles"]
    p: list[str] = []
    if policy.get("schema") != "procsense.staff_exam_policy/1":
        p.append("schema must be procsense.staff_exam_policy/1")
    roles = policy.get("roles") or {}
    if not roles:
        p.append("no roles")
    forbidden = sorted(set(BASE_FORBIDDEN) | {w.lower() for w in policy.get("forbidden_words", [])})
    reviewed = {(r.get("role"), r.get("intent")): r for r in policy.get("review", [])}
    for role, spec in roles.items():
        if role == "patient" or role not in intents:
            p.append(f"{role}: not a staff role of intents.json")
            continue
        if role not in practice:
            p.append(f"{role}: no practice lines in staff_intent_lines.json")
        words = spec.get("address_words") or []
        if not words or any(not str(w).strip() for w in words):
            p.append(f"{role}: needs address words")
        lines = spec.get("lines") or {}
        for intent in intents[role]:
            if not str(lines.get(intent, "")).strip():
                p.append(f"{role}.{intent}: no examination line")
        for intent, line in lines.items():
            where = f"{role}.{intent}"
            if intent not in intents[role]:
                p.append(f"{where}: not an allowed intent of the role")
                continue
            low = str(line).lower()
            for w in forbidden:
                if w in low:
                    p.append(f"{where}: examination line holds the forbidden word '{w}'")
            if DIGIT.search(low):
                p.append(f"{where}: examination line holds a number")
            old = practice.get(role, {}).get(intent)
            if old is not None and old != line:
                r = reviewed.get((role, intent))
                if r is None:
                    p.append(f"{where}: differs from the practice line but has no review entry")
                elif r.get("verdict") not in VERDICTS or not str(r.get("reason", "")).strip():
                    p.append(f"{where}: review needs a verdict ({', '.join(sorted(VERDICTS))}) and a reason")
                elif r.get("practice_line") != old:
                    p.append(f"{where}: review quotes a practice line that is not the current one")
    return p


def main() -> int:
    found = problems()
    for x in found:
        print("staff_exam_policy:", x)
    if not found:
        print("staff_exam_policy: OK")
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
