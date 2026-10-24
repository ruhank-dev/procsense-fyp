"""Spoken orders: from a transcript to a confirmed action (p1-voice-input-s03, EXP-VOX-4; plan tests T-VO1 to T-VO4).

    rb = ReadBack(vocabulary())
    r = rb.hear("could I get a full blood count please")          # status "confirm", read_back "Confirm: order a full blood count?"
    action = rb.confirm("yes")                                     # a Core action {"verb": "order_investigation", "params": {"test": "fbc"}}, only now

Recognition makes mistakes, so nothing reaches the engine on a first hearing. The transcript is cleaned (doses and units are removed: a spoken dose is never carried into an order, the trainee chooses
doses elsewhere), an intent is found from trigger words (ORDER_TEST, REQUEST_MEDICATION, HOLD_MED, REQUEST_TRANSPORT, CALL_STAFF), and the thing named is matched against the content by fuzzy comparison
(difflib ratio of at least FUZZY, 0.85, over every run of 1 to 5 words; brand names and plain words come from synonyms.csv). Two different things equally close is an ambiguity and a clarifying question is asked
(T-VO2); nothing close enough offers the menu (T-VO4). The read-back is spoken by the staff voice; "yes" or "confirm" releases the action, "no" cancels and offers the menu. Every hearing and its outcome is
logged (intent, slots, confirmed or not, latency) and a test checks that no action was released without a confirmation. The grammar itself never calls a language model. The plan's model fallback for a transcript with no order in it is
server/orders/llm_fallback.py (an optional `mapper`): it only suggests an item of the content, the read-back and the yes are unchanged (DT-208).
"""
from __future__ import annotations

import csv
import difflib
import json
import re
import time
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FUZZY = 0.85
TIE = 0.04
UNITS = r"(?:mg|mcg|micrograms?|g|grams?|ml|millilitres?|litres?|l|units?|iu|mmol|%|per cent|percent|puffs?|tablets?|drops?|mls)"
DOSE = re.compile(rf"\b\d+(?:\.\d+)?\s*{UNITS}\b(?:\s*(?:per|a|/)\s*(?:kg|hour|day|minute))?", re.IGNORECASE)
NUMBER = re.compile(r"\b\d+(?:\.\d+)?\b")
YES = {"yes", "yeah", "yep", "confirm", "confirmed", "correct", "that is right", "go ahead", "please do"}
NO = {"no", "nope", "cancel", "stop", "wrong", "not that"}

TRIGGERS = {
    "HOLD_MED": ("hold", "withhold", "stop the", "omit", "do not give", "dont give", "don't give", "skip"),
    "REQUEST_TRANSPORT": ("transport", "porter", "transfer", "take the patient", "move the patient", "send the patient", "take her to", "take him to", "send him", "send her", "take her along", "take him along", "bring him to", "bring her to", "wheel", "move him", "move her", "take them to", "send them"),
    "CALL_STAFF": ("call", "bleep", "phone", "page", "get the", "ask the", "ring", "i need the", "escalate to"),
    "REQUEST_MEDICATION": ("give", "start", "administer", "request", "prescribe", "i would like to give", "can i have", "set up"),
    "ORDER_TEST": ("order", "request", "send", "take", "get me", "can i have", "could i get", "could i have", "i would like", "check", "run"),
}
KIND_OF = {"ORDER_TEST": ("test",), "REQUEST_MEDICATION": ("medicine",), "HOLD_MED": ("medicine", "home_medicine"), "REQUEST_TRANSPORT": ("place",), "CALL_STAFF": ("staff",)}
MENU = {"ORDER_TEST": "Use the investigations menu.", "REQUEST_MEDICATION": "Use the medicines menu.", "HOLD_MED": "Use the medicines menu to hold a medicine.", "REQUEST_TRANSPORT": "Use the transport menu.", "CALL_STAFF": "Use the call menu."}


@dataclass
class Entry:
    phrase: str
    kind: str
    target: str
    label: str


