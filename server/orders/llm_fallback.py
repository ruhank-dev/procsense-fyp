"""Language-model fallback for the spoken-order grammar (features C32 and B23, plan capabilities C32.c1 and B23.c2): when the grammar finds no order in a transcript, a small model may suggest which item of the content was meant.

    mapper = OrderMapper(client)
    m = mapper.map("get a trace of his heart", vocabulary())      # Mapped(entry=<ecg>, intent="ORDER_TEST", confidence=0.9), or None: the menu is offered as before

The model only ever chooses among a closed list built from the content (every test, medicine, place and member of staff, the closest to the cleaned transcript first, about 55 lines), through a schema whose enum is that list plus NONE, so it cannot name an
item that does not exist. It never sees a dose (doses are removed by the grammar before) and never decides anything: its suggestion becomes an ordinary read-back ("Confirm: order ECG?") and nothing is released
until the trainee says yes, exactly as for a grammar hit. A model that is down, slow, unsure (confidence under MIN_CONFIDENCE) or answering NONE gives None, and the grammar's own answer stands (clarify or menu).
The intent comes from the kind of item (a test is ordered, a medicine requested, a place is a transport, a person is called); a medicine named with a hold word ("leave out", "stop", "skip") is a hold.
"""
from __future__ import annotations

import difflib
import re
from dataclasses import dataclass

try:
    from llm import gateway
    from orders.grammar import Entry, TRIGGERS, clean
except ImportError:
    from server.llm import gateway
    from server.orders.grammar import Entry, TRIGGERS, clean

LIST_SIZE = 140
MIN_CONFIDENCE = 0.6
KINDS = ("test", "medicine", "home_medicine", "place", "staff")
INTENT_OF_KIND = {"test": "ORDER_TEST", "medicine": "REQUEST_MEDICATION", "home_medicine": "HOLD_MED", "place": "REQUEST_TRANSPORT", "staff": "CALL_STAFF"}
HOLD_WORDS = TRIGGERS["HOLD_MED"] + ("leave out", "leave off", "left off", "left out", "not take", "no", "should not", "needs stopping", "stopping")
KIND_WORDS = {"test": "test or scan", "medicine": "medicine, fluid or oxygen", "home_medicine": "medicine the patient takes at home", "place": "place to take the patient", "staff": "member of staff"}


@dataclass
class Mapped:
    entry: Entry
    intent: str
    confidence: float
    shortlist: list[str]


def is_hold(text: str) -> bool:
    return any(re.search(rf"(^| ){re.escape(w)}( |$)", text) for w in HOLD_WORDS)


def shortlist(text: str, vocab: list[Entry], size: int = LIST_SIZE) -> list[Entry]:
    """Every item the trainee could mean (tests, medicines, places, staff; the medicines the patient takes at home only when the sentence holds or stops something), one per (kind, target), the closest to the words of
    the text first. The whole list is offered, not only the lexical matches, because a described order ("a trace of his heart") shares no word with its item ("ECG"); the list is about 55 lines, which the live model reads in well under a second."""
    kinds = KINDS if is_hold(text) else tuple(k for k in KINDS if k != "home_medicine")
    words = text.split()
    grams = {" ".join(words[i:i + n]) for n in range(1, 5) for i in range(len(words) - n + 1)} | {text}
    best: dict[tuple[str, str], tuple[float, Entry]] = {}
    for e in vocab:
        if e.kind not in kinds:
            continue
        score = max((difflib.SequenceMatcher(None, g, e.phrase).ratio() for g in grams), default=0.0)
        if any(w in e.phrase.split() for w in words if len(w) > 3):
            score = max(score, 0.6)
        key = (e.kind, e.target)
        if key not in best or score > best[key][0]:
            best[key] = (score, e)
    ranked = sorted(best.values(), key=lambda x: (-x[0], x[1].kind, x[1].target))
    return [e for _, e in ranked[:size]]


def schema(ids: list[str]) -> dict:
    return {"type": "object", "required": ["target", "confidence"], "additionalProperties": False,
            "properties": {"target": {"enum": ids + ["NONE"]}, "confidence": {"type": "number", "minimum": 0, "maximum": 1}}}


class OrderMapper:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout

    def map(self, transcript: str, vocab: list[Entry]) -> Mapped | None:
        if self.client is None:
            return None
        text, _ = clean(transcript)
        cands = shortlist(text, vocab)
        if not text or not cands:
            return None
        ids = [f"{e.kind}:{e.target}" for e in cands]
        items = "\n".join(f"{i} | {e.label} | {KIND_WORDS[e.kind]}" for i, e in zip(ids, cands))
        r = gateway.run(self.client, "orders.map", {"heard": text, "items": items}, schema=schema(ids), model=self.model, timeout=self.timeout, retries=0, task="orders_map")
        if not r.ok:
            return None
        pick, conf = r.value["target"], float(r.value["confidence"])
        if pick == "NONE" or pick not in ids or conf < MIN_CONFIDENCE:
            return None
        e = cands[ids.index(pick)]
        intent = INTENT_OF_KIND[e.kind]
        if e.kind == "medicine" and is_hold(text):
            intent = "HOLD_MED"
        return Mapped(e, intent, conf, ids)
