"""Where the time of one spoken-order hearing goes (ADR-0021): `uv run --project tools tools/ai/profile_orders.py [--n 16] [--prompt-version 2|3]`.

For the first N orders of the development set (tests/orders/paraphrases.json) the real mapper is run and every call to the model host is timed from the host's own report: total, model load, prompt evaluation (and how many tokens), generation (and how many), and what is left over (serialization, the network hop, the Python code
around the call). Reported per call type (kind stage, first reading, second reading) as medians, plus the Python time outside the host calls (grammar, shortlist, re-ranking). Writes nothing unless --write (reports/ai/orders-latency-profile.json).
"""
from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import eval_orders_llm as E  # noqa: E402
from server.llm import client as C  # noqa: E402
from server.orders import llm_fallback as LF  # noqa: E402
from server.orders.grammar import ReadBack, vocabulary  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=16)
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--label", default="current")
    ap.add_argument("--lean", type=int, default=0)
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    a = ap.parse_args()
    client = C.OllamaClient(a.ollama)
    calls: list[dict] = []
    post = client._http.post

    def timed(url, **kw):
        t = time.perf_counter()
        r = post(url, **kw)
        wall = (time.perf_counter() - t) * 1000
        try:
            d = r.json()
        except ValueError:
            d = {}
        fmt = json.dumps(kw.get("json", {}).get("format", {}))
        calls.append({"type": "kind" if '"kind"' in fmt else "map", "wall_ms": wall, "total_ms": d.get("total_duration", 0) / 1e6, "load_ms": d.get("load_duration", 0) / 1e6, "prompt_ms": d.get("prompt_eval_duration", 0) / 1e6,
                      "prompt_tokens": d.get("prompt_eval_count", 0), "gen_ms": d.get("eval_duration", 0) / 1e6, "gen_tokens": d.get("eval_count", 0)})
        return r
    client._http.post = timed                                            # type: ignore[method-assign]
    vocab = vocabulary()
    mapper = LF.OrderMapper(client, timeout=60.0, lean=a.lean)
    ReadBack(vocab, mapper).hear("warm up the model with one order please")
    calls.clear()
    rows = [r for r in E.load_rows("paraphrases.json") if r["target"]][: a.n]
    hear_ms, outside = [], []
    for r in rows:
        before = len(calls)
        t = time.perf_counter()
        ReadBack(vocab, mapper).hear(r["text"])
        total = (time.perf_counter() - t) * 1000
        in_calls = sum(c["wall_ms"] for c in calls[before:])
        hear_ms.append(total)
        outside.append(total - in_calls)
    by = defaultdict(list)
    for i, c in enumerate(calls):
        by[c["type"]].append(c)
    out = {"label": a.label, "hearings": len(rows), "calls": len(calls), "calls_per_hearing": round(len(calls) / len(rows), 2), "hearing_ms_median": round(statistics.median(hear_ms)), "hearing_ms_p95": round(sorted(hear_ms)[int(len(hear_ms) * 0.95) - 1]),
           "python_outside_host_calls_ms_median": round(statistics.median(outside), 1), "by_call_type": {}}
    for k, v in by.items():
        med = lambda f: round(statistics.median(x[f] for x in v), 1)          # noqa: E731
        out["by_call_type"][k] = {"n": len(v), "wall_ms": med("wall_ms"), "host_total_ms": med("total_ms"), "load_ms": med("load_ms"), "prompt_eval_ms": med("prompt_ms"), "prompt_tokens": med("prompt_tokens"), "generation_ms": med("gen_ms"), "generation_tokens": med("gen_tokens"),
                                  "overhead_ms": round(statistics.median(x["wall_ms"] - x["total_ms"] for x in v), 1)}
    print(json.dumps(out, indent=1))
    if a.write:
        (ROOT / "reports" / "ai" / f"orders-latency-profile-{a.label}.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
