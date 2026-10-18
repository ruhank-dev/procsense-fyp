"""Measures the plan composer's further proposals (features A4, A5, A8; capabilities A4.c2, A5.c2, A8.c2; DT-224): `uv run --project tools tools/ai/eval_plan_extras.py [--model qwen3.5:4b-q4_K_M] [--write]`.

Every case of the library (the 53 cases of content/composer) is given to the ExtrasPlanner with the real model. Reported: how many cases had eligible events and how many had knowledge-base interaction candidates
(code-made, no model), how many proposals the model made, how many were refused by the deterministic validation (`refused`: an id outside the candidates, a repeat, a what-if that does not belong to the event), how
many ended with at least one event or interaction, the share of the final proposals that are INELIGIBLE (must be 0: they are checked, not trusted), how often the model chose no interaction at all although candidates
existed, and latency. What it cannot show is whether a person finds the chosen events or interactions relevant and plausible (EX-03 in docs/testing/SANNAN_EXTERNAL_TESTING.md): the sampled reasons are written out for that.
Needs a running Ollama with the model. `--write` saves reports/ai/plan-extras-eval.json.
"""
from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--timeout", type=float, default=300.0)
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    from composer import blocks as B
    from generation import plan_extras as X
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases = B.load_cases()
    cases = cases[:a.limit] if a.limit else cases
    planner = X.ExtrasPlanner(client, a.model, a.timeout)
    n = with_events_elig = with_cands = proposed = refused = got_events = got_inter = none_chosen = ineligible = 0
    secs, samples, event_use = [], [], {}
    for c in cases:
        n += 1
        elig, cands = X.eligible_events(c), X.interaction_candidates(c)
        with_events_elig += bool(elig)
        with_cands += bool(cands)
        t = time.monotonic()
        r = planner.propose(c, {})
        secs.append(time.monotonic() - t)
        if r is None:
            continue
        proposed += 1
        if r.get("refused"):
            refused += 1
            continue
        ok_ids = {e["id"] for e in elig}
        ineligible += sum(e["event_id"] not in ok_ids for e in r["events"])
        got_events += bool(r["events"])
        got_inter += bool(r["interactions"])
        none_chosen += bool(cands) and not r["interactions"]
        for e in r["events"]:
            event_use[e["event_id"]] = event_use.get(e["event_id"], 0) + 1
        if len(samples) < 8 and (r["events"] or r["interactions"]):
            samples.append({"case": c["id"], "events": r["events"], "interactions": r["interactions"]})
    secs.sort()
    out = {"synthetic": False, "model": a.model, "digest": client.models().get(a.model, ""), "cases": n, "cases_with_eligible_events": with_events_elig, "cases_with_interaction_candidates": with_cands,
           "proposals_made": proposed, "refused_by_validation": refused, "cases_with_events": got_events, "share_cases_with_interaction": round(got_inter / n, 3), "baseline_share_cases_with_interaction": round(with_cands / n, 3), "cases_with_interactions": got_inter, "model_chose_no_interaction_despite_candidates": none_chosen,
           "ineligible_events_in_final_proposals": ineligible, "event_use": dict(sorted(event_use.items())), "seconds_p50": round(statistics.median(secs), 1), "seconds_p95": round(secs[int(0.95 * (len(secs) - 1))], 1),
           "samples_for_human_rating": samples,
           "note": "Proxies only: the validator decides what is allowed; whether the chosen events/interactions are relevant to the case is a human rating (EX-03)."}
    print(json.dumps(out, indent=2))
    if a.write:
        (ROOT / "reports" / "ai" / "plan-extras-eval.json").write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8", newline="\n")
