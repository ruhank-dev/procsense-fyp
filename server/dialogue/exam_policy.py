"""The examination policy for the staff voices (owner decision D3, content/ai/staff_exam_policy.json).

In the examination the staff speak, but only when the trainee speaks to them, and then only the examination line of the intent the model chose: a
role-appropriate answer, a read-back or a fact the trainee asked for, never an unsolicited next step, an answer, a score or a checklist item. The same
rules are applied again in Unity (StaffExamPolicy.cs); either side alone keeps the examination safe. Nothing here changes the simulation.
"""
from __future__ import annotations

import json
import re
from functools import lru_cache
from pathlib import Path

POLICY_FILE = Path(__file__).resolve().parents[2] / "content" / "ai" / "staff_exam_policy.json"


@lru_cache
def policy() -> dict:
    return json.loads(POLICY_FILE.read_text(encoding="utf-8"))["roles"]


def roles(requested: list[str]) -> list[str]:
    """The requested staff roles that may speak in the examination at all."""
    return [r for r in requested if r in policy()]


def lines(role: str) -> dict[str, str]:
    """The examination line of every intent of the role."""
    return dict(policy()[role]["lines"])


def addressed(role: str, question: str) -> bool:
    """The trainee spoke to this role: one of its address words is in the sentence as a whole word."""
    q = " " + re.sub(r"[^a-z ]+", " ", question.lower()) + " "
    return any(" " + w.lower() + " " in q for w in policy().get(role, {}).get("address_words", []))
