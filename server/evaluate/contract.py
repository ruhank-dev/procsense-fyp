"""The strict output contract of the evaluator (p1-ai-language-s22, ADR-0025). A reply that does not meet it is rejected, never repaired. The checks are about FORM and internal consistency only (closed vocabularies, grade range, evidence copied from the utterance, performed_now consistent with assertion and actor);
nothing here judges what the utterance means: that is the model's job, and no downstream rule second-guesses it."""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass

from server.evaluate import rubric

TOP_KEYS = {"acts", "claims_physical_action", "physical_action", "critical_topic"}
ACT_KEYS = {"objective", "assertion", "actor", "recipient", "form", "performed_now", "relevance", "correctness", "completeness", "evidence", "rationale"}
RETRY_FORMAT = {"FENCED", "NOT_JSON", "SURROUNDING_TEXT", "SCHEMA", "TRUNCATED"}
MAX_UTTERANCE = 400                  # the route accepts no longer utterance (server/schemas/evaluate.py)
MAX_EVIDENCE = MAX_UTTERANCE         # the evidence is words copied from the utterance, so it can never be longer than the utterance itself (DT-247: a flat 160 rejected natural longer speech); the engine accepts a span of this length


class Invalid(Exception):
    def __init__(self, code: str, detail: str = ""):
        super().__init__(code + ":" + detail)
        self.code, self.detail = code, detail


@dataclass
class Act:
    objective: str
    assertion: str
    actor: str
    recipient: str
    form: str
    performed_now: bool
    relevance: int
    correctness: int
    completeness: int
    evidence: str
    rationale: str


@dataclass
class Evaluation:
    acts: list[Act]
    claims_physical_action: bool
    physical_action: str
    critical_topic: bool

    def to_dict(self) -> dict:
        return {"acts": [asdict(a) for a in self.acts], "claims_physical_action": self.claims_physical_action, "physical_action": self.physical_action, "critical_topic": self.critical_topic}


def norm(t: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9' ]+", " ", t.lower().replace("’", "'")).split())


def parse(text: str, done_reason, utterance: str) -> Evaluation:
    t = (text or "").strip()
    if done_reason not in (None, "stop"):
        raise Invalid("TRUNCATED", str(done_reason))
    if t.startswith("```"):
        raise Invalid("FENCED")
    try:
        d = json.loads(t)
    except ValueError:
        raise Invalid("SURROUNDING_TEXT" if "{" in t else "NOT_JSON")
    if not isinstance(d, dict) or set(d) != TOP_KEYS:
        raise Invalid("SCHEMA", "top-level keys")
    if not isinstance(d["acts"], list) or len(d["acts"]) > rubric.MAX_ACTS:
        raise Invalid("SCHEMA", "acts")
    if not isinstance(d["claims_physical_action"], bool) or not isinstance(d["critical_topic"], bool) or not isinstance(d["physical_action"], str) or len(d["physical_action"]) > 160:
        raise Invalid("SCHEMA", "utterance fields")
    ids, acts, seen, spoken = set(rubric.objective_ids()), [], set(), norm(utterance)
    for a in d["acts"]:
        if not isinstance(a, dict) or set(a) != ACT_KEYS:
            raise Invalid("SCHEMA", "act keys")
        if a["objective"] not in ids or a["objective"] in seen:
            raise Invalid("SCHEMA", "objective")
        seen.add(a["objective"])
        if a["assertion"] not in rubric.ASSERTIONS or a["actor"] not in rubric.ACTORS or a["recipient"] not in rubric.RECIPIENTS or a["form"] not in rubric.FORMS or not isinstance(a["performed_now"], bool):
            raise Invalid("SCHEMA", "closed values")
        for k in ("relevance", "correctness", "completeness"):
            if type(a[k]) is not int or not 0 <= a[k] <= rubric.GRADE_MAX:
                raise Invalid("SCHEMA", k)
        if not isinstance(a["evidence"], str) or not isinstance(a["rationale"], str) or len(a["evidence"]) > MAX_EVIDENCE or len(a["rationale"]) > 220:
            raise Invalid("SCHEMA", "text fields")
        if a["performed_now"] != (a["assertion"] == "performed" and a["actor"] == "trainee"):
            raise Invalid("SCHEMA", "performed_now inconsistent")
        if a["evidence"].strip() and norm(a["evidence"]) not in spoken:
            raise Invalid("SCHEMA", "evidence not in utterance")
        if a["performed_now"] and not a["evidence"].strip():
            raise Invalid("SCHEMA", "performed without evidence")
        acts.append(Act(**a))
    return Evaluation(acts, d["claims_physical_action"], d["physical_action"], d["critical_topic"])