def vocabulary() -> list[Entry]:
    """Everything that can be named, from the content and the synonym table (nothing typed twice: labels come from content, brand names and plain words from synonyms.csv)."""
    out: list[Entry] = []
    inv = json.loads((ROOT / "content" / "investigations" / "investigations.json").read_text(encoding="utf-8"))
    spoken = lambda label: re.sub(r"\s*\(.*?\)", "", label).strip()           # noqa: E731  the part of a label before its explanation in brackets is what people say
    for t in inv["tests"]:
        out.append(Entry(spoken(t["label"]).lower(), "test", t["id"], spoken(t["label"])))
    tre = json.loads((ROOT / "content" / "treatments.json").read_text(encoding="utf-8"))
    labels = {t["id"]: spoken(t["label"]) for t in tre["treatments"]}
    test_label = {t["id"]: spoken(t["label"]) for t in inv["tests"]}
    for t in tre["treatments"]:
        if t["kind"] in ("medicine", "fluid", "oxygen"):
            out.append(Entry(spoken(t["label"]).lower(), "medicine", t["id"], spoken(t["label"])))
    loc = json.loads((ROOT / "content" / "locations" / "locations.json").read_text(encoding="utf-8"))
    for p in loc["locations"]:
        out.append(Entry(p["label"].lower(), "place", p["id"], p["label"]))
    with (ROOT / "server" / "orders" / "synonyms.csv").open(encoding="utf-8", newline="") as f:
        for r in csv.DictReader(f):
            label = labels.get(r["target"]) or test_label.get(r["target"]) or r["phrase"]
            out.append(Entry(r["phrase"].lower(), r["kind"], r["target"], label))
    lex = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))
    known = {e.phrase for e in out}
    for drug in lex["categories"]["drug"]:                           # a home medicine the trainee may hold (metformin): it is not an order, so it has no treatment id
        if drug.lower() not in known:
            out.append(Entry(drug.lower(), "home_medicine", drug.lower(), drug))
    return out


def clean(text: str) -> tuple[str, bool]:
    """Lower case, doses and units removed, punctuation gone. Returns the text and whether a dose was removed."""
    t = text.lower().replace("’", "'")
    stripped = DOSE.sub(" ", t)
    removed = stripped != t
    t = NUMBER.sub(lambda m: " " if removed else m.group(0), stripped)
    return re.sub(r"\s+", " ", re.sub(r"[^a-z0-9' ]+", " ", t)).strip(), removed


# A spoken order is never read back when the words say it is not an order: a question, a hypothetical, a condition or a negated order verb ("should I hold the metformin", "if he is diabetic
# hold it", "do not hold the metformin", "don't order a CRP"). "do not give" and "don't give" are themselves hold orders, so they are taken out first; a negation that is not in front of an
# order word ("she must not have the metformin") is left to the grammar and the model. DT-208 safety review, 24 Oct 2026.
QUESTION_OR_CONDITION = re.compile(r"(?:^| )(?:should (?:i|we)|shall (?:i|we)|what if|even if|suppose|supposing|do you think|is it (?:ok|okay|safe) to|can i|may i|hypothetically|maybe|perhaps)(?: |$)|^if ")
NEGATION = r"(?:not|never|dont|don't|do not|no need to|no need|without|instead of|rather than)"
HOLD_BY_NEGATION = ("do not give", "dont give", "don't give")
HOLD_BY_MODAL = re.compile(r" (?:should not|must not|shouldn't|mustn't|cannot|can't|is not to|are not to) (?:take|have|get|be given|receive) ")   # "she should not take her warfarin" is a hold, not a refusal
NOT_AN_ORDER_REPLY = "That sounded like a question, a condition or a negative, so I did not take it as an order. Say the order plainly or use the menu."


