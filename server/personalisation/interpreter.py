"""The performance interpreter: a language model proposes hypotheses, evidence confirms or refutes them (p1-personalisation-s04, feature D67).

    interp = Interpreter(client)
    hyps = interp.propose(model, labels, trainee)       # up to 3 Hypothesis objects, each citing competency ids and evidence ids that exist
    tracker = Tracker(); tracker.add(hyps); tracker.update(model)      # later: confirmed, refuted or still open

The model reads a structured profile (competency id, plain name, sessions, share right, status) and may propose links between competencies. Its answer is accepted only if every competency id and every evidence
id it cites is in the profile it was given, the confidence is low when the evidence is thin (fewer than MIN_SESSIONS sessions behind any cited competency), and the text holds no number or id that is not in the
profile. A hypothesis NEVER changes the learner model: it is stored, shown to a teacher as a hypothesis, and checked against later sessions by rule: it is `confirmed` when every cited competency is still a
weakness or a hypothesis after CHECK_AFTER more sessions, `refuted` when one of them has become a strength, otherwise `open`. Whether the model's links are better than chance is a number the evaluation reports
(tools/ai/eval_interpreter.py), not something assumed.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field

try:
    from llm import gateway
except ImportError:
    from server.llm import gateway

MAX_HYPOTHESES = 3
MIN_SESSIONS = 3
CHECK_AFTER = 3
ID = re.compile(r"\bb\d{2}\b")
NUMBER = re.compile(r"\d+(?:\.\d+)?")
WEAK = ("weakness", "hypothesis")


@dataclass
class Hypothesis:
    competencies: list[str]
    evidence: list[str]
    confidence: str
    text: str
    status: str = "open"                # open | confirmed | refuted
    sessions_at_proposal: int = 0
    audit: dict = field(default_factory=dict)


def profile_lines(model, labels: dict[str, str]) -> tuple[str, set[str], set[str]]:
    """The profile the model sees, the competency ids in it and the evidence ids in it (one evidence id per competency: `e-<id>`)."""
    rows, comps, ev = [], set(), set()
    for name in sorted(model.beta):
        m = model.mastery(name)
        if m.sessions == 0:
            continue
        comps.add(name)
        ev.add(f"e-{name}")
        rows.append((0 if m.status in WEAK else 1, m.mean, name, f"{name} | {labels.get(name, name)} | sessions {m.sessions} | right {round(100 * m.mean)} out of 100 | {m.status} | evidence e-{name}"))
    rows.sort()                                       # the weaknesses and hypotheses first, the lowest share first within them (the order the version 2 prompt describes)
    return "\n".join(r[3] for r in rows), comps, ev


def defects(value: dict, model, comps: set[str], ev: set[str], profile: str) -> list[str]:
    out = []
    hyps = value.get("hypotheses", [])
    if len(hyps) > MAX_HYPOTHESES:
        out.append(f"TOO_MANY: at most {MAX_HYPOTHESES} hypotheses")
    allowed_numbers = set(NUMBER.findall(profile))
    for i, h in enumerate(hyps):
        if not h["competencies"] or any(c not in comps for c in h["competencies"]):
            out.append(f"UNKNOWN_COMPETENCY: hypothesis {i + 1} cites a competency that is not in the list")
        if not h["evidence"] or any(e not in ev for e in h["evidence"]):
            out.append(f"UNKNOWN_EVIDENCE: hypothesis {i + 1} cites evidence that is not in the list")
        if any(x not in allowed_numbers for x in NUMBER.findall(ID.sub("", h["text"]))):
            out.append(f"UNGROUNDED_NUMBER: hypothesis {i + 1} states a number that is not in the list")
        if any(x not in comps for x in ID.findall(h["text"])):
            out.append(f"UNKNOWN_COMPETENCY: hypothesis {i + 1} names a competency id that is not in the list")
        strong = [c for c in h["competencies"] if c in comps and model.mastery(c).status not in WEAK]
        if strong:
            out.append(f"NOT_A_WEAKNESS: hypothesis {i + 1} cites {strong}, which the list does not mark as a weakness or hypothesis")
        thin = [c for c in h["competencies"] if c in comps and model.mastery(c).sessions < MIN_SESSIONS]
        if thin and h["confidence"] != "low":
            out.append(f"OVERCONFIDENT: hypothesis {i + 1} has fewer than {MIN_SESSIONS} sessions behind {thin} so its confidence must be low")
    return out


def schema(comps: set[str], ev: set[str]) -> dict:
    return {"type": "object", "required": ["hypotheses"], "additionalProperties": False, "properties": {"hypotheses": {"type": "array", "maxItems": MAX_HYPOTHESES, "items": {
        "type": "object", "required": ["competencies", "evidence", "confidence", "text"], "additionalProperties": False,
        "properties": {"competencies": {"type": "array", "minItems": 1, "maxItems": 4, "items": {"enum": sorted(comps)}, "uniqueItems": True},
                       "evidence": {"type": "array", "minItems": 1, "maxItems": 6, "items": {"enum": sorted(ev)}, "uniqueItems": True},
                       "confidence": {"enum": ["low", "medium", "high"]}, "text": {"type": "string", "minLength": 5, "maxLength": 220}}}}}}


class Interpreter:
    def __init__(self, client, model: str | None = None, timeout: float | None = None, version: int | None = None):
        self.client, self.model, self.timeout, self.version = client, model, timeout, version

    def propose(self, model, labels: dict[str, str] | None = None) -> list[Hypothesis]:
        profile, comps, ev = profile_lines(model, labels or {})
        if self.client is None or len(comps) < 2:
            return []
        r = gateway.run(self.client, "personalisation.interpret", {"profile": profile}, schema=schema(comps, ev), validate=lambda v: defects(v, model, comps, ev, profile), model=self.model,
                        timeout=self.timeout, retries=1, task="interpret", version=self.version)
        if not r.ok:
            return []
        return [Hypothesis(h["competencies"], h["evidence"], h["confidence"], h["text"], "open", model.sessions, r.audit) for h in r.value["hypotheses"]]


class Tracker:
    """Checks stored hypotheses against later evidence. Never changes the learner model."""

    def __init__(self):
        self.items: list[Hypothesis] = []

    def add(self, hyps: list[Hypothesis]) -> None:
        self.items += hyps

    def update(self, model) -> None:
        for h in self.items:
            if h.status != "open" or model.sessions - h.sessions_at_proposal < CHECK_AFTER:
                continue
            states = [model.mastery(c).status for c in h.competencies]
            if any(s == "strength" for s in states):
                h.status = "refuted"
            elif all(s in ("weakness", "hypothesis") for s in states):
                h.status = "confirmed"
