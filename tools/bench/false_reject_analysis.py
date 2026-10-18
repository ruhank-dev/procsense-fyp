"""Why the credit gate refuses a legitimate sentence: `uv run --project tools tools/bench/false_reject_analysis.py [--write]` (needs a running Ollama for the embeddings).

For every ORDINARY sentence (true act not critical, not NONE) of the DEVELOPMENT sets (v2 dev, v2 test, v3 test, challenge v4 and v5; the frozen v6 is never read here) the first reason the production gate gives no credit is named, in the order the gate applies its rules:

  credited              the gate credited it (right act or wrong act is counted apart)
  head_names_critical   the hybrid head's most probable act is critical or NONE
  head_wrong_act        the hybrid head names a different ordinary act, so the credit would have been for the wrong act
  below_threshold       the right act, but under the act's own threshold
  critical_mass         the right act, but the head's probability of a critical act is at or over the cap
  cue_guard             a critical-content cue matched (the sentence is about an allergy, identity, consent, fasting, worsening or handover)
  second_reader         the hybrid head would credit, the TF-IDF head names another act
  non_assertive         the sentence is a denial, hypothetical, report or breaks off

Which of these are recoverable without weakening a guard is the point of the table: `below_threshold` and `second_reader` with the right act are the only ones a better head can fix; the guards (cue, mass, non-assertive) are removed credit by design. Synthetic, AI-judged, not independent. Writes
reports/ai/classifier-false-rejects.json.
"""
from __future__ import annotations

import argparse
import collections
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.assertion import non_assertive  # noqa: E402
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from classify.guard import critical_cue  # noqa: E402
import safety_gate as G  # noqa: E402

REPORT = ROOT / "reports" / "ai" / "classifier-false-rejects.json"


def reason(clf: EnsembleActClassifier, vec: list[float], text: str, truth: str) -> str:
    h = clf.embedding
    probs = h.probabilities_of(vec, text)
    act = max(probs, key=probs.get)
    if act in clf.critical or act == "NONE":
        return "head_names_critical"
    if act != truth:
        return "head_wrong_act"
    t = h.head["thresholds"].get(act)
    cap = h.head.get("critical_mass_max")
    if t is None or probs[act] < t:
        return "below_threshold"
    if cap is not None and h.critical_mass(probs) >= cap:
        return "critical_mass"
    if critical_cue(text):
        return "cue_guard"
    if clf.tfidf.classify(text).act != act:
        return "second_reader"
    if non_assertive(text):
        return "non_assertive"
    return "credited"


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    emb = OllamaEmbedder(a.ollama)
    clf = EnsembleActClassifier(emb)
    sets = {k: v for k, v in G.sets(False).items() if k in ("v2_dev", "v2_test", "v3_test")}
    v4 = json.loads((ROOT / "tests" / "classifier_challenge_v4" / "challenge.json").read_text(encoding="utf-8"))["rows"]
    v5 = json.loads((ROOT / "tests" / "classifier_challenge_v5" / "challenge.json").read_text(encoding="utf-8"))["rows"]
    sets["challenge_v4"] = [r for r in v4 if r.get("bucket") != "non_assertive" and r["act"] not in clf.critical]
    sets["challenge_v5_development"] = [r for r in v5 if r.get("bucket") != "non_assertive" and r["act"] not in clf.critical]
    out = {"synthetic": True, "independent": False, "sets": {}, "all": {}}
    total = collections.Counter()
    by_act: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)
    for name, rows in sets.items():
        c = collections.Counter()
        for r in rows:
            if r["act"] in clf.critical or r["act"] == "NONE":
                continue
            vec = emb(r["text"])
            if vec is None:
                raise SystemExit(f"REFUSED: no embedding for a row of {name}; nothing written")
            why = reason(clf, vec, r["text"], r["act"])
            c[why] += 1
            by_act[r["act"]][why] += 1
        out["sets"][name] = {"rows": sum(c.values()), "reasons": dict(c)}
        total += c
    n = sum(total.values())
    out["all"] = {"rows": n, "reasons": dict(total), "shares": {k: round(v / n, 3) for k, v in total.items()}}
    out["by_act"] = {k: dict(v) for k, v in sorted(by_act.items())}
    print(json.dumps({"rows": n, "reasons": dict(total.most_common())}, indent=1))
    if a.write:
        REPORT.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", REPORT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
