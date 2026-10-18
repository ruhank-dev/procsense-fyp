"""The personalisation experiment (p1-personalisation-s03 and s05): `uv run --project tools tools/personalisation/simulate.py [--learners 200] [--sessions 12] [--seed 1] [--write]`.

Runs server/personalisation/experiment.py: the engine's Elo selection, the learner-model rules and the Thompson-sampling bandit on the same simulated learners, and prints the paired improvement of the true mastery of
each learner's five weakest behaviours with a bootstrap 95 percent interval. `--write` saves the result to docs/research/personalisation_simulation.json. A policy is adopted only if its interval is above zero against
the one it would replace; the document says which were.
"""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--learners", type=int, default=200)
    ap.add_argument("--sessions", type=int, default=12)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--study", action="store_true", help="the fair multi-family comparison with a random floor and an oracle ceiling (docs/research/personalisation_study.json); --learners is per family and seed")
    ap.add_argument("--seeds", type=int, default=3)
    a = ap.parse_args()
    from personalisation import experiment
    if a.study:
        result = experiment.study(a.learners, a.sessions, tuple(range(1, a.seeds + 1)))
        print(json.dumps(result["pooled"], indent=1), result["better_than_elo_in_every_family"])
        if a.write:
            out = ROOT / "docs" / "research" / "personalisation_study.json"
            out.write_text(json.dumps(result, indent=1) + "\n", encoding="utf-8", newline="\n")
            print("wrote", out.relative_to(ROOT))
        sys.exit(0)
    result = experiment.run(a.learners, a.sessions, a.seed)
    print(json.dumps(result, indent=2))
    if a.write:
        out = ROOT / "docs" / "research" / "personalisation_simulation.json"
        out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("wrote", out.relative_to(ROOT))
