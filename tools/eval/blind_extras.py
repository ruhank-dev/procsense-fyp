"""Blind rating of the plan composer's extras and of the reworded traps (EX-17, DT-224): `uv run --project tools tools/eval/blind_extras.py make|analyse ...`.

    make     [--seed 1] [--out docs/testing/results/EX-17]
             Reads the samples the evaluations wrote (reports/ai/plan-extras-eval.json `samples_for_human_rating`, reports/ai/trap-writer-eval.json `pairs_for_human_rating`) and writes `sheet.csv` (what the rater sees)
             and `key.json` (which trap wording is the authored one; open only after rating). Event and interaction items are rated 1 to 5 for relevance to the case. Trap items show two wordings, X and Y, in random
             order; the rater writes which one a busy clinician would more plausibly choose (X, Y or SAME).
    analyse  --a rater_a.csv --b rater_b.csv
             Opens the key and reports the mean relevance and the balance of trap judgements (reworded preferred minus authored preferred, over all judgements). The result is written to
             reports/ai/plan-extras-human-rating.json and reports/ai/trap-writer-human-rating.json, which the gates `plan_extras` and `trap_writer` read; nothing is invented, a missing score stops it.
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
OUT = ROOT / "docs" / "testing" / "results" / "EX-17"
REPORTS = ROOT / "reports" / "ai"
COLUMNS = ["item", "kind", "case", "shown", "relevance_1_to_5", "more_plausible_X_Y_or_same"]


def make(a) -> int:
    rng = random.Random(a.seed)
    extras = json.loads((REPORTS / "plan-extras-eval.json").read_text(encoding="utf-8"))["samples_for_human_rating"]
    traps = json.loads((REPORTS / "trap-writer-eval.json").read_text(encoding="utf-8"))["pairs_for_human_rating"]
    rows, key = [], {}
    for s in extras:
        for e in s["events"]:
            rows.append({"item": f"I{len(rows) + 1:03d}", "kind": "event", "case": s["case"], "shown": f"{e['event_id']} - {e['reason']}"})
        for i in s["interactions"]:
            rows.append({"item": f"I{len(rows) + 1:03d}", "kind": "interaction", "case": s["case"], "shown": f"{i['pair']} - {i['reason']}"})
    for t in traps:
        n = f"I{len(rows) + 1:03d}"
        flip = rng.random() < 0.5
        x, y = (t["reworded"], t["authored"]) if flip else (t["authored"], t["reworded"])
        key[n] = {"X_is": "reworded" if flip else "authored"}
        rows.append({"item": n, "kind": "trap", "case": t["case"], "shown": f"X: {x} || Y: {y}"})
    rng.shuffle(rows)
    OUT.mkdir(parents=True, exist_ok=True)
    with (OUT / "sheet.csv").open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, COLUMNS)
        w.writeheader()
        w.writerows({**r, "relevance_1_to_5": "", "more_plausible_X_Y_or_same": ""} for r in rows)
    (OUT / "key.json").write_text(json.dumps(key, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {len(rows)} items to {OUT.relative_to(ROOT).as_posix()}")
    return 0


def read(path: str) -> dict[str, dict]:
    with open(path, encoding="utf-8", newline="") as f:
        return {r["item"]: r for r in csv.DictReader(f)}


def analyse(a) -> int:
    key = json.loads((OUT / "key.json").read_text(encoding="utf-8"))
    sheets = [read(a.a), read(a.b)]
    rel: list[int] = []
    pref = same = worse = n_trap = 0
    for item in sheets[0]:
        for s in sheets:
            r = s[item]
            if r["kind"] in ("event", "interaction"):
                if not r["relevance_1_to_5"].strip():
                    raise SystemExit(f"missing relevance for {item}")
                rel.append(int(r["relevance_1_to_5"]))
            else:
                v = r["more_plausible_X_Y_or_same"].strip().upper()
                if v not in ("X", "Y", "SAME"):
                    raise SystemExit(f"missing or invalid choice for {item}")
                n_trap += 1
                x_is_reworded = key[item]["X_is"] == "reworded"
                if v == "SAME":
                    same += 1
                elif (v == "X") == x_is_reworded:
                    pref += 1
                else:
                    worse += 1
    mean_rel = round(statistics.mean(rel), 2) if rel else None
    REPORTS.mkdir(exist_ok=True)
    (REPORTS / "plan-extras-human-rating.json").write_text(json.dumps({"mean_relevance": mean_rel, "ratings": len(rel), "raters": 2}, indent=1) + "\n", encoding="utf-8", newline="\n")
    gain = round((pref - worse) / n_trap, 3) if n_trap else None
    (REPORTS / "trap-writer-human-rating.json").write_text(json.dumps({"plausibility_gain_over_authored": gain, "preferred_reworded": pref, "same": same, "preferred_authored": worse, "judgements": n_trap, "raters": 2}, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"mean relevance {mean_rel} ({len(rel)} ratings); trap judgements: reworded preferred {pref}, same {same}, authored preferred {worse}; balance {gain}")
    return 0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("make")
    m.add_argument("--seed", type=int, default=1)
    an = sub.add_parser("analyse")
    an.add_argument("--a", required=True)
    an.add_argument("--b", required=True)
    args = ap.parse_args()
    sys.exit(make(args) if args.cmd == "make" else analyse(args))
