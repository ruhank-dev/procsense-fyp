"""Blind comparison of the AI plan composer with the solver alone, for two human raters (EX-03, DT-203): `uv run --project tools tools/eval/blind_compare.py make|analyse ...`.

    make     [--cases 40] [--seed 1] [--model qwen3.5:4b-q4_K_M] [--out docs/testing/results/EX-03]
             Generates the same requests through the production pipeline twice (the planner arm needs a running Ollama; the solver arm needs nothing), shows each result as plain text, shuffles the two arms for every request
             and writes `sheet.csv` (what the raters see: item id, the objectives asked of the case, the case text, empty score columns) and `key.json` (which item is which arm: nobody opens it before analysis).
    analyse  --a rater_a.csv --b rater_b.csv [--key key.json]
             Reads two raters' filled sheets, opens the key, and reports for coherence and for fit to the objectives: the mean score per arm, the paired difference (planner minus solver, averaged over the two raters)
             with a bootstrap 95 percent interval, the quadratic-weighted kappa between the raters, and the verdict against the rule of p1-live-scenario-generation-s13 (the planner is better only when the interval is above zero and
             the raters agree at kappa 0.4 or more; with weaker agreement the result is reported as unreliable). It invents no rating: with a missing score it stops.

A rater scores each item 1 (poor) to 5 (excellent): `coherence` (does the case hang together as one believable patient and story) and `fit` (does it exercise the objectives that were asked for). The text of an item never names the arm.
"""
from __future__ import annotations

import argparse
import csv
import json
import random
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
OUT = ROOT / "docs" / "testing" / "results" / "EX-03"
COLUMNS = ["item", "objectives", "case", "coherence", "fit"]
MIN_KAPPA = 0.4


def render_case(pkg: dict) -> str:
    c = pkg["case"]
    pres = c["presentation"]
    lines = [f"Scenario {c['scenario']}, {pres['setting']}. {pres['summary']}", "Problems in the case: " + "; ".join(f"{p['label']} ({p['type'].replace('_', ' ')})" for p in c["problems"])]
    lines += [f"Decision {i + 1}: {d['prompt']}" for i, d in enumerate(c["decisions"][:4])]
    allergies = c.get("allergies") or []
    lines.append("Allergies: " + (", ".join(a if isinstance(a, str) else a.get("substance", str(a)) for a in allergies) or "none recorded"))
    return "\n".join(lines)


