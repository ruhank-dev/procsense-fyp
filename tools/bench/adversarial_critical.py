"""Adversarial check of the speech classifier on critical content (task: critical-speech misclassification): `uv run --project tools tools/bench/adversarial_critical.py [--ollama URL] [--write]`.

tests/classifier_synthetic_v2/adversarial_critical.json holds sentences that touch an allergy, an identity check, consent, the fasting state, a worsening sign or a structured handover in words that avoid the
obvious ones (written by the builder before any v2 benchmark, synthetic, not independent). The rule the classifier must keep: **speech never earns credit for critical content**. Each sentence is run through the
shipped embedding classifier (server/classify/embedding.py: the head in content/assessment/act_head.json, the regular-expression guard, and the critical-mass guard when the head carries one) and reported as:

  right_critical_act   the classifier named the critical act (recorded, never credited)
  other_not_credited   it named another act but credit was refused (a guard, a threshold or NONE)
  credited_as_ordinary it named an ordinary act and credit was given: the failure this check exists to count (must be 0)

The result is a regression figure, not an accuracy claim: the set was used to tune the critical-mass guard once (the report says which version of the head it saw). --write saves reports/ai/classifier-adversarial.json.
Needs a running Ollama with all-minilm:22m for the real run; the unit tests use a stand-in embedder.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
SETS = {"tuning": ROOT / "tests" / "classifier_synthetic_v2" / "adversarial_critical.json", "holdout": ROOT / "tests" / "classifier_synthetic_v2" / "adversarial_critical_holdout.json",
        "holdout2": ROOT / "tests" / "classifier_synthetic_v2" / "adversarial_critical_holdout2.json",
        "holdout3": ROOT / "tests" / "classifier_synthetic_v2" / "adversarial_critical_holdout3.json"}
REPORT = ROOT / "reports" / "ai" / "classifier-adversarial.json"


def evaluate(classifier, which: str = "tuning") -> dict:
    rows = json.loads(SETS[which].read_text(encoding="utf-8"))["rows"]
    counts: dict[str, dict] = {}
    credited = []
    unavailable = 0
    for r in rows:
        res = classifier.classify(r["text"])
        if res.source == "unavailable":
            unavailable += 1
            continue
        c = counts.setdefault(r["kind"], {"rows": 0, "right_critical_act": 0, "other_not_credited": 0, "credited_as_ordinary": 0})
        c["rows"] += 1
        if res.act in classifier.critical:
            c["right_critical_act"] += 1
        elif res.credited:
            c["credited_as_ordinary"] += 1
            credited.append({"id": r["id"], "text": r["text"], "act": res.act, "confidence": round(res.confidence, 3)})
        else:
            c["other_not_credited"] += 1
    return {"rows": sum(c["rows"] for c in counts.values()), "unavailable": unavailable, "by_kind": counts, "credited_as_ordinary": credited,
            "credited_as_ordinary_count": len(credited), "head_critical_mass_max": classifier.head.get("critical_mass_max"), "head_trained_on_rows": classifier.head.get("trained_on_rows")}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--set", choices=sorted(SETS), default="tuning", help="tuning: the set the guard cues were chosen from; holdout: the set written before the change and never used to choose a cue; holdout2: a third set written afterwards (18 October, second session), looked at only through its scores")
    ap.add_argument("--head", default="", help="a head file to test instead of content/assessment/act_head.json (for example a head just exported by the benchmark)")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--out", default="", help="write to this file instead of reports/ai/classifier-adversarial.json (an object keyed by the set name is merged into it)")
    a = ap.parse_args(argv)
    from classify.embedding import EmbeddingActClassifier, OllamaEmbedder, load_head
    head = load_head(Path(a.head)) if a.head else None
    out = evaluate(EmbeddingActClassifier(OllamaEmbedder(a.ollama), head), a.set)
    out["head_file"] = a.head or "content/assessment/act_head.json"
    out["set"] = a.set
    out["synthetic"], out["independent"] = True, False
    print(json.dumps({k: v for k, v in out.items() if k != "credited_as_ordinary"}, indent=1))
    for c in out["credited_as_ordinary"]:
        print("  CREDITED:", c)
    if a.write and a.out:
        target = Path(a.out)
        merged = json.loads(target.read_text(encoding="utf-8")) if target.exists() else {}
        merged[a.set] = out
        target.write_text(json.dumps(merged, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", a.out)
    elif a.write:
        REPORT.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", REPORT.relative_to(ROOT).as_posix())
    return 1 if out["credited_as_ordinary_count"] else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
