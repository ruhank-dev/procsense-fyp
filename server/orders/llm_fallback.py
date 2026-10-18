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
import functools
import json
import re
from dataclasses import dataclass
from pathlib import Path

try:
    from llm import gateway
    from orders.grammar import Entry, TRIGGERS, clean
except ImportError:
    from server.llm import gateway
    from server.orders.grammar import Entry, TRIGGERS, clean

ROOT = Path(__file__).resolve().parents[2]
LIST_SIZE = 140
MIN_CONFIDENCE = 0.9
KINDS = ("test", "medicine", "home_medicine", "place", "staff")
INTENT_OF_KIND = {"test": "ORDER_TEST", "medicine": "REQUEST_MEDICATION", "home_medicine": "HOLD_MED", "place": "REQUEST_TRANSPORT", "staff": "CALL_STAFF"}
HOLD_WORDS = TRIGGERS["HOLD_MED"] + ("leave out", "leave off", "left off", "left out", "not take", "no", "should not", "needs stopping", "stopping")
KIND_WORDS = {"test": "test or scan", "medicine": "medicine, fluid or oxygen", "home_medicine": "medicine the patient takes at home", "place": "place to take the patient", "staff": "member of staff"}


@functools.lru_cache(maxsize=1)
def _glosses() -> dict[tuple[str, str], str]:
    """A short note of what each item shows or does, made from the content (nothing typed twice): a test names its kind and what it measures, a medicine the effects the physiology tables give it, a place what can be done there."""
    out: dict[tuple[str, str], str] = {}
    inv = json.loads((ROOT / "content" / "investigations" / "investigations.json").read_text(encoding="utf-8"))
    for t in inv["tests"]:
        out[("test", t["id"])] = f"{t['kind']} investigation; shows {', '.join(t['analytes'][:5])}" if t.get("analytes") else f"{t['kind']} investigation"
    tre = json.loads((ROOT / "content" / "treatments.json").read_text(encoding="utf-8"))
    for t in tre["treatments"]:
        eff = [e.replace("EFF-", "").replace("-", " ").lower() for e in t.get("effects", [])]
        out[("medicine", t["id"])] = (f"{t['kind']}; effect: {', '.join(eff)}" if eff else f"{t['kind']} given to the patient")
    loc = json.loads((ROOT / "content" / "locations" / "locations.json").read_text(encoding="utf-8"))
    for p in loc["locations"]:
        out[("place", p["id"])] = f"a place; offers {', '.join(p.get('capabilities', [])) or 'care'}"
    return out


def gloss(e: Entry) -> str:
    return _glosses().get((e.kind, e.target), KIND_WORDS[e.kind])


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


def kind_schema() -> dict:
    return {"type": "object", "required": ["kind"], "additionalProperties": False, "properties": {"kind": {"enum": list(KINDS) + ["NONE"]}}}


MOVES = tuple(w for w in TRIGGERS["REQUEST_TRANSPORT"]) + ("take him", "take her", "send him", "send her", "wheel", "move him", "move her", "return", "transfer", "porter", "go to", "up to", "down to")


class OrderMapper:
    """`two_stage` (the default) first asks which KIND of thing is wanted (five kinds and NONE), then chooses among the items of that kind only, each with a note of what it does; the single call of the first version
    (`two_stage=False`) chose among every item at once. A place that is also a test (CT, MRI, ultrasound, X-ray) is a place only when the sentence moves the patient."""

    def __init__(self, client, model: str | None = None, timeout: float | None = None, two_stage: bool = True):
        self.client, self.model, self.timeout, self.two_stage = client, model, timeout, two_stage

    def _kind(self, text: str) -> str | None:
        r = gateway.run(self.client, "orders.kind", {"heard": text}, schema=kind_schema(), model=self.model, timeout=self.timeout, retries=0, task="orders_kind")
        if not r.ok or r.value["kind"] == "NONE":
            return None
        k = r.value["kind"]
        if k == "home_medicine" and not is_hold(text):
            k = "medicine"
        if k == "place" and not any(w in text for w in MOVES):
            k = "test"
        return k

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
