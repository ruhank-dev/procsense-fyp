"""Measures the AI plan composer against the solver alone (p1-live-scenario-generation-s13, p1-scenario-composer-difficulty-s03, EXP-COMP-1): `uv run --project tools tools/ai/eval_planner.py --model qwen3.5:4b-q4_K_M --requests 20`.

The same requests (a simulated trainee with ratings, so the objectives name dimensions) are generated twice through the production pipeline: once with the planner (the model proposes the story and the blocks, the solver and
validators decide) and once with the deterministic skeleton alone. Reported for each arm: how many packages passed the whole gate, how many were served, the quality metrics (coverage of the requested dimensions,
difficulty match), the number of distinct block sets and stories, and for the planner arm: first-try schema validity, proposals accepted by the composer's rules, rounds used, fallbacks to the solver and latency.
What this cannot show is the blind human comparison the plan asks for (two raters, 40 cases, DT-203): the coverage and difficulty numbers are deterministic proxies, and the model's stated reasons are written to the
output for a person to read. Results go to artifacts/ai-bench/ (git-ignored). Needs a running Ollama.
"""
import argparse
import json
import random
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
    ap.add_argument("--requests", type=int, default=20)
    ap.add_argument("--timeout", type=float, default=300.0)
    ap.add_argument("--spine-version", type=int, default=None, help="prompt version of compose_plan.spine (default: the latest)")
    ap.add_argument("--tag", default="", help="suffix for the result file")
    a = ap.parse_args()
    from composer import blocks as B
    from generation import compose_plan, history as H, objectives as ob, package, pipeline, retrieve
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases = B.load_cases()
    tables = ob.Tables.load()
    index = retrieve.build_index()
    behaviours = sorted(b for d in tables.dimensions for b in d.weak_behaviours)
    arms = {"solver": pipeline.Context(index, cases, tables), "planner": pipeline.Context(index, cases, tables, planner=compose_plan.Planner(client, model=a.model, timeout=a.timeout, spine_version=a.spine_version))}
    out = {"model": a.model, "spine_prompt_version": a.spine_version, "shortlist": compose_plan.SHORTLIST, "digest": client.models().get(a.model, ""), "requests": a.requests, "arms": {}}
    reasons = []
    for arm, ctx in arms.items():
        served = passed = model_plans = 0
        cov, diff, blocks, stories, secs, rounds, first_try = [], [], set(), set(), [], [], 0
        for i in range(a.requests):
            rng = random.Random(1000 + i)
            skill = (i % 5) / 5.0
            hist = [H.HistoryEntry("trainee-000", f"s{k}", f"X{k}", "VAR-S1-A", "P0", {b: [g, 6 - g] for b in behaviours if rng.random() < 0.6 for g in [sum(rng.random() < 0.2 + 0.7 * skill for _ in range(6))]},
                                   "2026-10-17T00:00:00Z") for k in range(3)]
            req = {"version": 1, "trainee": "trainee-000", "mode": "practice", "scenario": "S1" if i % 2 == 0 else "S2", "level": "advanced", "seed": 500 + i}
            t = time.monotonic()
            r = pipeline.generate(req, H.session_records(hist), [], ctx)
            secs.append(time.monotonic() - t)
            if not r.package:
                continue
            served += 1
            p = r.package
            passed += package.validate(p, cases)["passed"]
            q = p["quality"]["metrics"]
            if q.get("coverage") is not None:
                cov.append(q["coverage"])
            diff.append(q["difficulty"])
            blocks.add(",".join(p["provenance"]["blocks"]))
            stories.add(p["provenance"]["spine"])
            pl = p["provenance"].get("plan", {})
            if pl.get("method") == "model":
                model_plans += 1
                rounds.append(pl["rounds"])
                reasons.append({"request": i, "spine": p["provenance"]["spine"], "reasons": pl["reasons"]})
        out["arms"][arm] = {"served": served, "passed_gate": passed, "coverage_mean": round(statistics.mean(cov), 3) if cov else None, "difficulty_mean": round(statistics.mean(diff), 3) if diff else None,
                            "distinct_block_sets": len(blocks), "distinct_stories": len(stories), "seconds_p50": round(statistics.median(secs), 1), "seconds_max": round(max(secs), 1),
                            "model_plans_used": model_plans, "rounds_mean": round(statistics.mean(rounds), 2) if rounds else None}
    out["sample_reasons"] = reasons[:10]
    path = ROOT / "artifacts" / "ai-bench" / f"planner-eval{a.tag}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=2))
