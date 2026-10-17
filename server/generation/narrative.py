"""AI composition of the patient's own words: a speaking style over facts that do not change (p1-live-scenario-generation-s04, stage LG-06; ADR-0005: language is the product, values are not).

    narrator = Narrator(client)                                   # or Narrator(None): every line stays as authored
    report = narrator.apply(case, seed)                           # rewrites some of the patient's lines in place, returns what it did

A composed case already has its facts and its authored wording. This step gives the patient a speaking style (persona, content/ai/persona.json) by rewriting a few lines of what the patient says: the reason for
admission and up to MAX_LINES history answers. The model is told the style, the patient's age and sex word, the topic and the original line, and returns one line. A line is accepted only if nothing a person
could check has changed: the same numbers, the same medicines, tests, conditions, allergens and equipment (the lexicon of content/ai/lexicon.json), no new ones, no prescribing phrase, and a length near the
original. Otherwise the model is asked once more with the reasons; after that the authored line stays (the template fallback, FB-01). The model never sees a hidden fact it was not given: it is given only the
line it rewrites. The structured case (results, orders, problems, decisions) is never passed to the model and never changed here.
"""
from __future__ import annotations

import hashlib
import json
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
try:                                         # the generation package is used with server/ on the path (tools) and as part of the server application
    from llm import gateway
    from validation.validator import CATEGORIES, PRESCRIBE, content_words, has_term, norm, numbers
except ImportError:
    from server.llm import gateway
    from server.validation.validator import CATEGORIES, PRESCRIBE, content_words, has_term, norm, numbers

PERSONA = ROOT / "content" / "ai" / "persona.json"
LEXICON = ROOT / "content" / "ai" / "lexicon.json"
MAX_LINES = 5
MIN_CHARS = 25                       # shorter answers ("No.") are left alone
LENGTH_FACTOR = (0.5, 2.2)


def persona_table() -> dict:
    return json.loads(PERSONA.read_text(encoding="utf-8"))


def entities(text: str, lexicon: dict) -> set[str]:
    n = norm(text)
    return {f"{cat}:{term}" for cat in CATEGORIES for term in lexicon["categories"][cat] if has_term(n, term)}


NUMBER_WORDS = {"one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
                "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety", "hundred", "thousand", "first", "second", "third", "once", "twice", "half", "couple", "few", "several"}
TIME_WORDS = {"second", "seconds", "minute", "minutes", "hour", "hours", "day", "days", "week", "weeks", "month", "months", "year", "years", "ago", "since", "yesterday", "tomorrow", "tonight", "morning",
              "afternoon", "evening", "night", "waiting", "wait", "waited", "long", "while", "recently", "lately", "always", "never", "ever"}
NEGATIONS = {"no", "not", "never", "none", "nothing", "nobody", "neither", "nor", "dont", "didnt", "doesnt", "havent", "hasnt", "wasnt", "cant", "wont", "cannot", "without"}
RETAIN = 0.6                         # share of the original's content words (by their first five letters) the new line must keep


def _words(text: str) -> list[str]:
    return norm(text.replace("'", "").replace("\u2019", "")).split(" ")


def _stems(text: str) -> set[str]:
    return {w[:5] for w in content_words(text.replace("'", "").replace("\u2019", ""))}


