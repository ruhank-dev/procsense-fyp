"""Communication act classifier (p1-ai-language-s07, EXP-AI-3): a short language-model call with a closed list of acts, calibrated thresholds, and a guard for the critical acts.

    clf = ActClassifier(client)
    r = clf.classify("Could you tell me about your last meal?")      # Result(act, confidence, credited, source, error)

The model sees only the sentence and the list of act ids with their definitions (content/assessment/taxonomy.json); the schema's enum is that list plus NONE, so it cannot answer an act that does not exist.
An act is `credited` (counted as evidence of the behaviour) only if its confidence reaches the threshold calibrated for it on the dev split (content/assessment/thresholds.json: the lowest threshold with precision
at least 0.8; an act that never reaches that precision gets no threshold and is never credited). CRITICAL ACTS ARE NEVER CREDITED FROM SPEECH: they are recorded (so the debrief can say they were heard) and the engine
credits them from the form or menu action. A model that is down, slow or wrong gives `source: "unavailable"` and nothing is credited; the menu path is unchanged. The classification is asynchronous evidence: it
never blocks an action.
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

try:
    from classify.guard import critical_cue
    from llm import gateway
except ImportError:
    from server.classify.guard import critical_cue
    from server.llm import gateway

ROOT = Path(__file__).resolve().parents[2]
TAXONOMY = ROOT / "content" / "assessment" / "taxonomy.json"
THRESHOLDS = ROOT / "content" / "assessment" / "thresholds.json"
MIN_PRECISION = 0.8


@dataclass
class Result:
    act: str
    confidence: float
    credited: bool
    source: str                      # "model" or "unavailable"
    critical: bool = False
    error: str = ""


def taxonomy() -> dict:
    return json.loads(TAXONOMY.read_text(encoding="utf-8"))


def acts_text(tax: dict) -> str:
    return "\n".join(f"{a['id']}: {a['definition']}" for a in tax["acts"])


def schema(tax: dict) -> dict:
    return {"type": "object", "required": ["act", "confidence"], "additionalProperties": False,
            "properties": {"act": {"enum": [a["id"] for a in tax["acts"]]}, "confidence": {"type": "number", "minimum": 0, "maximum": 1}}}


def load_thresholds() -> dict:
    return json.loads(THRESHOLDS.read_text(encoding="utf-8")) if THRESHOLDS.exists() else {"default": None, "per_act": {}}


def calibrate(dev: list[tuple[str, str, float]], tax: dict, min_precision: float = MIN_PRECISION) -> dict:
    """dev: [(true act, predicted act, confidence)]. For each act the lowest confidence threshold at which the predictions of that act on the dev split reach `min_precision`
    (with at least 3 predictions at or above it); an act that never does has no threshold (None) and is not credited."""
    out = {}
    for a in tax["acts"]:
        if a["id"] == "NONE":
            continue
        preds = sorted(((c, t == a["id"]) for t, p, c in dev if p == a["id"]), reverse=True)
        best = None
        for i in range(len(preds)):
            top = preds[: i + 1]
            if len(top) >= 3 and sum(ok for _, ok in top) / len(top) >= min_precision:
                best = top[-1][0]
        out[a["id"]] = best
    return out


class ActClassifier:
    def __init__(self, client, model: str | None = None, timeout: float | None = None, thresholds: dict | None = None):
        self.client, self.model, self.timeout = client, model, timeout
        self.tax = taxonomy()
        self.critical = {a["id"] for a in self.tax["acts"] if a["critical"]}
        self.thresholds = (thresholds if thresholds is not None else load_thresholds()).get("per_act", {})
        self.schema = schema(self.tax)
        self.acts = acts_text(self.tax)

    def credit_allowed(self, act: str, confidence: float) -> bool:
        if act in self.critical or act == "NONE":
            return False
        t = self.thresholds.get(act)
        return t is not None and confidence >= t

    def classify(self, sentence: str) -> Result:
        if self.client is None:
            return Result("NONE", 0.0, False, "unavailable", error="NO_MODEL")
        r = gateway.run(self.client, "classify.acts", {"acts": self.acts, "sentence": sentence}, schema=self.schema, model=self.model, timeout=self.timeout, retries=0, task="classify")
        if not r.ok:
            return Result("NONE", 0.0, False, "unavailable", error=r.audit["error"] or "REJECTED")
        act, conf = r.value["act"], float(r.value["confidence"])
        return Result(act, conf, self.credit_allowed(act, conf) and not critical_cue(sentence), "model", act in self.critical)      # a sentence that touches critical content is never credited under a non-critical act (guard.py)
