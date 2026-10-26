"""The plan composer's further proposals about one case: which events may fire (A5.c2), which what-if branches those realise (A8.c2) and which of the case's problems interact (A4.c2). DT-224.

    extras = ExtrasPlanner(client).propose(case, objectives)       # {"events": [...], "interactions": [...], "audit": [...]} or None; every id has been checked

What decides and what the model does. The candidates are made by code, from the content, with no model: an event is ELIGIBLE when it is approved for practice and the case can meet its precondition (the allergic
reaction needs an allergy in the case; the six vital-sign events need nothing); two problems are CANDIDATES for an interaction when the approved knowledge base (content/kb/relations.json) links both to the same
piece of evidence (the same result, analyte or action), so every interaction is confirmed by the knowledge base before a model is asked. The model only chooses among those candidates, with one sentence of reason
(a closed enum, so it cannot name anything else), and may choose none. The choices then go through `validate`, which re-checks every id against the candidates. A model that is down, slow or wrong gives None: nothing
is restricted and no interaction is added, which is exactly today's behaviour.

What the choices do. `events` becomes `provenance.plan.events` of the package; the engine reads it (ScenarioPackageReader.PlannedEvents -> SessionOptions.PlannedEvents -> EventDirector) and, in practice mode only,
lets just those events fire; assessment ignores it. Each event carries the what-if it realises (`whatif_id`), which is the branch (A8.c2). `interactions` becomes `provenance.plan.interactions` and one grounded sentence
appended to the debrief's `related` line of the case (`apply_to_case`). The model never sees a hidden clinical fact, a result or a number it could copy into a case: it sees problem labels and event cues.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

try:
    from llm import gateway
except ImportError:
    from server.llm import gateway

ROOT = Path(__file__).resolve().parents[2]
EVENTS = ROOT / "content" / "events" / "events.json"
RELATIONS = ROOT / "content" / "kb" / "relations.json"
MAX_EVENTS, MAX_INTERACTIONS = 3, 2
READABLE = {"expects_action": "expects the action", "comes_with": "comes with", "is_in_zone": "is in the zone of", "shown_by": "is shown by", "indicated_in": "is indicated in", "critical_when_high": "is critical when high"}


def _events() -> list[dict]:
    return json.loads(EVENTS.read_text(encoding="utf-8"))["events"]


def eligible_events(case: dict) -> list[dict]:
    """Events a practice session of this case may be restricted to: approved for training, and the precondition can be met by the case. The allergic reaction needs an allergy in the case."""
    out = []
    for e in _events():
        if e["status"] not in ("APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"):
            continue
        if e["needs_flag"] == "allergy.reaction_eligible" and not case.get("allergies"):
            continue
        if e["needs_flag"] not in ("", "allergy.reaction_eligible"):         # an event that waits for something the trainee does (a declined test) is not one a plan can schedule
            continue
        out.append(e)
    return out


def _relations() -> list[dict]:
    return json.loads(RELATIONS.read_text(encoding="utf-8"))["relations"]


def interaction_candidates(case: dict) -> list[dict]:
    """Pairs of problems of the case that the approved knowledge base links to the same evidence. The case keeps the problem ids of the story (spine) it was composed from, so the knowledge base is read under the spine id."""
    spine = case.get("composition", {}).get("spine") or case["id"]
    problems = {p["id"]: p for p in case["problems"]}
    evidence: dict[str, dict[tuple[str, str], str]] = {}
    for r in _relations():
        if r["subject"]["kind"] != "problem" or r["status"] != "approved":
            continue
        sid = r["subject"]["id"]
        if not sid.startswith(spine + "."):
            continue
        pid = sid[len(spine) + 1:]
        if pid in problems:
            evidence.setdefault(pid, {})[(r["predicate"], r["object"]["id"])] = f"{READABLE.get(r['predicate'], r['predicate'])} {r['object']['label']}"
    out = []
    ids = sorted(evidence)
    for i, a in enumerate(ids):
        for b in ids[i + 1:]:
            shared = sorted(set(evidence[a]) & set(evidence[b]))
            if shared:
                out.append({"id": f"{a}+{b}", "problems": [a, b], "labels": [problems[a]["label"], problems[b]["label"]], "shared": [evidence[a][k] for k in shared][:3]})
    return out


def events_schema(ids: list[str]) -> dict:
    return {"type": "object", "required": ["events"], "additionalProperties": False, "properties": {"events": {"type": "array", "minItems": 0, "maxItems": MAX_EVENTS, "items": {
        "type": "object", "required": ["event_id", "reason"], "additionalProperties": False, "properties": {"event_id": {"enum": ids}, "reason": {"type": "string", "minLength": 3, "maxLength": 200}}}}}}


def interactions_schema(ids: list[str]) -> dict:
    return {"type": "object", "required": ["interactions"], "additionalProperties": False, "properties": {"interactions": {"type": "array", "minItems": 0, "maxItems": MAX_INTERACTIONS, "items": {
        "type": "object", "required": ["pair", "reason"], "additionalProperties": False, "properties": {"pair": {"enum": ids}, "reason": {"type": "string", "minLength": 3, "maxLength": 200}}}}}}


@dataclass
class Validated:
    events: list[dict]
    interactions: list[dict]


def validate(case: dict, events: list[dict], interactions: list[dict]) -> list[str]:
    """Problems with a proposal; empty when every event is eligible, none repeats, every pair is a knowledge-base candidate and the reasons are plain sentences."""
    bad = []
    ok_events = {e["id"]: e for e in eligible_events(case)}
    seen = set()
    for e in events:
        if e["event_id"] not in ok_events:
            bad.append(f"INELIGIBLE_EVENT:{e['event_id']}")
        if e["event_id"] in seen:
            bad.append(f"REPEATED_EVENT:{e['event_id']}")
        seen.add(e["event_id"])
        if e.get("whatif_id") != ok_events.get(e["event_id"], {}).get("whatif"):
            bad.append(f"WHATIF_MISMATCH:{e['event_id']}")
    if len(events) > MAX_EVENTS:
        bad.append("TOO_MANY_EVENTS")
    cands = {c["id"]: c for c in interaction_candidates(case)}
    for i in interactions:
        if i["pair"] not in cands:
            bad.append(f"NOT_IN_KNOWLEDGE_BASE:{i['pair']}")
    if len(interactions) > MAX_INTERACTIONS or len({i["pair"] for i in interactions}) != len(interactions):
        bad.append("INTERACTION_COUNT_OR_REPEAT")
    for x in [*events, *interactions]:
        if not re.search(r"[A-Za-z]{3}", x["reason"]) or len(x["reason"]) > 200:
            bad.append("REASON")
    return bad


def apply_to_case(case: dict, interactions: list[dict]) -> dict:
    """The case with one grounded sentence per interaction appended to the debrief's `related` line (the trainee sees why these problems belong together). Nothing else in the case changes."""
    if not interactions:
        return case
    cands = {c["id"]: c for c in interaction_candidates(case)}
    sentences = []
    for i in interactions:
        c = cands[i["pair"]]
        sentences.append(f"'{c['labels'][0]}' and '{c['labels'][1]}' meet in the same findings ({'; '.join(c['shared'][:2])}), so treat them together.")
    case["debrief"]["related"] = (case["debrief"].get("related", "").rstrip() + " " + " ".join(sentences)).strip()
    return case


