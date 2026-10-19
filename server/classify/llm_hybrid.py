"""Rules first, the 4B model only where the rules cannot decide (approach B of docs/decisions/ADR-0020).

    clf = RulesThenModel(client)                       # client: llm.client.OllamaClient or None
    r = clf.classify("Could I ask you to describe how the pain moves?")

1. The deterministic matcher (server/classify/rules.py) answers first. A credit it gives stands; a sentence it refuses for a SAFETY reason (a critical-content cue, a denial/hypothetical/report/broken-off sentence, a self-correction) is never put to the model: the model cannot reverse
   a deterministic refusal.
2. Only a sentence for which no rule fired, or two rules disagreed, is put to the 4B model (prompt `classify.interpret@1`, the schema's `act` enum is the ordinary acts plus NONE, so it cannot name a critical act or an unknown id). The answer carries the act, whether the speaker actually performs it
   (assertion: actual / intended / hypothetical / denied / reported / none), who performs it, the words copied from the sentence as evidence, and a flag for doubt.
3. The answer is a PROPOSAL and is credited only if it passes every deterministic check: the act is an ordinary act, assertion is `actual`, the actor is the trainee, the evidence is a verbatim part of the sentence, the model did not flag doubt, no critical cue matches, the sentence is
   not non-assertive by the regular-expression check, and the sentence does not have two readings in the rules. The model's stated certainty is never used as proof. A model that is down, slow, or breaks the schema gives no credit.
The model cannot name, award or influence a critical act, and the scoring engine never reads this result (ADR-0005); it is evidence for the debrief.
"""
from __future__ import annotations

import re

try:
    from classify.assertion import non_assertive
    from classify.classifier import Result, taxonomy
    from classify.guard import critical_cue
    from classify.rules import RuleActClassifier
    from llm import gateway
except ImportError:
    from server.classify.assertion import non_assertive
    from server.classify.classifier import Result, taxonomy
    from server.classify.guard import critical_cue
    from server.classify.rules import RuleActClassifier
    from server.llm import gateway

SAFETY_REFUSALS = ("CRITICAL_CUE", "NON_ASSERTIVE", "SELF_CORRECTION")


def _norm(t: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9' ]+", " ", t.lower()).split())


class RulesThenModel:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout
        self.rules = RuleActClassifier()
        tax = taxonomy()["acts"]
        self.critical = {t["id"] for t in tax if t["critical"]}
        self.ordinary = [t for t in tax if not t["critical"] and t["id"] != "NONE"]
        self.listing = "\n".join(f"{t['id']}: {t['definition']}" for t in self.ordinary)
        self.model_name = model

    def schema(self) -> dict:
        ids = [t["id"] for t in self.ordinary] + ["NONE"]
        return {"type": "object", "required": ["act", "assertion", "actor", "evidence", "unsure"], "additionalProperties": False, "properties": {
            "act": {"enum": ids}, "assertion": {"enum": ["actual", "intended", "hypothetical", "denied", "reported", "none"]}, "actor": {"enum": ["trainee", "other", "unclear"]},
            "evidence": {"type": "string", "maxLength": 200}, "unsure": {"type": "boolean"}}}

    def validate(self, sentence: str, v: dict) -> str:
        """The reason a model answer is refused, or an empty string when every deterministic check passes."""
        if v["act"] == "NONE" or v["act"] in self.critical:
            return "MODEL_NONE"
        if v["assertion"] != "actual":
            return "MODEL_ASSERTION:" + v["assertion"]
        if v["actor"] != "trainee":
            return "MODEL_ACTOR:" + v["actor"]
        if v["unsure"]:
            return "MODEL_UNSURE"
        ev = _norm(v["evidence"])
        if len(ev) < 3 or ev not in _norm(sentence):
            return "EVIDENCE_NOT_IN_SENTENCE"
        if critical_cue(sentence):
            return "CRITICAL_CUE"
        if (kind := non_assertive(sentence)):
            return f"NON_ASSERTIVE:{kind}"
        return ""

    def classify(self, sentence: str) -> Result:
        r = self.rules.classify(sentence)
        if r.credited or r.refused.split(":")[0] in SAFETY_REFUSALS:
            return Result(r.act, r.confidence, r.credited, "rules+model", r.critical, "", r.refused)
        if self.client is None:
            return Result("NONE", 0.0, False, "unavailable", False, "NO_MODEL", r.refused)
        g = gateway.run(self.client, "classify.interpret", {"acts": self.listing, "sentence": sentence}, schema=self.schema(), model=self.model, timeout=self.timeout, retries=0, task="classify_interpret")
        if not g.ok:
            return Result("NONE", 0.0, False, "unavailable", False, "MODEL_UNAVAILABLE", r.refused)
        why = self.validate(sentence, g.value)
        act = g.value["act"]
        return Result(act, 0.0, not why, "rules+model", act in self.critical, "", why)