def defects(original: str, new: str, lexicon: dict) -> list[str]:
    """What is wrong with a rewritten line; empty when nothing checkable has changed. The checks are lexical and cannot prove a line is faithful (the evaluation tool writes every accepted pair for a person to read)."""
    out = []
    ow, nw = _words(original), _words(new)
    if sorted(numbers(original)) != sorted(numbers(new)):
        out.append(f"NUMBERS: the original has {sorted(numbers(original))} and the new line has {sorted(numbers(new))}; keep the numbers exactly")
    added = (set(nw) & NUMBER_WORDS) - set(ow)
    if added:
        out.append("NUMBER_WORD: you added " + ", ".join(sorted(added)) + "; do not add counts or amounts")
    time_added = (set(nw) & TIME_WORDS) - set(ow)
    if time_added:
        out.append("TIME: you added " + ", ".join(sorted(time_added)) + "; do not add anything about time, waiting or how long")
    if bool(set(ow) & NEGATIONS) != bool(set(nw) & NEGATIONS):
        out.append("NEGATION: the original " + ("denies something" if set(ow) & NEGATIONS else "does not deny anything") + " and the new line does the opposite; keep yes and no exactly")
    before, after = entities(original, lexicon), entities(new, lexicon)
    if after - before:
        out.append("NEW_ENTITY: you mentioned " + ", ".join(sorted(e.split(":", 1)[1] for e in after - before)) + ", which the original does not; do not add any medicine, test, condition or allergen")
    if before - after:
        out.append("DROPPED_ENTITY: you left out " + ", ".join(sorted(e.split(":", 1)[1] for e in before - after)) + "; keep every fact of the original")
    keep = _stems(original)
    if keep and len(keep & _stems(new)) / len(keep) < RETAIN:
        out.append("FACTS_LOST: the new line keeps too few of the original's words; say the same things, in your own style")
    if PRESCRIBE.search(new):
        out.append("PRESCRIBE: the patient does not tell anyone to give or prescribe anything")
    lo, hi = LENGTH_FACTOR
    if not lo * len(original) - 10 <= len(new) <= hi * len(original) + 40:
        out.append(f"LENGTH: the original is {len(original)} characters and the new line {len(new)}; stay close to the original length")
    return out


def lines_of(case: dict) -> list[dict]:
    """The lines that may be rewritten: id, topic, original. Variant lines belong to a path and are left as authored."""
    out = []
    if len(case.get("reason_answer", "")) >= MIN_CHARS:
        out.append({"id": "reason_answer", "topic": "the reason for the operation", "original": case["reason_answer"]})
    for h in case["history"]:
        if h.get("variant") is None and len(h.get("answer", "")) >= MIN_CHARS:
            out.append({"id": h["id"], "topic": h["topic"].replace("_", " "), "original": h["answer"]})
    return out[:MAX_LINES + 1]


def _set(case: dict, line_id: str, text: str) -> None:
    if line_id == "reason_answer":
        case["reason_answer"] = text
        return
    for h in case["history"]:
        if h["id"] == line_id:
            h["answer"] = text
            return
    raise KeyError(line_id)


class Narrator:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout
        self.lexicon = json.loads(LEXICON.read_text(encoding="utf-8"))

    def style_for(self, seed: int) -> dict:
        table = persona_table()
        rng = random.Random(seed * 7 + 11)
        tags = rng.sample(sorted(table["tags"]), 2) if len(table["tags"]) >= 2 else sorted(table["tags"])
        return {"tags": tags, "wording": "; ".join(table["tags"][t] for t in tags)}

    def apply(self, case: dict, seed: int) -> dict:
        """Rewrites lines in `case` in place. Returns {"persona": [...], "lines": [{"id", "method", ...}], "audit": [...]}."""
        style = self.style_for(seed)
        report = {"persona": style["tags"], "lines": [], "audit": []}
        if self.client is None:
            report["lines"] = [{"id": ln["id"], "method": "template"} for ln in lines_of(case)]
            return report
        sex_word = "woman" if case["identity"]["sex"] == "F" else "man"
        for ln in lines_of(case):
            result = gateway.run(self.client, "compose.narrative", {"style": style["wording"], "age": str(case["identity"]["age"]), "sex_word": sex_word, "topic": ln["topic"], "original": ln["original"]},
                                 validate=lambda v, o=ln["original"]: defects(o, v["text"], self.lexicon), timeout=self.timeout, model=self.model, task="compose_narrative")
            report["audit"].append(result.audit)
            if result.ok:
                _set(case, ln["id"], result.value["text"].strip())
                report["lines"].append({"id": ln["id"], "method": "model", "attempts": len(result.attempts), "original_sha256": hashlib.sha256(ln["original"].encode("utf-8")).hexdigest()[:16]})
            else:
                report["lines"].append({"id": ln["id"], "method": "template", "attempts": len(result.attempts), "why": result.audit["defects"][-1][:120] if result.audit["defects"] else result.audit["error"]})
        return report
