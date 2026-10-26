"""The spoken-order mappers on the SAME sentences: `uv run --project tools tools/ai/compare_order_mappers.py [--sets paraphrases,v2,v3,v4] [--write]`.

  grammar   the order grammar alone (server/orders/grammar.py), no model
  legacy    the fourth-session mapper reproduced from commit 4e97297: ONE model call over every item, prompt orders.map@2, confidence floor 0.9, no kind stage, no content notes, no re-ranking, no agreement rule
  current   server/orders/llm_fallback.py OrderMapper: kind stage, content notes, deterministic re-ranking, agreement of two readings, a question when they disagree

Every mapper gets the current grammar and the current synonym table, so the only difference between `legacy` and `current` is the mapper. Sets: tests/orders/paraphrases.json (the development set: its failures were analysed), paraphrases_v2.json and paraphrases_v3.json (each scored once as a frozen set in an earlier session, development sets
since), and paraphrases_v4.json when it exists (frozen; pass --sets v4 once). Reported per set and per kind: correct, wrong, menu, clarify, non-orders offered, latency. Scores from different sets are never compared with each other; legacy and current are compared only on the same set. Synthetic, one author family: no independent evidence.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import eval_orders_llm as E  # noqa: E402
from server.llm import client as C, gateway  # noqa: E402
from server.orders import llm_fallback as LF  # noqa: E402
from server.orders.grammar import ReadBack, clean, vocabulary  # noqa: E402

FILES = {"paraphrases": "paraphrases.json", "v2": "paraphrases_v2.json", "v3": "paraphrases_v3.json", "v4": "paraphrases_v4.json", "v5": "paraphrases_v5.json", "v6": "paraphrases_v6.json", "v7": "paraphrases_v7.json", "v8": "paraphrases_v8.json", "v9": "paraphrases_v9.json", "v10": "paraphrases_v10.json"}
FROZEN = ("v4", "v5", "v8", "v9", "v10")                  # v4 was scored once (ADR-0021); v5 is the second author's set (EX-18), scored once when it exists
LOCK_DIR = ROOT / "artifacts" / "ai-bench"
OUT = ROOT / "reports" / "ai" / "orders-mapper-compare.json"


class Legacy(LF.OrderMapper):
    """The mapper of commit 4e97297 (fourth session), unchanged in behaviour."""

    def map(self, transcript, vocab):
        if self.client is None:
            return None
        text, _ = clean(transcript)
        cands = LF.shortlist(text, vocab)
        if not text or not cands:
            return None
        ids = [f"{e.kind}:{e.target}" for e in cands]
        items = "\n".join(f"{i} | {e.label} | {LF.KIND_WORDS[e.kind]}" for i, e in zip(ids, cands))
        r = gateway.run(self.client, "orders.map", {"heard": text, "items": items}, schema=LF.schema(ids), model=self.model, timeout=self.timeout, retries=0, task="orders_map")
        if not r.ok:
            return None
        pick, conf = r.value["target"], float(r.value["confidence"])
        if pick == "NONE" or pick not in ids or conf < 0.9:
            return None
        e = cands[ids.index(pick)]
        intent = LF.INTENT_OF_KIND[e.kind]
        if e.kind == "medicine" and LF.is_hold(text):
            intent = "HOLD_MED"
        return LF.Mapped(e, intent, conf, ids)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sets", default="paraphrases,v2,v3")
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--second-look", action="store_true", help="score an already-scored frozen set again with other mappers; the result file says so and the set is no longer untouched")
    ap.add_argument("--tag", default="", help="suffix of the result file, for a run that is not the standard comparison")
    ap.add_argument("--mappers", default="grammar,legacy,current,lean,compact,short", help="which of grammar, legacy, current, lean, compact, short to run")
    a = ap.parse_args()
    names = a.sets.split(",")
    for f in FROZEN:
        if f in names and not a.second_look and ((LOCK_DIR / f"orders-{f}-compare-used.json").exists() or (OUT.with_name(f"orders-mapper-compare-{f}.json")).exists()):
            raise SystemExit(f"REFUSED: the frozen set {f} was already scored")
    client = C.OllamaClient("http://127.0.0.1:11434")
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    vocab = vocabulary()
    mappers = {"grammar": None, "legacy": Legacy(client, model=a.model, timeout=60.0), "current": LF.OrderMapper(client, model=a.model, timeout=60.0), "lean": LF.OrderMapper(client, model=a.model, timeout=60.0, lean=True), "compact": LF.OrderMapper(client, model=a.model, timeout=60.0, lean=2), "short": LF.OrderMapper(client, model=a.model, timeout=60.0, lean=3)}
    ReadBack(vocab, mappers["current"]).hear("warm up the model with one order please")
    out = {"model": a.model, "synthetic": True, "independent": False, "sets": {}}
    for n in names:
        rows = E.load_rows(FILES[n])
        out["sets"][n] = {"file": FILES[n], "rows": len(rows), "frozen_scored_now": n in FROZEN and not a.second_look, "second_look": bool(a.second_look and n in FROZEN)}
        for mname, m in mappers.items():
            if mname not in a.mappers.split(","):
                continue
            res = E.score(rows, (lambda: ReadBack(vocab)) if m is None else (lambda m=m: ReadBack(vocab, m)))
            out["sets"][n][mname] = res
            print(f"{n:12s} {mname:8s} correct {res['correct']}  wrong {res['wrong']}  menu {res['menu']}  clarify {res['clarify_questions']}  false offer {res['false_offer']}  p50 {res['latency_ms_p50']} ms", flush=True)
    if a.write:
        frozen = [f for f in FROZEN if f in names]
        dest = OUT.with_name(f"orders-mapper-compare-{frozen[0]}{a.tag}.json") if frozen else OUT.with_name(f"orders-mapper-compare{a.tag}.json")
        dest.write_text(json.dumps(out, indent=1) + chr(10), encoding="utf-8", newline=chr(10))
        for f in frozen:
            LOCK_DIR.mkdir(parents=True, exist_ok=True)
            (LOCK_DIR / f"orders-{f}-compare-used.json").write_text(json.dumps({"scored": True}) + chr(10), encoding="utf-8")
        print("wrote", dest.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
