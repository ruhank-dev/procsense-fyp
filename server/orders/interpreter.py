"""The semantic interpreter of spoken orders (features B23, C32; p1-voice-input-s03; ADR-0028): one model reading of the WHOLE sentence in its context, instead of keyword guards deciding what a sentence is.

    it = OrderInterpreter(client).interpret("could I get a dose of morphine for her pain", vocabulary(), context="Recovery; drowsy after morphine; oxygen on")
    it.kind        # order | question | hypothetical | negated | ambiguous | incomplete | unsupported | other
    it.entry       # the catalogue item the model chose (only ever an item of the vocabulary), or None
    it.alternatives, it.clarification, it.confidence

Why: the earlier pipeline refused any sentence beginning "can I ..." with a word list and read an order back only when trigger words and a fuzzy match agreed. Polite requests ("can I get oxygen") were lost and the guard still could not tell
"should I hold the metformin" (a question) from "hold the metformin" (an order) by meaning. The same lesson as the speech evaluator (ADR-0025): give a strong model the full sentence, the scenario context and an explicit statement
of what each kind of sentence is, and let it judge meaning.

What the model does NOT decide. It never executes anything. Its answer is only a PROPOSAL that is checked against the vocabulary (an item that is not in it, a kind of item that does not fit the intent, or a model that is down all give
None or a refusal), turned into a read-back, and released only by the trainee's own yes (`ReadBack.confirm`, the signed single-use ticket); the engine still accepts or refuses the action by the scenario state, the required parameters and
the scoring rules. A sentence the model calls ambiguous or incomplete is a question to the trainee, never a guess. A dose is removed before anything is read back (grammar.clean). No keyword gate stands between the sentence and the model;
when the model is unavailable (outage, no consent, gate off) the caller falls back to the grammar with its question guard, which stays as the offline safety net.
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field

try:
    from llm import gateway
    from orders import llm_fallback as LF
    from orders.grammar import Entry
except ImportError:
    from server.llm import gateway
    from server.orders import llm_fallback as LF
    from server.orders.grammar import Entry

KINDS = ("order", "question", "hypothetical", "negated", "ambiguous", "incomplete", "unsupported", "other")
INTENTS = ("ORDER_TEST", "REQUEST_MEDICATION", "HOLD_MED", "REQUEST_TRANSPORT", "CALL_STAFF", "NONE")
INTENT_KINDS = {"ORDER_TEST": ("test",), "REQUEST_MEDICATION": ("medicine",), "HOLD_MED": ("medicine", "home_medicine"), "REQUEST_TRANSPORT": ("place",), "CALL_STAFF": ("staff",)}
MIN_CONFIDENCE = 0.5
MAX_CONTEXT = 1200


@dataclass
class Interpretation:
    kind: str                                   # one of KINDS
    entry: Entry | None = None                  # the item proposed (kind "order" only)
    intent: str = "NONE"
    alternatives: list[Entry] = field(default_factory=list)
    clarification: str = ""
    confidence: float = 0.0
    reason: str = ""                            # a short code for the log, never a sentence of the trainee
    model: str = ""


def catalogue(vocab: list[Entry]) -> tuple[str, dict[str, Entry]]:
    """The approved action catalogue the model may choose from, one line per item (id | name | what it is), and the ids it may answer with. Home medicines are listed as names, one id each."""
    lines, by_id = [], {}
    homes = []
    for e in vocab:
        key = f"{e.kind}:{e.target}"
        if key in by_id:
            continue
        by_id[key] = e
        if e.kind == "home_medicine":
            homes.append(e.target)
            continue
        lines.append(f"{key} | {e.label} | {LF.gloss(e)}")
    text = "\n".join(lines) + "\nHome medicines the patient may take, which the trainee can tell the team to hold or stop (answer home_medicine:<name>): " + ", ".join(sorted(homes))
    return text, by_id


def schema(ids: list[str]) -> dict:
    return {"type": "object", "required": ["kind", "intent", "target", "confidence"], "additionalProperties": False,
            "properties": {"kind": {"enum": list(KINDS)}, "intent": {"enum": list(INTENTS)}, "target": {"enum": ids + ["NONE"]},
                           "alternatives": {"type": ["array", "null"], "items": {"enum": ids}, "maxItems": 6}, "confidence": {"type": "number", "minimum": 0, "maximum": 1},
                           "clarification": {"type": ["string", "null"], "maxLength": 140}}}


def context_text(context) -> str:
    """The scenario context the headset (or a test) gives, as short plain text: a string, or a dict of the facts the engine holds (the patient, what is ordered or given, the previous turn, a read-back waiting for an answer)."""
    if not context:
        return "No scenario context was given."
    if isinstance(context, str):
        return context[:MAX_CONTEXT]
    return json.dumps(context, ensure_ascii=False)[:MAX_CONTEXT]


class OrderInterpreter:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout

    def interpret(self, transcript: str, vocab: list[Entry], context=None, dose_removed: bool = False) -> Interpretation | None:
        """None when the model could not be used (down, slow, an invalid answer): the caller then uses the grammar. Otherwise an Interpretation that has already been checked against the vocabulary."""
        if self.client is None or not transcript.strip():
            return None
        items, by_id = catalogue(vocab)
        ids = list(by_id)
        r = gateway.run(self.client, "orders.interpret", {"heard": transcript.strip(), "items": items, "context": context_text(context) + (" A dose was spoken; it is ignored." if dose_removed else "")},
                        schema=schema(ids), model=self.model, timeout=self.timeout, retries=0, task="orders_interpret")
        if not r.ok:
            return None
        return self.check(r.value, by_id, model=(r.attempts[-1].get("model", "") if r.attempts else ""))

    @staticmethod
    def check(v: dict, by_id: dict[str, Entry], model: str = "") -> Interpretation | None:
        """The execution-safety checks on a proposal; none of them reads the words of the sentence."""
        kind, intent, target = v.get("kind"), v.get("intent", "NONE"), v.get("target", "NONE")
        conf = float(v.get("confidence", 0.0))
        alts = [by_id[a] for a in (v.get("alternatives") or []) if a in by_id and a != target][:3]
        clar = str(v.get("clarification") or "").strip()[:140]
        if kind not in KINDS:
            return None
        if kind == "order":
            e = by_id.get(target)
            if e is None or intent not in INTENT_KINDS or e.kind not in INTENT_KINDS[intent]:
                return Interpretation("unsupported", reason="PROPOSAL_NOT_IN_CATALOGUE", confidence=conf, model=model)        # an item or a kind of action the simulator does not have is never executed
            if conf < MIN_CONFIDENCE:
                return Interpretation("ambiguous", alternatives=[e] + alts, clarification=clar, reason="LOW_CONFIDENCE", confidence=conf, model=model)
            if alts:                                                                                                          # the model itself named other readings: ask, do not choose
                return Interpretation("ambiguous", alternatives=[e] + alts, clarification=clar, reason="MODEL_NAMED_ALTERNATIVES", confidence=conf, model=model)
            return Interpretation("order", entry=e, intent=intent, confidence=conf, reason="PROPOSED", model=model)
        if kind in ("ambiguous", "incomplete"):
            return Interpretation(kind, alternatives=([by_id[target]] if target in by_id else []) + alts, clarification=clar, confidence=conf, reason=kind.upper(), model=model)
        return Interpretation(kind, confidence=conf, reason=kind.upper(), model=model)
