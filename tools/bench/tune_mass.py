"""Tunes the critical-mass guard of an embedding head on the v2 DEV split only: `uv run --project tools tools/bench/tune_mass.py --head artifacts/ai-bench/act_head_v2.json --out content/assessment/act_head.json`.

The head gives a probability for each of the 20 acts. The guard added here refuses ordinary credit when the head thinks a sentence might be a CRITICAL act: credit needs the summed probability of the critical acts
(`critical_mass`) to stay below `critical_mass_max`. The number is chosen on the frozen v2 dev sentences (315 of the 420 are critical acts said in different styles) as the largest value that keeps, on dev,

  1. no critical sentence credited under an ordinary act (the benchmark finding this guard exists for), and
  2. a credited precision of at least 0.8,

and among those the most credited sentences. Nothing here reads the v2 test split, the v3 hold-out or the adversarial sentences. The head's weights and per-act thresholds are copied unchanged; only
`critical_mass_max` and its provenance are added. The guard only ever removes credit, so it cannot make anything easier to pass.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for sub in ("server", "tools/bench", "tools/ai"):
    sys.path.insert(0, str(ROOT / sub))
import bench_classifier as BC  # noqa: E402
from classify.embedding import EmbeddingActClassifier  # noqa: E402
from classify.guard import critical_cue  # noqa: E402

GRID = [0.01, 0.02, 0.03, 0.05, 0.08, 0.1, 0.15, 0.2, 0.3, 0.4, 0.5, 1.01]         # 1.01 = no guard


def dev_rows() -> list[dict]:
    manifest = json.loads((BC.SYNTH2 / "manifest.json").read_text(encoding="utf-8"))
    import hashlib
    for name, digest in manifest["files"].items():
        if hashlib.sha256((BC.SYNTH2 / name).read_bytes()).hexdigest() != digest:
            raise SystemExit(f"REFUSED: {name} does not match the frozen hash")
    return BC.rows_of(BC.SYNTH2 / "dev.jsonl")


def evaluate(clf: EmbeddingActClassifier, rows: list[dict], vectors: list[list[float]], tau: float) -> dict:
    credited = wrong = misrouted = 0
    for r, v in zip(rows, vectors):
        p = clf.probabilities(v)
        act = max(p, key=p.get)
        mass = sum(p[c] for c in clf.critical)
        t = clf.head["thresholds"].get(act)
        ok = act not in clf.critical and act != "NONE" and t is not None and p[act] >= t and not critical_cue(r["text"]) and mass < tau
        if ok:
            credited += 1
            wrong += act != r["act"]
            misrouted += r["act"] in clf.critical
    return {"credited": credited, "precision": round((credited - wrong) / credited, 3) if credited else None, "critical_said_but_credited_as_other_act": misrouted}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--head", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    a = ap.parse_args(argv)
    head = json.loads(Path(a.head).read_text(encoding="utf-8"))
    clf = EmbeddingActClassifier(None, dict(head, _critical={c for c in head["classes"] if c in json.loads((ROOT / "content/assessment/mapping.json").read_text(encoding="utf-8"))["critical_acts"]}))
    rows = dev_rows()
    vectors = BC.embed([r["text"] for r in rows], head["embedding_model"], a.ollama)
    table = {tau: evaluate(clf, rows, vectors, tau) for tau in GRID}
    ok = [t for t, m in table.items() if m["critical_said_but_credited_as_other_act"] == 0 and (m["precision"] or 0) >= 0.8]
    chosen = max(ok, key=lambda t: table[t]["credited"]) if ok else min(table, key=lambda t: (table[t]["critical_said_but_credited_as_other_act"], -table[t]["credited"]))
    print(json.dumps({"grid": {str(k): v for k, v in table.items()}, "chosen": chosen, "met_both_conditions": bool(ok)}, indent=1))
    head["critical_mass_max"] = chosen if chosen <= 1 else None
    head["critical_mass_tuning"] = {"split": "synthetic v2 dev only", "rows": len(rows), "grid": {str(k): v for k, v in table.items()}, "chosen": chosen, "conditions": "no critical sentence credited under an ordinary act on dev, credited precision at least 0.8, then the most credited",
                                    "met_both_conditions": bool(ok)}
    Path(a.out).write_text(json.dumps(head, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("wrote", a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
