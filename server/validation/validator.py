"""The grounding validator: nothing an AI writes reaches a trainee unchecked (p1-ai-language s01, guide B-R2, plan tests T-GV1 to T-GV6).

    from validation.validator import validate
    result = validate(output, context, lexicon)      # result.ok, result.codes

Two kinds of output are checked.
  utterance  {"kind": "utterance", "role", "intent", "text", "fact_ids"}      a patient or staff line
  debrief    {"kind": "debrief", "sections": [{"title", "text", "fact_ids"}]}  a rewritten debrief

What is checked, in words:
  SCHEMA                    the shape and the enums are right and nothing extra is present
  UNKNOWN_INTENT            an utterance's intent is one the character is allowed to use now
  UNKNOWN_FACT              every fact id exists in the context the model was given
  OFF_CARD_ENTITY           no drug, test, condition, allergen or piece of equipment the case does not mention
  UNGROUNDED_NUMBER         every number in the text is a number of the context
  DISCLOSURE_LEAK           no hidden fact is told before the trainee has earned it
  DOSE_STATEMENT            a dose (a number with mg, g, ml, units...) only as the exact approved example text
  PRESCRIBE_PHRASING        no telling the trainee to give or prescribe something
  DEBRIEF_MISSING_FACT      every debrief fact is covered by a section
  DEBRIEF_NUMBER_CHANGED    a fact's numbers appear unchanged where the fact is used
  DEBRIEF_VERDICT_CHANGED   a section about only good (or only bad) facts does not say the opposite
  DEBRIEF_NEW_EVENT         no event that is not in the session log
The same algorithm is in src/ProcSense.Core/AIContext/Validator.cs, and both are run on tests/vectors/grounding_vectors.json (T-GV5). This module reads no model, no network and no clock.
"""
from __future__ import annotations

import re
from dataclasses import dataclass

CATEGORIES = ("drug", "test", "condition", "allergen", "equipment")
UTTERANCE_KEYS = {"kind", "role", "intent", "text", "fact_ids"}
DEBRIEF_KEYS = {"kind", "sections"}
SECTION_KEYS = {"title", "text", "fact_ids"}
MAX_UTTERANCE = 400
FILLER = {"the", "a", "an", "and", "or", "of", "to", "in", "on", "is", "was", "were", "it", "i", "my", "me", "you", "your", "for", "with", "at", "by", "that", "this", "have", "has", "had", "not", "no", "yes", "be", "been", "are", "am"}
NEGATIVE = ["missed", "failed", "omitted", "did not", "didn't", "wrong", "error", "mistake", "unsafe", "forgot", "skipped"]
POSITIVE = ["well done", "correct", "good", "excellent", "right", "great", "successfully"]
NUMBER = re.compile(r"(?<![\w.])\d+(?:\.\d+)?(?![\w])")
DOSE = re.compile(r"(?<![\w.])\d+(?:\.\d+)?\s?(?:mg|mcg|micrograms?|ml|units?|iu|g)(?![\w/])", re.IGNORECASE)
PRESCRIBE = re.compile(r"\b(?:you should|i recommend|i suggest|please|let's|lets)\s+(?:give|prescribe|administer|start|giving|prescribing)\b|\bprescrib(?:e|ing)\b", re.IGNORECASE)


@dataclass(frozen=True)
class Result:
    codes: tuple[str, ...]

    @property
    def ok(self) -> bool:
        return not self.codes


def norm(text: str) -> str:
    """Lower case, everything that is not a letter, digit, hyphen or dot between digits becomes a space, runs of spaces become one."""
    t = text.lower().replace("’", "'")
    t = re.sub(r"(?<=\d)\.(?=\d)", "\u0001", t)            # keep the point inside a number
    t = re.sub(r"[^a-z0-9\-\u0001]+", " ", t)
    return re.sub(r" +", " ", t.replace("\u0001", ".")).strip()


def has_term(normalised: str, term: str) -> bool:
    padded = " " + normalised + " "
    t = norm(term)
    return (" " + t + " ") in padded or (" " + t + "s ") in padded


def numbers(text: str) -> list[str]:
    out = []
    for m in NUMBER.findall(text):
        out.append(m[:-2] if m.endswith(".0") else m)
    return out


def content_words(value: str) -> list[str]:
    return [w for w in norm(value).split(" ") if w and w not in FILLER and not w.isdigit() and len(w) > 2]