def make(a: argparse.Namespace) -> int:
    from composer import blocks as B
    from generation import compose_plan, contracts, history as H, objectives as ob, pipeline, retrieve
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases, tables, index = B.load_cases(), ob.Tables.load(), retrieve.build_index()
    behaviours = sorted(b for d in tables.dimensions for b in d.weak_behaviours)
    arms = {"solver": pipeline.Context(index, cases, tables), "planner": pipeline.Context(index, cases, tables, planner=compose_plan.Planner(client, model=a.model, timeout=300.0))}
    rng_shuffle = random.Random(a.seed)
    rows, key = [], {}
    for i in range(a.cases):
        rng = random.Random(1000 + i + 100 * a.seed)
        skill = (i % 5) / 5.0
        hist = [H.HistoryEntry("trainee-000", f"s{k}", f"X{k}", "VAR-S1-A", "P0", {b: [g, 6 - g] for b in behaviours if rng.random() < 0.6 for g in [sum(rng.random() < 0.2 + 0.7 * skill for _ in range(6))]}, "2026-10-17T00:00:00Z") for k in range(3)]
        req = {"version": 1, "trainee": "trainee-000", "mode": "practice", "scenario": "S1" if i % 2 == 0 else "S2", "level": "advanced", "seed": 500 + i + 1000 * a.seed}
        obj = ob.derive(contracts.accept_request(req, lambda n: 0), H.session_records(hist), tables, None)
        wanted = ", ".join(d.replace("_", " ") for d in obj["dimensions"]) + (" (competencies: " + ", ".join(c["behaviour"] for c in obj["competencies"]) + ")" if obj.get("competencies") else "")
        texts = {}
        for arm, ctx in arms.items():
            r = pipeline.generate(req, H.session_records(hist), [], ctx)
            texts[arm] = render_case(r.package) if r.package else None
        if None in texts.values():
            continue
        order = ["solver", "planner"]
        rng_shuffle.shuffle(order)
        for tag, arm in zip("AB", order):
            item = f"R{i:02d}{tag}"
            rows.append({"item": item, "objectives": wanted, "case": texts[arm], "coherence": "", "fit": ""})
            key[item] = arm
    a.out.mkdir(parents=True, exist_ok=True)
    with (a.out / "sheet.csv").open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=COLUMNS)
        w.writeheader()
        w.writerows(rows)
    (a.out / "key.json").write_text(json.dumps({"model": a.model, "seed": a.seed, "key": key}, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {len(rows)} items for {len(rows) // 2} requests to {a.out}; do not open key.json before the raters have finished")
    return 0


def read_scores(path: Path) -> dict[str, dict[str, int]]:
    out = {}
    with path.open(encoding="utf-8", newline="") as f:
        for r in csv.DictReader(f):
            for col in ("coherence", "fit"):
                v = (r[col] or "").strip()
                if not v.isdigit() or not 1 <= int(v) <= 5:
                    raise SystemExit(f"{path.name}: item {r['item']} has no valid {col} score (1 to 5); a missing rating is never filled in")
            out[r["item"]] = {"coherence": int(r["coherence"]), "fit": int(r["fit"])}
    return out


def weighted_kappa(a: list[int], b: list[int]) -> float:
    """Quadratic-weighted kappa on 1..5."""
    n = len(a)
    obs = [[0.0] * 5 for _ in range(5)]
    for x, y in zip(a, b):
        obs[x - 1][y - 1] += 1
    ra = [sum(row) for row in obs]
    cb = [sum(obs[i][j] for i in range(5)) for j in range(5)]
    num = den = 0.0
    for i in range(5):
        for j in range(5):
            w = (i - j) ** 2 / 16.0
            num += w * obs[i][j]
            den += w * ra[i] * cb[j] / n
    return 1.0 - num / den if den else 1.0


def bootstrap(diffs: list[float], rounds: int = 2000, seed: int = 5) -> tuple[float, float, float]:
    rng = random.Random(seed)
    means = sorted(statistics.mean(rng.choices(diffs, k=len(diffs))) for _ in range(rounds))
    return statistics.mean(diffs), means[int(0.025 * rounds)], means[int(0.975 * rounds)]


def analyse_scores(sa: dict, sb: dict, key: dict[str, str]) -> dict:
    if set(sa) != set(sb) or set(sa) != set(key):
        raise SystemExit("the two sheets and the key must list the same items")
    out = {"items": len(key), "criteria": {}}
    requests = sorted({i[:-1] for i in key})
    for crit in ("coherence", "fit"):
        per_arm = {arm: statistics.mean((sa[i][crit] + sb[i][crit]) / 2 for i in key if key[i] == arm) for arm in ("solver", "planner")}
        diffs = []
        for r in requests:
            pair = {key[r + t]: (sa[r + t][crit] + sb[r + t][crit]) / 2 for t in "AB"}
            diffs.append(pair["planner"] - pair["solver"])
        mean, lo, hi = bootstrap(diffs)
        kappa = weighted_kappa([sa[i][crit] for i in sorted(key)], [sb[i][crit] for i in sorted(key)])
        verdict = "unreliable: raters disagree" if kappa < MIN_KAPPA else ("planner better" if lo > 0 else ("solver better" if hi < 0 else "no clear difference"))
        out["criteria"][crit] = {"mean_solver": round(per_arm["solver"], 3), "mean_planner": round(per_arm["planner"], 3), "paired_difference": round(mean, 3), "ci95": [round(lo, 3), round(hi, 3)], "weighted_kappa": round(kappa, 3), "verdict": verdict}
    out["planner_adopted_over_solver"] = all(c["verdict"] == "planner better" for c in out["criteria"].values())
    return out


def analyse(a: argparse.Namespace) -> int:
    key = json.loads(Path(a.key).read_text(encoding="utf-8"))["key"]
    result = analyse_scores(read_scores(Path(a.a)), read_scores(Path(a.b)), key)
    print(json.dumps(result, indent=1))
    return 0


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("make")
    m.add_argument("--cases", type=int, default=40)
    m.add_argument("--seed", type=int, default=1)
    m.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    m.add_argument("--out", type=Path, default=OUT)
    an = sub.add_parser("analyse")
    an.add_argument("--a", required=True)
    an.add_argument("--b", required=True)
    an.add_argument("--key", default=str(OUT / "key.json"))
    a = ap.parse_args(argv)
    return make(a) if a.cmd == "make" else analyse(a)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
