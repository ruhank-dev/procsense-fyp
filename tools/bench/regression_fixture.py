"""Builds the permanent classifier safety regression suite: `uv run --project tools tools/bench/regression_fixture.py [--ollama URL]` (needs Ollama with all-minilm:22m; the CI test needs no service).

tests/classifier_regression/cases.json holds sentences with the outcome the credit path must keep, and the embedding of each sentence (all-minilm:22m, 384 numbers, rounded to 5 places) so the test runs the real
heads without a model service. Groups:

  must_not_credit_failure   the three sentences the production path credited under an ordinary act in the 18 October 2026 fresh adversarial run (adversarial hold-out 3: ADD014, ADD029, ADD048)
  must_not_credit_v5         the two sentences the frozen challenge set v5 showed the 4th-session gate crediting (a fasting question worded without "fasting", and "I wanted to tell you ... but there is no time")
  must_not_credit_critical  one evasive critical sentence per kind from the tuning adversarial sets and hold-out 3 (credit must stay refused whatever the act)
  must_not_credit_nonassertive  denials, hypotheticals, reports and sentences that break off (rubric rule 10)
  may_credit                sentences the ensemble credited under the right act when this file was made, taken from the v3 test split: the guard must not become a blanket refusal (characterisation, not a claim)

The file is regenerated only deliberately; a change to a head or a guard that breaks a case is a decision to take in the open, not a number to refresh.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify.assertion import non_assertive  # noqa: E402
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402

V2, V3, V4 = ROOT / "tests" / "classifier_synthetic_v2", ROOT / "tests" / "classifier_synthetic_v3", ROOT / "tests" / "classifier_challenge_v4"
OUT = ROOT / "tests" / "classifier_regression" / "cases.json"
FAILURES = ["ADD014", "ADD029", "ADD048"]
V5_FAILURES = ["When was the last time you had anything in your stomach?", "I wanted to tell you what happens next but there is no time."]
NON_ASSERTIVE = ["I haven't asked about allergies yet.", "I'm not going to explain the procedure now, we'll do it later.", "If I were to explain the operation, I'd start with the incision.",
                 "The nurse said she would reassure you later.", "I never asked about allergies, that was the other nurse.", "Didn't check the wristband yet. Later.",
                 "So you'd like the gas repeated in an hour, and then I should...", "Sorry, when you say it throbs, do you mean..."]


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    a = ap.parse_args(argv)
    emb = OllamaEmbedder(a.ollama)
    clf = EnsembleActClassifier(emb)
    h3 = json.loads((V2 / "adversarial_critical_holdout3.json").read_text(encoding="utf-8"))["rows"]
    tuning = json.loads((V2 / "adversarial_critical.json").read_text(encoding="utf-8"))["rows"]
    cases = [{"group": "must_not_credit_failure", "id": r["id"], "text": r["text"], "kind": r["kind"]} for r in h3 if r["id"] in FAILURES]
    cases += [{"group": "must_not_credit_v5", "id": f"V5F{i + 1}", "text": t, "kind": "v5_failure"} for i, t in enumerate(V5_FAILURES)]
    taken: dict[str, int] = {}
    for r in tuning + h3:
        if r["id"] not in FAILURES and taken.get(r["kind"], 0) < 3:
            taken[r["kind"]] = taken.get(r["kind"], 0) + 1
            cases.append({"group": "must_not_credit_critical", "id": r["id"], "text": r["text"], "kind": r["kind"]})
    cases += [{"group": "must_not_credit_nonassertive", "id": f"NA{i + 1:02d}", "text": t, "kind": "non_assertive"} for i, t in enumerate(NON_ASSERTIVE)]
    v3 = [json.loads(l) for l in (V3 / "test.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    per_act: dict[str, int] = {}
    for r in v3:
        if r["act"] in clf.critical or r["act"] == "NONE" or per_act.get(r["act"], 0) >= 1 or non_assertive(r["text"]):
            continue
        res = clf.classify(r["text"])
        if res.credited and res.act == r["act"]:
            per_act[r["act"]] = per_act.get(r["act"], 0) + 1
            cases.append({"group": "may_credit", "id": r["id"], "text": r["text"], "act": r["act"], "kind": "ordinary"})
    for c in cases:
        v = emb(c["text"])
        if v is None:
            raise SystemExit("the embedding service did not answer")
        c["embedding"] = [round(x, 5) for x in v]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({"synthetic": True, "independent": False, "embedding_model": "all-minilm:22m", "note": __doc__.strip().splitlines()[0], "cases": cases}, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(len(cases), "cases;", {g: sum(c["group"] == g for c in cases) for g in {c["group"] for c in cases}})
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
