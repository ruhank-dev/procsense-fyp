"""Session evidence: what one finished session showed (p1-personalisation-s01, feature D67).

    ev = extract(report, trainee, session_id, ended_at)        # report = the engine's debrief facts (content/schema/debrief_facts.schema.json)

Made only from the engine's own facts: the scored items (completed steps, missed steps, errors by severity), the actions in order, the events, the credits and penalties per behaviour, and for a generated case
its story and tier. No sentence, no free text and no name is carried over; the trainee is a pseudonym. What the engine does not record yet (spoken acts, hints used, confidence reports, how fast an event was
answered, which actions were interpretations of results) is listed in `unrecorded`: a consumer treats it as unknown, not as zero. Times are UTC. The same session always gives the same record.
"""
from __future__ import annotations

import re

SEVERITY = {"minor": "minor", "major": "major", "critical": "critical"}
TRAINEE = re.compile(r"^[A-Za-z0-9_-]{3,64}$")
UNRECORDED = ["communication_acts", "confidence_reports", "event_response_latency", "hints", "interpretation_actions"]
MAX_ACTIONS = 400


def extract(report: dict, trainee: str, session_id: str, ended_at: str, tier: str = "P0") -> dict:
    """`tier` is the tier of a library case (the report of a generated case carries its own)."""
    if not TRAINEE.match(trainee):
        raise ValueError("the trainee is a pseudonym of 3 to 64 letters, digits, - or _")
    when = {t["seq"]: t["time"] for t in report["timeline"] if t["kind"] == "item"}
    late = {l["what"]: l["minutes"] for l in report["lateness"]}
    steps, errors = [], []
    for i in report["items"]:
        at = (when.get(i["evidence"][0]) if i["evidence"] else None) or ""
        if i["severity"] == "credit":
            steps.append({"item": i["id"], "behaviour": i["behaviour"], "state": "completed", "at": at})
        elif i["severity"] == "missed":
            steps.append({"item": i["id"], "behaviour": i["behaviour"], "state": "missed", "at": at})
        elif i["severity"] in SEVERITY:
            errors.append({"item": i["id"], "behaviour": i["behaviour"], "severity": SEVERITY[i["severity"]], "at": at})
        if i["id"] in late and steps and steps[-1]["item"] == i["id"]:
            steps[-1]["late_minutes"] = late[i["id"]]
    gen = report.get("generated")
    actions = [{"seq": t["seq"], "at": t["time"], "verb": t["text"]} for t in report["timeline"] if t["kind"] == "action"][:MAX_ACTIONS]
    return {"version": 1, "trainee": trainee, "session_id": session_id, "ended_at": ended_at, "case": report["case"], "story": gen["spine"] if gen else report["case"],
            "tier": gen["tier"] if gen else tier, "mode": report["mode"], "generated": bool(gen), "end_state": report["end_state"], "result": report["result"], "steps": steps, "errors": errors,
            "actions": actions, "events": [{"event": e, "response_latency_s": None} for e in report["events"]],
            "competencies": [{"behaviour": c["behaviour"], "credits": c["credits"], "penalties": c["penalties"], "items": c["items"]} for c in report["competencies"] if c["items"] > 0],
            "unrecorded": list(UNRECORDED)}