def _negated_order_word() -> re.Pattern:
    words = sorted({w for ws in TRIGGERS.values() for w in ws}, key=len, reverse=True)
    return re.compile(r"(?:^| )" + NEGATION + r"(?: \w+){0,2} (?:" + "|".join(re.escape(w) for w in words) + r")(?: |$)")


NEGATED_ORDER = _negated_order_word()


def not_an_order(text: str) -> bool:
    """True when the cleaned transcript is phrased as a question, a hypothetical, a condition or a negated order, so nothing may be read back or released from it."""
    t = f" {text} "
    for w in HOLD_BY_NEGATION:
        t = t.replace(f" {w} ", " hold ")
    t = HOLD_BY_MODAL.sub(" hold ", t).strip()
    return QUESTION_OR_CONDITION.search(t) is not None or NEGATED_ORDER.search(t) is not None


def intent_of(text: str, has_entity: dict[str, bool]) -> str | None:
    """The first intent whose trigger is in the text and whose kind of entity was named; HOLD_MED and CALL_STAFF and REQUEST_TRANSPORT are checked first (their trigger words are more specific)."""
    padded = f" {text} "
    for intent, words in TRIGGERS.items():
        if any(f" {w} " in padded or padded.startswith(f" {w} ") for w in words) and any(has_entity.get(k) for k in KIND_OF[intent]):
            return intent
    return None


def matches(text: str, vocab: list[Entry], kinds: tuple[str, ...]) -> list[tuple[float, Entry]]:
    words = text.split()
    best: dict[str, tuple[float, Entry]] = {}
    for n in range(1, 6):
        for i in range(len(words) - n + 1):
            gram = " ".join(words[i:i + n])
            for e in vocab:
                if e.kind not in kinds:
                    continue
                sm = difflib.SequenceMatcher(None, gram, e.phrase)
                if sm.real_quick_ratio() < FUZZY or sm.quick_ratio() < FUZZY:
                    continue                                     # cheap upper bounds of ratio(): the same result, without the full comparison
                score = sm.ratio()
                if score >= FUZZY and (e.target not in best or score > best[e.target][0]):
                    best[e.target] = (score, e)
    return sorted(best.values(), key=lambda x: -x[0])


@dataclass
class Hearing:
    status: str                                  # confirm | clarify | menu | removed_dose_confirm
    intent: str | None = None
    slots: dict = field(default_factory=dict)
    read_back: str = ""
    options: list[str] = field(default_factory=list)
    dose_removed: bool = False
    action: dict | None = None
    source: str = "grammar"                      # grammar | model (the language-model fallback suggested it; the read-back and the yes are the same)


def action_for(intent: str, e: Entry) -> dict | None:
    if intent == "ORDER_TEST":
        return {"verb": "order_investigation", "params": {"test": e.target}}
    if intent == "REQUEST_MEDICATION":
        return {"verb": "request_medication", "params": {"class": e.target}}
    if intent == "REQUEST_TRANSPORT":
        return {"verb": "request_transport", "params": {"to": e.target}}
    if intent == "CALL_STAFF":
        return {"verb": "request_anaesthetic_review", "params": {}} if e.target in ("anaesthetist",) else {"verb": "escalate", "params": {"to": e.target}}
    if intent == "HOLD_MED" and e.kind == "home_medicine":
        return {"verb": "hold_medicine", "params": {"drug": e.target}}   # DT-208: the engine refuses a drug the patient has not named
    return None                                  # holding a treatment the trainee ordered (a medicine kind) has no engine verb; it is read back and logged only


READBACK = {"ORDER_TEST": "order {label}", "REQUEST_MEDICATION": "request {label}", "HOLD_MED": "hold {label}", "REQUEST_TRANSPORT": "transport the patient to {label}", "CALL_STAFF": "call {label}"}


