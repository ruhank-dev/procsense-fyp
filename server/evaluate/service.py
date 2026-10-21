"""The evaluation service (p1-ai-language-s22, ADR-0025): asks the reader for the structured judgement of one utterance and shapes the answer. It has no scoring, no classifier, no cap and no semantic veto: the model is the semantic evaluator, and the engine alone turns a
judgement and its own state into evidence and score. Fail-safe: when the model cannot give a valid judgement the answer is UNAVAILABLE with the menu of existing verified actions, and no speech credit follows; credit is never guessed."""
from __future__ import annotations

import json
from pathlib import Path

from server.evaluate.reader import Outcome, Reader

MAPPING = Path(__file__).resolve().parents[2] / "content" / "assessment" / "mapping.json"


def menu_for(context: dict) -> list[dict]:
    """The existing menu actions of every behaviour whose window holds a running step, so the trainee can always act without speech."""
    behaviours = json.loads(MAPPING.read_text(encoding="utf-8"))["behaviours"]
    steps = set(context.get("steps") or [])
    return [{"behaviour": b["id"], "label": b["label"], "actions": list(b["actions"])} for b in behaviours if steps & set(b["steps"]) and b["actions"]]


def unavailable(context: dict, utterance_id: str, reasons: list[str]) -> dict:
    return {"decision": "UNAVAILABLE", "utterance_id": utterance_id, "acts": [], "claims_physical_action": False, "physical_action": "", "critical_topic": False, "menu": menu_for(context), "reasons": reasons, "source": "evaluate"}


def evaluated(outcome: Outcome, utterance_id: str) -> dict:
    e = outcome.evaluation
    out = e.to_dict()
    out["acts"] = [{**a, "evidence": a["evidence"][:300]} for a in out["acts"]]
    return {"decision": "EVALUATED", "utterance_id": utterance_id, **out, "menu": [], "reasons": [], "source": "evaluate"}


def evaluate(reader: Reader | None, context: dict, utterance: str, utterance_id: str) -> tuple[dict, Outcome | None]:
    """returns the answer for the route and the reader's outcome (for the audit row: model, attempts, codes; never the words)."""
    if reader is None:
        return unavailable(context, utterance_id, ["NOT_CONFIGURED"]), None
    outcome = reader.read(context, utterance, key=utterance_id)
    if outcome.status != "VALID" or outcome.evaluation is None:
        codes = ["EVAL_" + outcome.status] + ["EVAL_" + c.split(":")[0] for c in outcome.codes[-1:]]
        return unavailable(context, utterance_id, codes), outcome
    return evaluated(outcome, utterance_id), outcome
