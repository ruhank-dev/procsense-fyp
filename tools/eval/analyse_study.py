"""Analysis of the two studies of p1-evaluation (s03 learning effect, s04 AI dialogue A/B): `uv run --project tools tools/eval/analyse_study.py learning|ab --csv data.csv [--out results.md]`.

Nothing here collects data. It reads the table a study produces (one row per participant and session) and gives paired differences with effect sizes, bootstrap intervals and sign-flip permutation tests (exact for up
to 16 participants), written as a results page whose limitations come with it. Participant ids starting `SIM` mark a simulation: the page then says so in its first line and the script refuses to write it into
`docs/plans/p1/p1-evaluation/results/`, so a simulated table can never be mistaken for a result about people. No control group exists in P1, so the learning page says what it cannot show.

learning.csv   participant,session,quiz_before,quiz_after,critical_items,correct_end_state,seconds_to_decision     session 1 and 2, at least a day apart, matched but different cases
ab.csv         participant,order,mode,realism,task_success,seconds,rejections,fallbacks,safety_issues              order menu_first or ai_first; mode menu or ai
"""
from __future__ import annotations

import argparse
import csv
import itertools
import math
import random
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESULTS = ROOT / "docs" / "plans" / "p1" / "p1-evaluation" / "results"
MIN_N = 8


