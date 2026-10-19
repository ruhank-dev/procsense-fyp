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
import math
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
DETAIL_TOP = 8                      # the compact variant gives a note to this many items
RERANK_K = LIST_SIZE               # every item of the chosen kind stays on offer (a described order shares no word with its item); the re-ranking only orders them
MIN_CONFIDENCE = 0.5                 # a floor only: the stated confidence does not separate right from wrong (reports/ai/orders-eval-llm-v2.json), agreement of two readings decides
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


def gloss(e: Entry, lean: bool = False) -> str:
    g = _glosses().get((e.kind, e.target), KIND_WORDS[e.kind])
    if lean and e.kind == "test" and "; shows " in g:                      # fewer words per line: the first three analytes are enough to tell a test from its neighbour, and every token costs about 3 ms of prompt evaluation on the desktop card
        head, tail = g.split("; shows ", 1)
        g = head.replace(" investigation", "") + "; " + ", ".join(tail.split(", ")[:3])
    return g


@dataclass
class Mapped:
    entry: Entry
    intent: str
    confidence: float
    shortlist: list[str]
    alternatives: tuple = ()           # set when two readings disagree: both items are offered to the trainee to choose between, nothing is read back as an order


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



_STOP = {"the", "a", "an", "of", "to", "for", "and", "in", "on", "is", "are", "her", "his", "him", "she", "he", "please", "some", "any", "with", "we", "i", "it", "that", "this", "my", "at", "be", "do", "you", "can", "could", "would", "like", "need", "want", "get", "give", "check", "test"}


def _stem(w: str) -> str:
    for suf in ("ing", "ed", "es", "s"):
        if len(w) > 4 and w.endswith(suf):
            return w[: -len(suf)]
    return w


def rerank(text: str, cands: list[Entry], vocab: list[Entry]) -> list[Entry]:
    """The candidates, best first, by the stemmed words of the sentence that also occur in the item's names and synonyms (every phrase the vocabulary holds for it), its label and its note, weighted by how rare the word is among the
    candidates (an idf), so that a word shared by every blood test counts for little and 'inflammation' counts for much. No model; ties keep the earlier (fuzzy-match) order."""
    docs: dict[tuple[str, str], set[str]] = {}
    for e in vocab:
        docs.setdefault((e.kind, e.target), set()).update(_stem(w) for w in re.findall(r"[a-z]+", f"{e.phrase} {e.label}".lower()) if w not in _STOP)
    for e in cands:
        docs.setdefault((e.kind, e.target), set()).update(_stem(w) for w in re.findall(r"[a-z]+", f"{e.label} {gloss(e)}".lower()) if w not in _STOP)
        docs[(e.kind, e.target)].update(_stem(w) for w in re.findall(r"[a-z]+", gloss(e).lower()) if w not in _STOP)
    n = len(cands)
    df: dict[str, int] = {}
    for e in cands:
        for w in docs[(e.kind, e.target)]:
            df[w] = df.get(w, 0) + 1
    words = {_stem(w) for w in re.findall(r"[a-z]+", text.lower()) if w not in _STOP}
    scored = []
    for k, e in enumerate(cands):
        d = docs[(e.kind, e.target)]
        score = sum(math.log(1 + n / df[w]) for w in words if w in d)
        scored.append((-score, k, e))
    scored.sort(key=lambda x: (x[0], x[1]))
    return [e for _, _, e in scored]


def schema(ids: list[str]) -> dict:
    return {"type": "object", "required": ["target", "confidence"], "additionalProperties": False,
            "properties": {"target": {"enum": ids + ["NONE"]}, "confidence": {"type": "number", "minimum": 0, "maximum": 1}}}


def kind_schema() -> dict:
    return {"type": "object", "required": ["kind"], "additionalProperties": False, "properties": {"kind": {"enum": list(KINDS) + ["NONE"]}}}


MOVES = tuple(w for w in TRIGGERS["REQUEST_TRANSPORT"]) + ("take him", "take her", "send him", "send her", "wheel", "move him", "move her", "return", "transfer", "porter", "go to", "up to", "down to", "over to", "back to", "get her to", "get him to", "bring him", "bring her", "along to", "to the")


