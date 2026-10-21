"""Reports the controlled partial-credit evaluation (tests/synthetic_dev_v1/partial_credit_v1.jsonl, 32 AI-written items, synthetic DEVELOPMENT evidence): per item the model's grades and the ENGINE's communication credit (CLI speech-score) against the expected band fixed before the run.
Usage: server/.venv/Scripts/python.exe tools/synth_dev/partial_credit_report.py <run.json> [--set partial|partial2] [--write <summary.json>]"""
from __future__ import annotations

import collections
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(ROOT))
import engine_score as ES  # noqa: E402
from evaluator_run import context_of  # noqa: E402

SETS = {"partial": ROOT / "tests" / "synthetic_dev_v1" / "partial_credit_v1.jsonl", "partial2": ROOT / "tests" / "synthetic_dev_v1" / "partial_credit_v2.jsonl", "heldout_partial": ROOT / "tests" / "synthetic_heldout_v1" / "partial_credit.jsonl"}


def main(argv):
    d = json.loads(Path(argv[0]).read_text(encoding="utf-8"))
    which = argv[argv.index("--set") + 1] if "--set" in argv else "partial"
    rows = [json.loads(l) for l in SETS[which].read_text(encoding="utf-8").splitlines() if l.strip()]
    items = []
    for r in rows:
        ev = d["results"][r["id"]]["evaluation"]
        c = context_of(r["spec"])
        acts = [] if not ev else [{"act": a["objective"], "assertion": a["assertion"], "actor": a["actor"], "form": a["form"], "relevance": a["relevance"], "correctness": a["correctness"], "completeness": a["completeness"], "span": a["evidence"] or a["objective"]} for a in ev["acts"]]
        items.append({"id": r["id"], "steps": c["steps"], "patient_question_pending": c["patient_question_pending"], "readback_pending": c["readback_pending"], "acts": acts})
    eng = {x["id"]: x for x in ES.score(items)}
    out, ok = [], collections.Counter()
    for r in rows:
        ev = d["results"][r["id"]]["evaluation"]
        own = next((a for a in (ev["acts"] if ev else []) if a["objective"] == r["objective"]), None)
        total = eng[r["id"]]["communication_total"]
        lo, hi = r["expected_score"]
        inside = lo <= total <= hi if r["band"] not in ("full",) else total >= lo
        ok[r["band"]] += inside
        out.append({"id": r["id"], "objective": r["objective"], "band": r["band"], "expected": r["expected_score"], "score": total, "in_band": inside, "assertion": own["assertion"] if own else None, "grades": [own["relevance"], own["correctness"], own["completeness"]] if own else None,
                    "other_objectives": [a["objective"] for a in (ev["acts"] if ev else []) if a["objective"] != r["objective"]], "utterance": r["utterance"][:70]})
    by_band = {b: [x["score"] for x in out if x["band"] == b] for b in ("full", "partial", "wrong", "irrelevant")}
    summary = {"n": len(out), "in_expected_band": dict(ok), "per_band_scores": by_band, "per_band_mean": {b: round(sum(v) / len(v), 3) for b, v in by_band.items()},
               "monotonic_full_over_partial_over_wrong": all(min(by_band["full"]) > max(by_band["partial"]) > 0 and max(by_band["wrong"]) <= min(by_band["partial"]) for _ in [0]) if by_band["partial"] else None, "items": out,
               "tokens": d["tokens"], "prompt_hash": d["prompt_hash"]}
    if "--write" in argv:
        Path(argv[argv.index("--write") + 1]).write_text(json.dumps(summary, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: v for k, v in summary.items() if k != "items"}, ensure_ascii=False))
    for x in out:
        print(f"{x['id']} {x['objective'][:18]:18} {x['band']:10} score={x['score']:<6} {'ok' if x['in_band'] else 'OUT'} asr={x['assertion']} g={x['grades']} extra={x['other_objectives']} | {x['utterance']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
