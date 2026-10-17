"""Novelty over simulated trainees (p1-live-scenario-generation-s09): `python tools/generation/simulate_novelty.py [trainees] [sessions]`.

Each simulated trainee plays `sessions` generated cases in a row, alternating the two scenario families, with the fingerprint levels of their earlier cases as history. Prints the novelty rate (share of
sessions meeting the threshold), low-novelty sessions served, fallbacks and the mean distance to the nearest earlier case. The spec target is a novelty rate of 0.95 or more (live_generation_spec.md section 6).
CI runs 25 trainees of 10 sessions; the 1,000-trainee run is local (about 20 minutes) and its numbers are in docs/research/novelty_simulation.md.
"""
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ci"))
from test_generation_novelty import simulate  # noqa: E402

if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 1000
    k = int(sys.argv[2]) if len(sys.argv) > 2 else 12
    t0 = time.time()
    out = simulate(n, k)
    out.update(trainees=n, sessions_each=k, seconds=round(time.time() - t0))
    print(json.dumps(out, indent=2))
