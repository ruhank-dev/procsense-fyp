"""AI debrief (p1-ai-language-s08, EXP-AI-4): the engine's template debrief, reworded by a model only if the strict validator passes.

    out = Rewriter(client).make(report)       # {"template": [...sections...], "ai": [...] or None, "used": "template" | "ai", "reasons": [...], "latency_ms": n}

The report is the engine's debrief facts (debrief_facts.v1). Every item of it becomes a fact with an id, a verdict (credit, penalty, critical or info) and the numbers it contains; the end state and the result are
facts too. The TEMPLATE debrief is made first from the facts alone and is what the trainee sees at once. The model then rewrites the facts into sections with kind wording; its answer replaces the template
only if the grounding validator (server/validation/validator.py, debrief mode) finds nothing: every fact is covered by a section that lists its id, no number changed, no mistake turned into praise or praise
into a mistake, no event, medicine, test, condition or equipment that is not in the facts, no dose, no prescribing phrase. The model is never given the rules of the scoring, the hidden facts of the case or
anything outside the report. If the model is down, slow or refused, the template stays; that is the fallback and it is always complete.
"""
from __future__ import annotations

import json
import re
import time
from pathlib import Path

try:
    from llm import gateway
    from validation.validator import has_term, norm, numbers, validate
except ImportError:
    from server.llm import gateway
    from server.validation.validator import has_term, norm, numbers, validate

ROOT = Path(__file__).resolve().parents[2]
TITLES = ["What went well", "What to improve", "Critical points", "For your information", "How the session ended"]
VERDICT_OF = {"credit": "credit", "penalty": "penalty", "critical": "critical", "teaching": "info", "info": "info"}
SECTION_OF = {"credit": "What went well", "penalty": "What to improve", "critical": "Critical points", "info": "For your information"}


def facts_of(report: dict) -> list[dict]:
    """The facts a debrief may state: one per scored item (plain words from its id, its verdict, its numbers) and one for how the session ended."""
    facts = []
    for i, it in enumerate(report["items"]):
        label = it["id"].split(".", 1)[1].replace("_", " ")
        verdict = VERDICT_OF.get(it["status"], "info")
        facts.append({"id": f"f{i + 1}", "text": f"{label} ({verdict})", "verdict": verdict, "numbers": numbers(label)})
    end = report["end_state"].replace("_", " ").lower() if report["end_state"] else "not reached"
    facts.append({"id": f"f{len(facts) + 1}", "text": f"the session ended: {end}; result {report['result'].replace('_', ' ').lower()}", "verdict": "info", "numbers": []})
    return facts


def template(facts: list[dict]) -> list[dict]:
    """The debrief made from the facts alone, in fixed wording. Always complete."""
    secs = []
    for title, verdicts in (("What went well", ("credit",)), ("What to improve", ("penalty",)), ("Critical points", ("critical",)), ("For your information", ("info",))):
        rows = [f for f in facts if f["verdict"] in verdicts]
        if rows:
            secs.append({"title": title, "text": "; ".join(f["text"] for f in rows) + ".", "fact_ids": [f["id"] for f in rows]})
    return secs


def schema(fact_ids: list[str]) -> dict:
    return {"type": "object", "required": ["sections"], "additionalProperties": False, "properties": {"sections": {"type": "array", "minItems": 1, "maxItems": 8, "items": {
        "type": "object", "required": ["title", "text", "fact_ids"], "additionalProperties": False,
        "properties": {"title": {"enum": TITLES}, "text": {"type": "string", "minLength": 5, "maxLength": 600}, "fact_ids": {"type": "array", "minItems": 1, "items": {"enum": fact_ids}, "uniqueItems": True}}}}}}


def validator_context(facts: list[dict], lexicon: dict) -> dict:
    text = " ".join(f["text"] for f in facts)
    joined = norm(text)
    entities = [t for cat in ("drug", "test", "condition", "allergen", "equipment") for t in lexicon["categories"][cat] if has_term(joined, t)]
    return {"allowed_intents": [], "facts": [], "allowed_entities": entities, "numbers": numbers(text), "dose_text": [], "debrief_facts": facts, "log_events": []}


def check(sections: list[dict], facts: list[dict], lexicon: dict) -> list[str]:
    res = validate({"kind": "debrief", "sections": sections}, validator_context(facts, lexicon), lexicon)
    codes = set(res.codes)
    by = {f["id"]: f["verdict"] for f in facts}
    for s in sections:
        kinds = {by[i] for i in s["fact_ids"] if i in by}
        if "credit" in kinds and kinds & {"penalty", "critical"}:
            codes.add("DEBRIEF_MIXED_VERDICT")          # the validator judges the wording of a section by the verdict of its facts, so a section may not mix praise and mistakes (found by the route test)
    return sorted(codes)


def cover_missing(sections: list[dict], facts: list[dict]) -> list[str]:
    """Completes a rewrite that left facts out, in place: each missing fact is added to a section whose facts all share its verdict (its fixed template wording is appended and its id listed), or to a new
    section of its own. Returns the ids it added. The wording added is the template's, so no new claim can come from this step, and the strict check still runs on the completed sections; it makes a
    dropped fact cost no second model call. Facts the model got wrong (a changed verdict, a changed number) are never repaired here: those go back to the model once and then to the template."""
    covered = {i for s in sections for i in s["fact_ids"]}
    by = {f["id"]: f for f in facts}
    added = []
    for f in facts:
        if f["id"] in covered:
            continue
        home = next((s for s in sections if s["fact_ids"] and {by[i]["verdict"] for i in s["fact_ids"] if i in by} == {f["verdict"]}), None)
        if home is None:
            sections.append({"title": SECTION_OF.get(f["verdict"], "For your information"), "text": f["text"][0].upper() + f["text"][1:] + ".", "fact_ids": [f["id"]]})
        else:
            home["text"] = home["text"].rstrip() + " " + f["text"][0].upper() + f["text"][1:] + "."
            home["fact_ids"].append(f["id"])
        added.append(f["id"])
    return added


class Rewriter:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout
        self.lexicon = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))

    def make(self, report: dict) -> dict:
        facts = facts_of(report)
        base = template(facts)
        out = {"template": base, "ai": None, "used": "template", "reasons": [], "latency_ms": 0, "facts": len(facts)}
        if self.client is None:
            out["reasons"] = ["NO_MODEL"]
            return out
        t = time.monotonic()
        text = "\n".join(f"{f['id']} [{f['verdict']}]: {f['text']}" for f in facts)
        completed: list[str] = []
        r = gateway.run(self.client, "debrief.rewrite", {"titles": ", ".join(TITLES), "facts": text}, schema=schema([f["id"] for f in facts]),
                        validate=lambda v: (completed.clear(), completed.extend(cover_missing(v["sections"], facts)), [f"{c}: refused" for c in check(v["sections"], facts, self.lexicon)])[2], model=self.model, timeout=self.timeout, retries=1, task="debrief")
        out["latency_ms"] = round((time.monotonic() - t) * 1000)
        if r.ok:
            out["ai"], out["used"] = r.value["sections"], "ai"
            out["completed_from_template"] = completed
        else:
            out["reasons"] = [d.split(":")[0] for d in r.audit["defects"]] or [r.audit["error"] or "REJECTED"]
        return out
