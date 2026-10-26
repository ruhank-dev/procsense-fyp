"""Scores the semantic order interpreter (ADR-0028) on a AI-assistant-authored set of tests/orders/ai_authored/: `PROCSENSE_RELAY_MODELS=gemma4:31b-cloud uv run --project tools tools/ai/eval_orders_ai.py --set dev_v1 [--model gemma4:31b-cloud] [--write]`.

Every row goes through ReadBack.hear (the same path as the /hear route) with the interpreter, the vocabulary and the scenario context of the row. A `propose` row passes when the hearing is a read-back ("confirm") of the expected item (or an accepted one); a `clarify` row
passes when the hearing asks; `no_order` and `unsupported` rows pass when nothing is read back. Nothing is released without a yes, so the confirmation-bypass count is measured by sending every proposal through ReadBack.confirm with a wrong answer and with
no answer and counting the actions that come out (it must be 0).

A set marked `heldout` is scored ONCE per frozen implementation: a result file `reports/ai/orders-ai-<set>.json` that exists blocks a second run unless --second-look is given (and the file then says so). Its labels are never edited after a prediction is seen.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import statistics
import subprocess
import sys
import time
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tests" / "orders" / "ai_authored"))
from common import SAFETY_ITEMS  # noqa: E402
from server.llm import client as C  # noqa: E402
from server.orders.grammar import ReadBack, vocabulary  # noqa: E402
from server.orders.interpreter import OrderInterpreter  # noqa: E402

SETS = ROOT / "tests" / "orders" / "ai_authored"
OUT = ROOT / "reports" / "ai"


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def context_for(doc: dict, row: dict) -> str:
    c = doc["contexts"][row["context_id"]]
    return c + (" Previous turns: " + row["previous"] if row.get("previous") else "")


def judge(row: dict, h) -> tuple[str, str | None]:
    seen = "propose" if h.status in ("confirm", "removed_dose_confirm") else "clarify" if h.status == "clarify" else "no_order"
    return seen, proposed_key(h) if seen == "propose" else None


def compare(name: str) -> int:
    reps = {k: json.loads((OUT / f"orders-ai-{name}{suf}.json").read_text(encoding="utf-8")) for k, suf in (("cloud", ""), ("local", "-qwen_second_look"), ("grammar", "-grammar_alone"))}
    out = {"set": name, "independent_human": False, "authorship": reps["cloud"]["authorship"],
           "accuracy": {k: v["accuracy"] for k, v in reps.items()}, "cloud_gain_over_grammar": round(reps["cloud"]["accuracy"] - reps["grammar"]["accuracy"], 4),
           "note": "cloud: the frozen interpreter with gemma4:31b-cloud, scored once; local: the same interpreter with the local model (second look, for the routing rule only); grammar: no model"}
    (OUT / f"orders-ai-{name}-comparison.json").write_text(json.dumps(out, indent=1) + chr(10), encoding="utf-8", newline=chr(10))
    print(json.dumps(out, indent=1))
    return 0


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="dev_v1")
    ap.add_argument("--model", default="gemma4:31b-cloud")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--second-look", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--compare", action="store_true", help="write the comparison file of the stored reports of the set (cloud interpreter, local second look, grammar alone)")
    ap.add_argument("--tag", default="", help="suffix of the result file for a comparison run of ANOTHER model on an already-scored set (a second look: the result file says so; nothing is tuned on it)")
    a = ap.parse_args()
    if a.compare:
        return compare(a.set)
    path = SETS / f"{a.set}.json"
    doc = json.loads(path.read_text(encoding="utf-8"))
    out_path = OUT / f"orders-ai-{a.set}{'-' + a.tag if a.tag else ''}.json"
    if doc["role"].startswith("held") and out_path.exists() and not a.second_look:
        raise SystemExit(f"REFUSED: the held-out set {a.set} was already scored (use --second-look to score it again; the file then says so)")
    vocab = vocabulary()
    grammar_only = a.model == "grammar"                                  # the deterministic grammar with its question guard and no model: the baseline the plan compares against
    if grammar_only:
        interp = None
    else:
        client = C.OllamaClient("http://127.0.0.1:11434")
        if a.model not in client.models():
            raise SystemExit(f"{a.model} is not available (set PROCSENSE_RELAY_MODELS for a cloud model)")
        interp = OrderInterpreter(client, model=a.model, timeout=60.0)
    served = []
    inner = interp.check if interp else None

    def checked(v, by_id, model=""):                                  # the model that really answered: a cloud limit or outage makes the gateway use the local fallback, and that is not the model under test
        served.append(model)
        return inner(v, by_id, model=model)
    if interp:
        interp.check = checked
    rows = doc["rows"][: a.limit or None]
    res, lat, bypass = [], [], 0
    for r in rows:
        for attempt in (range(1) if grammar_only else range(4)):                                         # one transient failure of the cloud call is retried (the same sentence, the same prompt); a row that cannot be answered by the model after that aborts the run
            rb = ReadBack(vocab, None, interp)
            n_before = len(served)
            t0 = time.monotonic()
            h = rb.hear(r["text"], context_for(doc, r))
            took = time.monotonic() - t0
            if grammar_only or (len(served) > n_before and served[-1] == a.model):
                break
            time.sleep(4)
        lat.append(took)
        if not grammar_only and (len(served) == n_before or served[-1] != a.model):
            raise SystemExit(f"ABORTED at {r['id']}: {a.model} did not answer after 4 tries (served by {served[-1] if served else None!r}); nothing is written. A cloud limit or outage is not a result.")
        seen, proposed = judge(r, h)
        exp = r["expect"]
        want = {exp["target"], *exp["accept"]} - {None}
        if exp["behaviour"] == "propose":
            ok = seen == "propose" and proposed_key(h) in want
        else:
            ok = seen == ("clarify" if exp["behaviour"] == "clarify" else "no_order")
        if h.status in ("confirm", "removed_dose_confirm"):               # release test: no yes, a wrong answer and silence must never give an action
            for spoken in ("", "maybe later", "tell me more"):
                if rb.pending is not None and rb.confirm(spoken):
                    bypass += 1
                rb.pending = h
        res.append({"id": r["id"], "category": r["category"], "ok": ok, "expected": exp["behaviour"], "want": sorted(want), "seen": seen, "proposed": proposed_key(h) if seen == "propose" else None,
                    "interpretation": h.interpretation, "text": r["text"]})
    n = len(res)
    by_cat = defaultdict(lambda: [0, 0])
    for x in res:
        by_cat[x["category"]][0] += x["ok"]
        by_cat[x["category"]][1] += 1
    proposes = [x for x in res if x["seen"] == "propose"]
    wrong_item = [x for x in res if x["expected"] == "propose" and x["seen"] == "propose" and not x["ok"]]
    nonorder = [x for x in res if x["expected"] in ("no_order", "unsupported") and x["seen"] == "propose"]
    nonorder_n = sum(1 for x in res if x["expected"] in ("no_order", "unsupported"))
    safety = [x for x in res if not x["ok"] and x["seen"] == "propose" and (x["proposed"] in SAFETY_ITEMS or any(w in SAFETY_ITEMS for w in x["want"]))]
    out = {"set": a.set, "set_sha256": sha(path), "rows_sha256": hashlib.sha256(json.dumps(doc["rows"], sort_keys=True, ensure_ascii=False).encode()).hexdigest(), "model": a.model, "role": doc["role"], "authorship": doc["authorship"], "independent_human": False, "second_look": bool(a.second_look),
           "prompt_sha256": sha(ROOT / "prompts" / "orders" / "interpret_v1.txt"), "interpreter_sha256": sha(ROOT / "server" / "orders" / "interpreter.py"),
           "commit": subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True, cwd=ROOT).stdout.strip(),
           "rows": n, "correct": sum(x["ok"] for x in res), "accuracy": round(sum(x["ok"] for x in res) / n, 4),
           "by_category": {k: {"correct": v[0], "n": v[1], "accuracy": round(v[0] / v[1], 3)} for k, v in sorted(by_cat.items())},
           "wrong_item_rate": round(len(wrong_item) / max(1, sum(1 for x in res if x["expected"] == "propose")), 4), "wrong_item": len(wrong_item),
           "non_order_false_positives": len(nonorder), "non_order_rows": nonorder_n, "non_order_false_positive_rate": round(len(nonorder) / max(1, nonorder_n), 4),
           "safety_related_incorrect_proposals": len(safety), "confirmation_bypasses": bypass,
           "latency_s": {"median": round(statistics.median(lat), 2), "p95": round(sorted(lat)[int(0.95 * (len(lat) - 1))], 2), "max": round(max(lat), 2)},
           "failures": [x for x in res if not x["ok"]]}
    print(json.dumps({k: v for k, v in out.items() if k not in ("failures",)}, indent=1))
    for x in out["failures"]:
        print(f"  FAIL {x['id']} [{x['category']}] exp={x['expected']} want={x['want']} saw={x['seen']} {x['proposed'] or ''} ({x['interpretation']}) :: {x['text']}")
    if a.write:
        out_path.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    return 0


def proposed_key(h) -> str | None:
    tgt = (h.slots or {}).get("target")
    if tgt is None:
        return None
    if h.intent == "HOLD_MED":
        return f"home_medicine:{tgt}" if (h.action or {}).get("verb") == "hold_medicine" else f"medicine:{tgt}"
    kind = {"ORDER_TEST": "test", "REQUEST_MEDICATION": "medicine", "REQUEST_TRANSPORT": "place", "CALL_STAFF": "staff"}.get(h.intent)
    return f"{kind}:{tgt}"


if __name__ == "__main__":
    raise SystemExit(main())