class ExtrasPlanner:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout

    def propose(self, case: dict, objectives: dict) -> dict | None:
        if self.client is None:
            return None
        audits: list[dict] = []
        events: list[dict] = []
        eligible = eligible_events(case)
        problems = "; ".join(p["label"] for p in sorted(case["problems"], key=lambda p: p["rank"]))
        objective_text = ("; ".join(c["behaviour"] for c in objectives.get("competencies", [])) or "the standard objectives") + ((" ; dimensions: " + ", ".join(objectives["dimensions"])) if objectives.get("dimensions") else "")
        if eligible:
            listing = "\n".join(f"{e['id']}: {e['category']} event, cues: {', '.join(e['cues'])}" for e in eligible)
            r = gateway.run(self.client, "compose_plan.events", {"case": f"{case['presentation']['summary']} Problems: {problems}.", "objectives": objective_text, "events": listing},
                            schema=events_schema([e["id"] for e in eligible]), model=self.model, timeout=self.timeout, retries=1, task="compose_plan_events")
            audits.append(r.audit)
            if r.ok:
                by = {e["id"]: e for e in eligible}
                events = [{"event_id": x["event_id"], "whatif_id": by[x["event_id"]]["whatif"], "reason": x["reason"]} for x in r.value["events"]]
        interactions: list[dict] = []
        cands = interaction_candidates(case)
        if cands:
            listing = "\n".join(f"{c['id']}: '{c['labels'][0]}' and '{c['labels'][1]}' share: {'; '.join(c['shared'])}" for c in cands)
            r = gateway.run(self.client, "compose_plan.interactions", {"case": f"{case['presentation']['summary']} Problems: {problems}.", "pairs": listing},
                            schema=interactions_schema([c["id"] for c in cands]), model=self.model, timeout=self.timeout, retries=1, task="compose_plan_interactions")
            audits.append(r.audit)
            if r.ok:
                interactions = [{"pair": x["pair"], "problems": next(c["problems"] for c in cands if c["id"] == x["pair"]), "reason": x["reason"]} for x in r.value["interactions"]]
        if validate(case, events, interactions):
            return {"events": [], "interactions": [], "audit": audits, "refused": validate(case, events, interactions)}
        return {"events": events, "interactions": interactions, "audit": audits}