def _schema(output: dict) -> bool:
    if not isinstance(output, dict):
        return False
    kind = output.get("kind")
    if kind == "utterance":
        return (set(output) == UTTERANCE_KEYS and all(isinstance(output[k], str) for k in ("role", "intent", "text")) and 0 < len(output["text"]) <= MAX_UTTERANCE
                and isinstance(output["fact_ids"], list) and all(isinstance(x, str) for x in output["fact_ids"]))
    if kind == "debrief":
        s = output.get("sections")
        return (set(output) == DEBRIEF_KEYS and isinstance(s, list) and len(s) > 0
                and all(isinstance(x, dict) and set(x) == SECTION_KEYS and isinstance(x["title"], str) and isinstance(x["text"], str) and x["text"] != ""
                        and isinstance(x["fact_ids"], list) and all(isinstance(f, str) for f in x["fact_ids"]) for x in s))
    return False


def validate(output: dict, context: dict, lexicon: dict) -> Result:
    codes: set[str] = set()
    if not _schema(output):
        return Result(("SCHEMA",))
    texts = [output["text"]] if output["kind"] == "utterance" else [s["text"] for s in output["sections"]]
    fact_ids = list(output["fact_ids"]) if output["kind"] == "utterance" else [f for s in output["sections"] for f in s["fact_ids"]]
    whole = " ".join(texts)
    normal = norm(whole)

    if output["kind"] == "utterance":
        if output["intent"] not in context["allowed_intents"]:
            codes.add("UNKNOWN_INTENT")
        known = {f["id"] for f in context["facts"]}
    else:
        known = {f["id"] for f in context["debrief_facts"]}
    if any(f not in known for f in fact_ids):
        codes.add("UNKNOWN_FACT")

    allowed: set[str] = set()
    for a in context["allowed_entities"]:
        n = norm(a)
        allowed.update((n, n + "s"))                                   # singular and plural of an allowed word are the same word
        if n.endswith("s"):
            allowed.add(n[:-1])
    for cat in CATEGORIES:
        for term in lexicon["categories"][cat]:
            if norm(term) not in allowed and has_term(normal, term):
                codes.add("OFF_CARD_ENTITY")

    allowed_numbers = {n[:-2] if n.endswith(".0") else n for n in context["numbers"]}
    if output["kind"] == "debrief":
        for f in context["debrief_facts"]:
            allowed_numbers.update(f.get("numbers", []))
    if any(n not in allowed_numbers for n in numbers(whole)):
        codes.add("UNGROUNDED_NUMBER")

    if output["kind"] == "utterance":
        for f in context["facts"]:
            if f.get("disclosed") or f.get("disclosable_now"):
                continue
            value = f["value"]
            nums = numbers(value)
            words = content_words(value)
            tokens = set(normal.split(" "))
            if nums and all(n in numbers(whole) for n in nums):
                codes.add("DISCLOSURE_LEAK")
            elif len(words) >= 2 and sum(1 for w in words if w in tokens) / len(words) >= 0.8:
                codes.add("DISCLOSURE_LEAK")

    for m in DOSE.finditer(whole):
        if not any(example in whole for example in context.get("dose_text", [])):
            codes.add("DOSE_STATEMENT")
            break
    if PRESCRIBE.search(whole):
        codes.add("PRESCRIBE_PHRASING")

    if output["kind"] == "debrief":
        facts = {f["id"]: f for f in context["debrief_facts"]}
        covered = set(fact_ids)
        if any(f not in covered for f in facts):
            codes.add("DEBRIEF_MISSING_FACT")
        for s in output["sections"]:
            sn = norm(s["text"])
            used = [facts[f] for f in s["fact_ids"] if f in facts]
            for f in used:
                if any(n not in numbers(s["text"]) for n in f.get("numbers", [])):
                    codes.add("DEBRIEF_NUMBER_CHANGED")
            polarity = {f["verdict"] for f in used}
            if polarity == {"credit"} and any(has_term(sn, w) for w in NEGATIVE):
                codes.add("DEBRIEF_VERDICT_CHANGED")
            if polarity and polarity <= {"penalty", "critical"} and any(has_term(sn, w) for w in POSITIVE):
                codes.add("DEBRIEF_VERDICT_CHANGED")
        logged = set(context.get("log_events", []))
        for ev, words in lexicon["events"].items():
            if ev not in logged and any(has_term(normal, w) for w in words):
                codes.add("DEBRIEF_NEW_EVENT")
    return Result(tuple(sorted(codes)))
