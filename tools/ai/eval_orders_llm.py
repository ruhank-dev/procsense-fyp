"""Measures the language-model fallback of the spoken-order grammar (features C32 and B23; capabilities C32.c1 and B23.c2): `uv run --project tools tools/ai/eval_orders_llm.py [--model qwen3.5:4b-q4_K_M] [--write]`.

The same paraphrased orders (tests/orders/paraphrases.json: 61 natural wordings of orders with no trigger word and mostly without the name of the thing, and 10 sentences that are not orders; synthetic, one author, hash
checked) are heard twice by the read-back: by the grammar alone and by the grammar with the model fallback (server/orders/llm_fallback.py). Reported for each: the share whose FIRST read-back names the right item and
intent (`correct`), the share read back as a wrong item (`wrong`; the trainee would say no), the share that ended in the menu (`menu`), and for the non-orders the share read back as an order (`false_offer`). The
rule of the plan: accuracy at least 90 percent and at least 10 points above the grammar alone (B23.c2), at least 95 percent (C32.c1, with the read-back), and `released_without_confirmation` = 0 (no action leaves
the read-back before a yes: checked on every hearing). The order grammar's own synthetic phrases (eval_orders.py) must not get worse with the fallback on; they are re-run here as `regression`.
Needs a running Ollama with the model. `--write` saves reports/ai/orders-eval-llm.json.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "ai"))

SET = ROOT / "tests" / "orders"


def load_rows() -> list[dict]:
    manifest = json.loads((SET / "manifest.json").read_text(encoding="utf-8"))
    if hashlib.sha256((SET / "paraphrases.json").read_bytes()).hexdigest() != manifest["files"]["paraphrases.json"]:
        raise SystemExit("REFUSED: tests/orders/paraphrases.json does not match its frozen hash")
    return json.loads((SET / "paraphrases.json").read_text(encoding="utf-8"))["rows"]


def score(rows: list[dict], make) -> dict:
    n_order = right = wrong = menu = n_none = false_offer = released = 0
    ms: list[float] = []
    by_kind: dict[str, list[int]] = {}
    wrong_examples = []
    for r in rows:
        rb = make()
        t = time.perf_counter()
        h = rb.hear(r["text"])
        ms.append((time.perf_counter() - t) * 1000)
        released += len(rb.released)                                        # nothing was confirmed, so nothing may have been released
        offered = h.status in ("confirm", "removed_dose_confirm")
        if r["target"] is None:
            n_none += 1
            false_offer += offered
            continue
        n_order += 1
        ok = offered and h.intent == r["intent"] and h.slots.get("target") == r["target"]
        right += ok
        wrong += offered and not ok
        menu += not offered
        by_kind.setdefault(r["kind"], [0, 0])
        by_kind[r["kind"]][0] += ok
        by_kind[r["kind"]][1] += 1
        if offered and not ok and len(wrong_examples) < 8:
            wrong_examples.append({"said": r["text"], "wanted": r["target"], "heard": h.slots.get("target"), "intent": h.intent})
    ms.sort()
    return {"orders": n_order, "correct": round(right / n_order, 3), "wrong": round(wrong / n_order, 3), "menu": round(menu / n_order, 3), "non_orders": n_none, "false_offer": round(false_offer / n_none, 3) if n_none else None,
            "released_without_confirmation": released, "latency_ms_p50": round(statistics.median(ms)), "latency_ms_p95": round(ms[int(len(ms) * 0.95) - 1]),
            "by_kind": {k: f"{v[0]}/{v[1]}" for k, v in sorted(by_kind.items())}, "wrong_examples": wrong_examples}


def regression(make_plain, make_model) -> dict:
    """The grammar's own phrases (eval_orders.py templates with mild corruption): the fallback must change nothing for a transcript the grammar already parses."""
    import random

    import eval_orders as E
    from server.orders.grammar import vocabulary
    vocab = vocabulary()
    rng = random.Random(11)
    same = diff = 0
    for e in vocab:
        if e.kind not in E.PHRASES or len(e.phrase) < 3:
            continue
        for template in E.PHRASES[e.kind]:
            said = template.format(x=e.phrase)
            said = E.corrupt(said, rng)
            a_, b_ = make_plain().hear(said), make_model().hear(said)
            if a_.status == "menu":
                continue                                                     # the fallback is only asked when the grammar found no order
            same += (a_.status, a_.intent, a_.slots.get("target")) == (b_.status, b_.intent, b_.slots.get("target"))
            diff += (a_.status, a_.intent, a_.slots.get("target")) != (b_.status, b_.intent, b_.slots.get("target"))
    return {"changed_where_the_grammar_already_answered": diff, "unchanged": same}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--skip-regression", action="store_true")
    a = ap.parse_args()
    from server.llm import client as C
    from server.orders.grammar import ReadBack, vocabulary
    from server.orders.llm_fallback import OrderMapper
    client = C.OllamaClient("http://127.0.0.1:11434")
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    vocab = vocabulary()
    mapper = OrderMapper(client, model=a.model, timeout=30.0)
    rows = load_rows()
    ReadBack(vocab, mapper).hear("warm up the model with one order please")
    out = {"model": a.model, "synthetic": True, "independent": False, "rows": len(rows), "grammar_only": score(rows, lambda: ReadBack(vocab)), "with_fallback": score(rows, lambda: ReadBack(vocab, mapper)),
           "limit": "61 paraphrases and 10 non-orders written by one author; real trainees and recogniser errors are not modelled (DC-08 on the Quest)"}
    if not a.skip_regression:
        out["regression"] = regression(lambda: ReadBack(vocab), lambda: ReadBack(vocab, mapper))
    out["gain_points"] = round(100 * (out["with_fallback"]["correct"] - out["grammar_only"]["correct"]), 1)
    print(json.dumps({k: v for k, v in out.items() if k not in ("limit",)}, indent=1))
    if a.write:
        p = ROOT / "reports" / "ai" / "orders-eval-llm.json"
        p.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
