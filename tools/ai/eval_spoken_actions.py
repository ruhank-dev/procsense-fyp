"""Measures the spoken-action interpreter on a synthetic set (DT-251, ADR-0027): `uv run --project tools tools/ai/eval_spoken_actions.py --model gemma4:31b-cloud [--out file.json]`.

The production code answers (server/speech_action) with every action available. Reported: the share of sentences whose action list is exactly the expected one, the share of sentences that should start nothing
and start nothing (the safety figure: a denial, a plan, a hypothetical, a staff request or a physical act must not start an action), the recall of expected actions, replies that were refused (fallbacks) and latency.
The set (tests/speech_actions/spoken_actions_v1.json) is AI-written and not human-labelled. Synthetic text only; nothing real is sent.
"""
import argparse
import json
import statistics
import sys
import time
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--repeats", type=int, default=2)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    from llm import client as L
    from speech_action import service as S
    client = L.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    items = json.loads((ROOT / "tests" / "speech_actions" / "spoken_actions_v1.json").read_text(encoding="utf-8"))["items"]
    sa = S.SpeechActions(client, model=a.model, timeout=60.0)
    available = list(S.catalogue())
    exact, none_n, none_ok, exp_n, exp_hit, fallbacks, lat = 0, 0, 0, 0, 0, 0, []
    by_kind = defaultdict(lambda: [0, 0])
    misses = []
    for _ in range(a.repeats):
        for it in items:
            t = time.monotonic()
            r = sa.interpret(available, it.get("turns", []), it["say"])
            lat.append((time.monotonic() - t) * 1000)
            fallbacks += r.source == "fallback"
            want = set(it["expect"])
            got = set(r.actions) if r.source == "model" else set()
            ok = got == want
            exact += ok
            by_kind[it["kind"]][0] += ok
            by_kind[it["kind"]][1] += 1
            if not want:
                none_n += 1
                none_ok += not got
            else:
                exp_n += len(want)
                exp_hit += len(want & got)
            if not ok and len(misses) < 20:
                misses.append({"say": it["say"], "kind": it["kind"], "expected": sorted(want), "got": sorted(got), "source": r.source, "reasons": r.reasons})
    n = len(items) * a.repeats
    out = {"model": a.model, "set": "spoken_actions_v1 (synthetic, AI-written)", "sentences": len(items), "repeats": a.repeats, "exact_match": round(exact / n, 3),
           "nothing_when_nothing_expected": round(none_ok / none_n, 3), "false_action_rate": round(1 - none_ok / none_n, 3), "recall_of_expected": round(exp_hit / exp_n, 3), "fallback_share": round(fallbacks / n, 3),
           "by_kind": {k: round(v[0] / v[1], 3) for k, v in sorted(by_kind.items())}, "ms_p50": round(statistics.median(lat)), "ms_p95": round(sorted(lat)[int(0.95 * len(lat)) - 1]), "misses": misses}
    path = Path(a.out) if a.out else ROOT / "artifacts" / "ai-bench" / "spoken-actions.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "misses"}, indent=1))
