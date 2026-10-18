"""The trap wording writer (feature A7, capability A7.c2; DT-224): a model rewords the WRONG options of a case's decisions so that they read as something a busy clinician might plausibly choose.

    report = TrapWriter(client).apply(case, seed)        # rewrites wrong options of case["decisions"] in place; {"lines": [{"id", "method": "model" | "template", ...}], "audit": [...]}

A trap is fair when the trainee could have found why it is wrong (server/generation/quality.py `trap_fairness`: the consequence names something the case holds). The writer changes ONLY the text of a wrong option: its id, the
`correct` flags, the consequence (which carries the cue that makes the option fair) and every correct option stay as authored. The new wording must pass the same lexical checks as the patient wording (narrative.defects:
the same numbers, no added count or time, the same yes or no, no medicine, test, condition or allergen added or dropped, enough of the original's words kept, similar length) and must not start to say the opposite
(no wrong option may gain words that make it a statement of the correct option). A wording that fails, a model that is down, or a case with no wrong option leaves the authored text: the template baseline. The plan's acceptance
(plausibility above the template's) needs people to rate the wording (EX-03); what is measured here is that the checks hold and fairness does not fall (tools/ai/eval_traps.py).
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

try:
    from generation.narrative import LEXICON, defects
    from generation.quality import trap_fairness
    from llm import gateway
except ImportError:
    from server.generation.narrative import LEXICON, defects
    from server.generation.quality import trap_fairness
    from server.llm import gateway

MIN_CHARS = 20


def wrong_options(case: dict) -> list[dict]:
    out = []
    for d in case["decisions"]:
        for o in d["options"]:
            if not o["correct"] and len(o["text"]) >= MIN_CHARS:
                out.append({"id": o["id"], "prompt": d["prompt"], "original": o["text"]})
    return out


class TrapWriter:
    def __init__(self, client, model: str | None = None, timeout: float | None = None, limit: int = 4):
        self.client, self.model, self.timeout, self.limit = client, model, timeout, limit
        self.lexicon = json.loads(Path(LEXICON).read_text(encoding="utf-8"))

    def apply(self, case: dict, seed: int) -> dict:
        report = {"lines": [], "audit": []}
        options = wrong_options(case)[:self.limit]
        if self.client is None:
            report["lines"] = [{"id": o["id"], "method": "template"} for o in options]
            return report
        for o in options:
            r = gateway.run(self.client, "trap.reword", {"prompt": o["prompt"], "original": o["original"]}, validate=lambda v, orig=o["original"]: defects(orig, v["text"], self.lexicon),
                            timeout=self.timeout, model=self.model, task="trap_reword")
            report["audit"].append(r.audit)
            new = r.value["text"].strip() if r.ok else ""
            if r.ok and new == o["original"].strip():
                report["lines"].append({"id": o["id"], "method": "unchanged", "attempts": len(r.attempts)})
            elif r.ok:
                before = trap_fairness(case)[0]
                for d in case["decisions"]:
                    for opt in d["options"]:
                        if opt["id"] == o["id"] and not opt["correct"]:
                            opt["text"] = new
                if trap_fairness(case)[0] < before:                          # fairness must not fall: put the authored text back
                    for d in case["decisions"]:
                        for opt in d["options"]:
                            if opt["id"] == o["id"] and not opt["correct"]:
                                opt["text"] = o["original"]
                    report["lines"].append({"id": o["id"], "method": "template", "attempts": len(r.attempts), "why": "TRAP_FAIRNESS_WOULD_FALL"})
                    continue
                report["lines"].append({"id": o["id"], "method": "model", "attempts": len(r.attempts), "original_sha256": hashlib.sha256(o["original"].encode("utf-8")).hexdigest()[:16]})
            else:
                report["lines"].append({"id": o["id"], "method": "template", "attempts": len(r.attempts), "why": (r.audit["defects"][-1][:120] if r.audit["defects"] else r.audit["error"])})
        return report