class OrderMapper:
    """`two_stage` (the default) first asks which KIND of thing is wanted (five kinds and NONE), then chooses among the items of that kind only, each with a note of what it does; the single call of the first version
    (`two_stage=False`) chose among every item at once. A place that is also a test (CT, MRI, ultrasound, X-ray) is a place only when the sentence moves the patient."""

    def __init__(self, client, model: str | None = None, timeout: float | None = None, two_stage: bool = True, lean: bool | int = False):
        """`lean` (ADR-0021, latency): the shorter kind prompt (orders.kind@2), the list-first map prompt (orders.map@3), shorter notes, and the items listed in a fixed order (the re-ranking still decides what the second reading must agree with) so the host can reuse the evaluated list between hearings."""
        self.client, self.model, self.timeout, self.two_stage, self.lean = client, model, timeout, two_stage, bool(lean)
        self.short = lean == 3                       # lean=3: compact and the shortest prompts (orders.kind@3, orders.map@4)
        self.compact = lean in (2, 3)                      # lean=2: only the DETAIL_TOP items the re-ranker likes best carry a note; the rest are listed by name alone (about half the prompt tokens)

    def _kind(self, text: str) -> str | None:
        r = gateway.run(self.client, "orders.kind", {"heard": text}, schema=kind_schema(), model=self.model, timeout=self.timeout, retries=0, task="orders_kind", version=(3 if self.short else 2) if self.lean else None)
        if not r.ok or r.value["kind"] == "NONE":
            return None
        k = r.value["kind"]
        if k == "home_medicine" and not is_hold(text):
            k = "medicine"
        if k == "place" and not any(w in text for w in MOVES):
            k = "test"
        return k

    def _ask(self, text: str, cands: list[Entry], detailed: set[str] | None = None) -> tuple[str, float] | None:
        ids = [f"{e.kind}:{e.target}" for e in cands]
        items = chr(10).join(f"{i} | {e.label} | {gloss(e, self.lean)}" if detailed is None or i in detailed else f"{i} | {e.label}" for i, e in zip(ids, cands))
        r = gateway.run(self.client, "orders.map", {"heard": text, "items": items}, schema=schema(ids), model=self.model, timeout=self.timeout, retries=0, task="orders_map", version=(4 if self.short else 3) if self.lean else 2)
        if not r.ok:
            return None
        pick = r.value["target"]
        return (pick, float(r.value["confidence"])) if pick in ids else ("NONE", 0.0)

    def map(self, transcript: str, vocab: list[Entry]) -> Mapped | None:
        """grammar-independent mapping in four steps: (1) the kind of thing wanted (model, closed answer); (2) a deterministic RE-RANKING of the items of that kind by the words of the sentence against each item's names, synonyms and note, keeping the
        best `RERANK_K`; (3) the model chooses among those, shown with their notes; (4) the choice is accepted only if it AGREES: either it is the re-ranker's first item, or a second call with the items in reverse order picks the same
        item. The stated confidence is not used to decide (it does not separate right from wrong, reports/ai/orders-eval-llm-v2.json); agreement is the calibration. Anything else gives None and the menu."""
        if self.client is None:
            return None
        text, _ = clean(transcript)
        pool = shortlist(text, vocab)
        if not text or not pool:
            return None
        kind = self._kind(text) if self.two_stage else None
        if self.two_stage and kind is None:                           # the kind stage is also the guard against turning small talk into an order: tried without it (fifth session, second pass) the share of non-orders offered rose from 0 to 20 percent on a development set for 2 points of recall
            return None
        ranked = rerank(text, [e for e in pool if kind is None or e.kind == kind] or pool, vocab)[:RERANK_K]
        shown = sorted(ranked, key=lambda e: (e.kind, e.target)) if self.lean else ranked
        detailed = {f"{e.kind}:{e.target}" for e in ranked[:DETAIL_TOP if not self.short else 6]} if self.compact else None
        first = self._ask(text, shown, detailed)
        if first is None or first[0] == "NONE" or first[1] < MIN_CONFIDENCE:
            return None
        ids = [f"{e.kind}:{e.target}" for e in ranked]
        pick = first[0]
        if pick != ids[0]:                                           # the re-ranker disagrees with the model: ask again with the order reversed and accept only the same item twice
            second = self._ask(text, list(reversed(shown)), detailed)
            if second is None or second[0] == "NONE" or second[0] not in ids:
                return None
            if second[0] != pick:                                      # two readings, two items: the trainee chooses (a question, not an order)
                a, b = ranked[ids.index(pick)], ranked[ids.index(second[0])]
                if a.kind != b.kind:
                    return None
                return Mapped(a, INTENT_OF_KIND[a.kind], 0.0, ids, alternatives=(a, b))
        e = ranked[ids.index(pick)]
        if e.kind == "test" and any(w in text for w in MOVES):                      # 'take her to the CT scanner' moves the patient to the place; it does not order the scan
            place = next((v for v in vocab if v.kind == "place" and v.target == e.target), None)
            if place is not None:
                e = place
        intent = INTENT_OF_KIND[e.kind]
        if e.kind == "medicine" and is_hold(text):
            intent = "HOLD_MED"
        return Mapped(e, intent, first[1], ids)