class ReadBack:
    def __init__(self, vocab: list[Entry] | None = None, mapper=None):
        self.vocab = vocab or vocabulary()
        self.mapper = mapper                     # server/orders/llm_fallback.OrderMapper or None: used only when the grammar found no order
        self.pending: Hearing | None = None
        self.log: list[dict] = []
        self.released: list[dict] = []

    def hear(self, transcript: str) -> Hearing:
        t0 = time.monotonic()
        text, removed = clean(transcript)
        if not_an_order(text):
            h = Hearing("menu", read_back=NOT_AN_ORDER_REPLY, options=sorted(MENU.values()), dose_removed=removed)
            self.pending = None
            self._log(transcript, h, t0)
            return h
        found = {k: matches(text, self.vocab, (k,)) for k in ("test", "medicine", "home_medicine", "place", "staff")}
        intent = intent_of(text, {k: bool(v) for k, v in found.items()})
        if intent is None:
            m = self.mapper.map(transcript, self.vocab) if self.mapper is not None else None
            if m is not None and m.alternatives:                 # two readings disagreed: ask which one, release nothing
                names = [x.label for x in m.alternatives]
                h = Hearing("clarify", m.intent, {}, "Which one: " + " or ".join(names) + "?", names, removed, source="model")
                self.pending = None
                self._log(transcript, h, t0)
                return h
            if m is not None:                                    # a suggestion of the model: read back like any other, released only by a yes
                h = Hearing("removed_dose_confirm" if removed else "confirm", m.intent, {"target": m.entry.target, "label": m.entry.label}, "Confirm: " + READBACK[m.intent].format(label=m.entry.label.lower()) + "?",
                            dose_removed=removed, action=action_for(m.intent, m.entry), source="model")
                if removed:
                    h.read_back += " (I left out the dose; choose it on the form.)"
                self.pending = h
                self._log(transcript, h, t0)
                return h
            h = Hearing("menu", read_back="I did not catch an order. Please use the menu.", options=sorted(MENU.values()), dose_removed=removed)
            self._log(transcript, h, t0)
            return h
        cand = [m for k in KIND_OF[intent] for m in found[k]]
        cand.sort(key=lambda x: -x[0])
        close = [m for m in cand if cand[0][0] - m[0] <= TIE and m[1].target != cand[0][1].target]
        if close:
            names = [cand[0][1].label] + [m[1].label for m in close]
            h = Hearing("clarify", intent, {}, "Which one: " + " or ".join(names) + "?", names, removed)
            self.pending = None
            self._log(transcript, h, t0)
            return h
        e = cand[0][1]
        h = Hearing("removed_dose_confirm" if removed else "confirm", intent, {"target": e.target, "label": e.label}, "Confirm: " + READBACK[intent].format(label=e.label.lower()) + "?", dose_removed=removed,
                    action=action_for(intent, e))
        if removed:
            h.read_back = "Confirm: " + READBACK[intent].format(label=e.label.lower()) + "? (I left out the dose; choose it on the form.)"
        self.pending = h
        self._log(transcript, h, t0)
        return h

    def confirm(self, spoken: str) -> dict | None:
        """The action to release, only if a hearing is waiting and the answer is a yes; a no cancels it."""
        answer = re.sub(r"[^a-z' ]+", " ", spoken.lower()).strip()
        h = self.pending
        if h is None:
            return None
        if answer in YES or any(answer.startswith(y + " ") for y in YES):
            self.pending = None
            self.log.append({"event": "confirmed", "intent": h.intent, "slots": h.slots, "by": "voice"})
            if h.action:
                self.released.append(h.action)
            return h.action
        if answer in NO or answer.split(" ")[0] in NO:
            self.pending = None
            self.log.append({"event": "cancelled", "intent": h.intent, "slots": h.slots})
            return None
        return None                                                      # anything else leaves the question open

    def _log(self, transcript: str, h: Hearing, t0: float) -> None:
        self.log.append({"event": "heard", "status": h.status, "intent": h.intent, "slots": h.slots, "dose_removed": h.dose_removed, "latency_ms": round((time.monotonic() - t0) * 1000), "words": len(transcript.split()), "source": h.source})
