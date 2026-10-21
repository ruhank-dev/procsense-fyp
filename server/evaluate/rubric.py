"""The approved rubric the evaluator judges against: objectives with their definitions, positive and negative examples (content/assessment/taxonomy.json) and the approved behaviour rule texts (content/assessment/mapping.json), the
annotation-rubric tie-breaks kept as data (server/evaluate/wording.json, from tests/classifier_synthetic_v2/rubric.md), and the closed vocabularies of the output contract. Nothing clinical is stated here that those files do not state."""
from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TAXONOMY = ROOT / "content" / "assessment" / "taxonomy.json"
MAPPING = ROOT / "content" / "assessment" / "mapping.json"
WORDING = Path(__file__).with_name("wording.json")

ASSERTIONS = ("performed", "negated", "intended_later", "hypothetical", "reported", "other_actor", "earlier_claim", "uncertain", "incomplete", "none")
ACTORS = ("trainee", "other", "unclear")
RECIPIENTS = ("patient", "colleague", "senior", "unclear", "none")
FORMS = ("statement", "question", "request")
MAX_ACTS = 3
GRADE_MAX = 3


@lru_cache
def objectives() -> list[dict]:
    return [a for a in json.loads(TAXONOMY.read_text(encoding="utf-8"))["acts"] if a["id"] != "NONE"]


def objective_ids() -> list[str]:
    return [a["id"] for a in objectives()]


def critical_ids() -> set[str]:
    return set(json.loads(MAPPING.read_text(encoding="utf-8"))["critical_acts"])


@lru_cache
def wording() -> dict:
    return json.loads(WORDING.read_text(encoding="utf-8"))


def rules_text() -> str:
    """the approved annotation rubric rules 1 to 9, for choosing between NEIGHBOURING objectives for the same words; where a rule says the first clause decides, that is the objective for those words, and a later clause that does a separate job is a second objective."""
    return ("Approved rubric rules for choosing between neighbouring objectives (apply to the same words; a later clause that does a separate job is a second objective):\n" + "\n".join(wording()["rules"]))


def rules_of(objective: str) -> list[str]:
    m = json.loads(MAPPING.read_text(encoding="utf-8"))
    return sorted({b["rule"] for b in m["behaviours"] if objective in (b.get("ai_acts") or [])})


def objective_lines() -> list[str]:
    out = []
    for a in objectives():
        rule = "; ".join(rules_of(a["id"]))
        pos = " | ".join(a.get("positive", [])[:2])
        neg = " | ".join(a.get("negative", [])[:1])
        out.append(f"{a['id']}: {a['definition']}" + (f" Approved rubric rule: {rule}." if rule else "") + (f" Example: {pos}" if pos else "") + (f" Not it: {neg}" if neg else ""))
    return out