def read(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def num(x) -> float:
    return float(x)


def sign_flip_p(diffs: list[float], n_random: int = 20000, seed: int = 0) -> float:
    """Two-sided permutation test of 'the mean difference is zero': every sign pattern for up to 16 participants, 20,000 random ones above that."""
    d = [x for x in diffs if x != 0]
    if not d:
        return 1.0
    obs = abs(sum(d))
    if len(d) <= 16:
        hits = sum(abs(sum(s * x for s, x in zip(signs, d))) >= obs - 1e-12 for signs in itertools.product((1, -1), repeat=len(d)))
        return round(hits / 2 ** len(d), 4)
    rng = random.Random(seed)
    hits = sum(abs(sum(rng.choice((1, -1)) * x for x in d)) >= obs - 1e-12 for _ in range(n_random))
    return round((hits + 1) / (n_random + 1), 4)


def bootstrap_ci(diffs: list[float], seed: int = 0, n: int = 5000) -> list[float]:
    rng = random.Random(seed)
    means = sorted(statistics.mean(rng.choice(diffs) for _ in diffs) for _ in range(n))
    return [round(means[int(0.025 * n)], 3), round(means[int(0.975 * n) - 1], 3)]


def effect(diffs: list[float]) -> dict:
    n = len(diffs)
    sd = statistics.stdev(diffs) if n > 1 else 0.0
    dz = statistics.mean(diffs) / sd if sd else 0.0
    g = dz * (1 - 3 / (4 * (n - 1) - 1)) if n > 2 else dz              # Hedges' correction for a paired design
    return {"n": n, "mean_difference": round(statistics.mean(diffs), 3), "sd": round(sd, 3), "effect_size_g": round(g, 3), "ci95": bootstrap_ci(diffs) if n > 1 else [None, None], "p_sign_flip": sign_flip_p(diffs)}


def learning(rows: list[dict]) -> dict:
    by: dict[str, dict[int, dict]] = {}
    for r in rows:
        by.setdefault(r["participant"], {})[int(r["session"])] = r
    both = {p: s for p, s in by.items() if 1 in s and 2 in s}
    out = {"participants": len(by), "complete": len(both), "measures": {}}
    spec = {"quiz_score": lambda s: num(s[2]["quiz_after"]) - num(s[1]["quiz_before"]), "critical_items": lambda s: num(s[2]["critical_items"]) - num(s[1]["critical_items"]),
            "correct_end_state": lambda s: num(s[2]["correct_end_state"]) - num(s[1]["correct_end_state"]), "seconds_to_decision": lambda s: num(s[2]["seconds_to_decision"]) - num(s[1]["seconds_to_decision"])}
    for name, f in spec.items():
        diffs = [f(s) for s in both.values()]
        if diffs:
            out["measures"][name] = effect(diffs)
    out["warnings"] = ([f"only {len(both)} participants played both sessions (at least {MIN_N} wanted): the intervals are wide and the tests weak"] if len(both) < MIN_N else [])
    return out


def ab(rows: list[dict]) -> dict:
    by: dict[str, dict] = {}
    for r in rows:
        by.setdefault(r["participant"], {"order": r["order"]})[r["mode"]] = r
    both = {p: v for p, v in by.items() if "menu" in v and "ai" in v}
    out = {"participants": len(by), "complete": len(both), "measures": {}, "order_effect": {}}
    for name, col in (("realism", "realism"), ("task_success", "task_success"), ("seconds", "seconds")):
        diffs = [num(v["ai"][col]) - num(v["menu"][col]) for v in both.values()]
        if diffs:
            out["measures"][name] = effect(diffs)
            first = [num(v["ai"][col]) - num(v["menu"][col]) for v in both.values() if v["order"] == "ai_first"]
            second = [num(v["ai"][col]) - num(v["menu"][col]) for v in both.values() if v["order"] == "menu_first"]
            if first and second:
                out["order_effect"][name] = round(statistics.mean(first) - statistics.mean(second), 3)          # the AI-minus-menu difference when AI came first, minus when it came second
    ai = [v["ai"] for v in both.values()]
    out["ai_totals"] = {"validator_rejections": int(sum(num(r["rejections"]) for r in ai)), "fallbacks": int(sum(num(r["fallbacks"]) for r in ai)), "safety_issues": int(sum(num(r["safety_issues"]) for r in ai))}
    real = out["measures"].get("realism")
    if out["ai_totals"]["safety_issues"] > 0:
        out["decision"] = "AI dialogue stays OFF by default: a safety issue was found in the audit log"
    elif real and real["mean_difference"] > 0 and real["p_sign_flip"] < 0.05:
        out["decision"] = "realism is higher with AI dialogue and no safety issue was found: it may be switched on by default, with the gate's other suites still passing"
    else:
        out["decision"] = "no clear benefit shown: the AI dialogue is not switched on by default on this evidence"
    out["warnings"] = ([f"only {len(both)} participants played both modes (at least {MIN_N} wanted)"] if len(both) < MIN_N else []) + (["the novelty of the AI mode can inflate its realism rating: look at the order effect"] if out["order_effect"] else [])
    return out


LIMITS = {
    "learning": ["There is no control group: a gain may come from the quiz, from familiarity with the app or from the second case rather than from the practice.",
                 "Matched but different cases are used so that learning the specific case is not mistaken for learning the skill; the matching is by design, not proved.",
                 "Participants are volunteers and few; the intervals and the permutation tests say how weak the evidence is."],
    "ab": ["The novelty of free-text dialogue can inflate perceived realism; the counterbalanced order and a second session reduce it, and the order effect above shows how much remains.",
           "A safety issue in the audit log keeps the feature off whatever the realism rating says.",
           "Few participants: the tests are weak and a missing difference is not proof that there is none."],
}


def page(kind: str, res: dict, simulated: bool) -> str:
    title = {"learning": "Learning-effect study (EXP-EVAL-3)", "ab": "AI dialogue A/B (EXP-EVAL-4)"}[kind]
    lines = ([f"> **SIMULATED DATA: this page shows what the analysis does; it is not a result about people.**", ""] if simulated else []) + [f"# {title}", "", f"Participants {res['participants']}, with both sessions or modes {res['complete']}.", "",
                                                                                                                                      "| Measure | n | Mean difference | 95% interval | Effect size (g) | p (sign-flip) |", "|---|---|---|---|---|---|"]
    for k, v in res["measures"].items():
        lines.append(f"| {k} | {v['n']} | {v['mean_difference']} | {v['ci95'][0]} to {v['ci95'][1]} | {v['effect_size_g']} | {v['p_sign_flip']} |")
    if kind == "ab":
        lines += ["", f"AI mode totals: {res['ai_totals']}. Order effect (AI minus menu, AI first minus AI second): {res['order_effect']}.", "", f"**Decision:** {res['decision']}."]
    lines += ["", "## Warnings", ""] + [f"- {w}" for w in res["warnings"]] + (["- none"] if not res["warnings"] else []) + ["", "## Limitations", ""] + [f"- {x}" for x in LIMITS[kind]]
    return "\n".join(lines) + "\n"


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["learning", "ab"])
    ap.add_argument("--csv", type=Path, required=True)
    ap.add_argument("--out", type=Path)
    a = ap.parse_args(argv)
    rows = read(a.csv)
    simulated = all(r["participant"].upper().startswith("SIM") for r in rows)
    res = (learning if a.kind == "learning" else ab)(rows)
    text = page(a.kind, res, simulated)
    if a.out:
        if simulated and RESULTS in a.out.resolve().parents:
            raise SystemExit("REFUSED: simulated participants (ids starting SIM) cannot be written into the results folder")
        a.out.parent.mkdir(parents=True, exist_ok=True)
        a.out.write_text(text, encoding="utf-8", newline="\n")
    print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
