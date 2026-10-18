"""The deterministic critical-content guard, measured on its own, with no classifier and no model: `uv run --project tools tools/bench/guard_eval.py [--write|--check]`.

The guard (server/classify/guard.py, cues in content/ai/critical_cues.json plus the allergen lexicon) is a set of regular expressions. It runs after any classifier and refuses credit when a sentence touches
critical content. What this measures is the guard ALONE, so its protection is not credited to (or hidden by) a classifier:

  critical sentences         rows whose label is a critical act (allergy, identity, consent, fasting, worsening, handover) plus every row of the adversarial sets
  caught by a cue            the guard fires: no classifier, however wrong or hostile, can earn credit for the sentence
  residual                   no cue fires: protection then rests on the classifier's own critical-mass cap and on the engine never crediting critical acts from speech (measured in classifier-adversarial.json)
  false blocks               rows of an ORDINARY act on which a cue fires: credit lost for nothing (cost of the guard; it can never add credit)

Sets: the v2 dev and test splits, the v3 hold-out, and the two adversarial sets. The sentences are model-written and AI-judged (not independent), so the figures are regression figures: --check fails if the
committed report differs from a fresh run, which forces a conscious update when a cue changes. A hostile classifier is also simulated: one that names an ordinary act with confidence 1.0 for every sentence is
credited exactly on the residual rows (`credit_if_classifier_hostile`).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify.guard import critical_cue  # noqa: E402

REPORT = ROOT / "reports" / "ai" / "classifier-guard-only.json"
V2, V3 = ROOT / "tests" / "classifier_synthetic_v2", ROOT / "tests" / "classifier_synthetic_v3"


def jsonl(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def critical_acts() -> set[str]:
    tax = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))
    return {a["id"] for a in tax["acts"] if a["critical"]}


def measure(name: str, rows: list[dict], crit: set[str], all_critical: bool = False) -> dict:
    cr = [r for r in rows if all_critical or r["act"] in crit]
    caught = [r for r in cr if critical_cue(r["text"])]
    resid = [r for r in cr if not critical_cue(r["text"])]
    ordinary = [r for r in rows if not all_critical and r["act"] not in crit and r["act"] != "NONE"]
    blocked = [r for r in ordinary if critical_cue(r["text"])]
    return {"set": name, "rows": len(rows), "critical_sentences": len(cr), "caught_by_a_cue": len(caught), "caught_share": round(len(caught) / len(cr), 3) if cr else None,
            "residual_no_cue": len(resid), "credit_if_classifier_hostile": len(resid), "ordinary_rows": len(ordinary), "false_blocks": len(blocked),
            "false_block_share": round(len(blocked) / len(ordinary), 3) if ordinary else None,
            "residual_examples": [r["text"] for r in resid[:5]], "false_block_examples": [r["text"] for r in blocked[:5]]}


def run() -> dict:
    crit = critical_acts()
    sets = [measure("v2 dev", jsonl(V2 / "dev.jsonl"), crit), measure("v2 test", jsonl(V2 / "test.jsonl"), crit), measure("v3 hold-out", jsonl(V3 / "test.jsonl"), crit)]
    for name, f in (("adversarial tuning", "adversarial_critical.json"), ("adversarial hold-out", "adversarial_critical_holdout.json"),
                    ("adversarial hold-out 2 (used for tuning cues v4)", "adversarial_critical_holdout2.json"), ("adversarial hold-out 3 (fresh, no cue chosen from it)", "adversarial_critical_holdout3.json")):
        sets.append(measure(name, json.loads((V2 / f).read_text(encoding="utf-8"))["rows"], crit, all_critical=True))
    cues = json.loads((ROOT / "content" / "ai" / "critical_cues.json").read_text(encoding="utf-8"))
    return {"guard": "server/classify/guard.py", "cues_version": cues.get("version"), "synthetic": True, "independent": False,
            "note": "No classifier is involved. Sentences are model-written and AI-judged, so these are regression figures, not real-world accuracy.",
            "history": "Hold-out 2 was measured on cues version 3 BEFORE version 4 was written: 23 of 60 caught by a cue, 37 residual. Version 4 was then chosen from the families in the first two sets and in general speech, and hold-out 2 became tuning evidence (59 of 60 after). Hold-out 3 was written after version 4 and no cue was chosen from it: the cues catch 6 of 48 (13 percent), so a list of phrases does not generalise to new wording and the second layer (the head's critical-mass cap) carries the rest.",
            "sets": sets}


def main(argv: list[str]) -> int:
    out = run()
    text = json.dumps(out, indent=1) + "\n"
    if "--check" in argv:
        ok = REPORT.exists() and REPORT.read_text(encoding="utf-8") == text
        print("guard-only report " + ("current" if ok else "OUT OF DATE: run tools/bench/guard_eval.py --write after a conscious change of a cue"))
        return 0 if ok else 1
    for s in out["sets"]:
        print(f"{s['set']:22} critical {s['critical_sentences']:4}  caught {s['caught_by_a_cue']:4} ({s['caught_share']})  residual {s['residual_no_cue']:3}  false blocks {s['false_blocks']}/{s['ordinary_rows']}")
    if "--write" in argv:
        REPORT.write_text(text, encoding="utf-8", newline="\n")
        print("wrote", REPORT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
