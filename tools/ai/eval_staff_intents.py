"""Staff intent selection on a small labelled synthetic set (ADR-0026): `uv run --project tools tools/ai/eval_staff_intents.py --model gemma4:31b-cloud`.

Each item is a trainee sentence aimed at the nurse or the anaesthetist (tests/dialogue_staff/staff_intents_v1.json, written by an AI assistant, not human-labelled). The production code chooses (`Dialogue.pick_many`, both roles in
one call, the way the three-voice turn does) and the intent chosen for the addressed role must be one the set accepts. Reported: the share accepted per role, invalid replies (fallbacks), and latency. Results go to
artifacts/ai-bench/ (git-ignored). Synthetic data only; nothing real leaves the machine.
"""
import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--repeats", type=int, default=3)
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    from dialogue import dialogue as D
    from llm import client as L
    client = L.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    intents = json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]
    items = json.loads((ROOT / "tests" / "dialogue_staff" / "staff_intents_v1.json").read_text(encoding="utf-8"))["items"]
    d = D.Dialogue(client, model=a.model, timeout=60.0)
    ok = {"nurse": [0, 0], "anaesthesia": [0, 0]}
    fallbacks, lat, misses = 0, [], []
    for rep in range(a.repeats):
        for it in items:
            ctxs = {r: {"role": r, "case": "SYN", "allowed_intents": intents[r], "facts": [], "last_turns": [{"who": "trainee", "text": it["say"]}], "results": []} for r in ("nurse", "anaesthesia")}
            t = time.monotonic()
            out = d.pick_many(ctxs, it["say"], {})
            lat.append((time.monotonic() - t) * 1000)
            r = out[it["role"]]
            fallbacks += r.source == "fallback"
            ok[it["role"]][1] += 1
            if r.source != "fallback" and r.intent in it["accept"]:
                ok[it["role"]][0] += 1
            elif len(misses) < 12:
                misses.append({"say": it["say"], "role": it["role"], "chose": r.intent, "source": r.source, "accepted": it["accept"]})
    n = sum(v[1] for v in ok.values())
    res = {"model": a.model, "set": "staff_intents_v1 (synthetic, AI-written)", "items": len(items), "repeats": a.repeats, "accepted_share": round(sum(v[0] for v in ok.values()) / n, 3),
           "by_role": {k: round(v[0] / v[1], 3) for k, v in ok.items()}, "fallback_share": round(fallbacks / n, 3), "ms_p50": round(statistics.median(lat)), "ms_p95": round(sorted(lat)[int(0.95 * len(lat)) - 1]), "misses": misses}
    path = Path(a.out) if a.out else ROOT / "artifacts" / "ai-bench" / "staff-intents.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(res, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in res.items() if k != "misses"}, indent=1))
