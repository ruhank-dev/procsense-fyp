"""What the assertion-layer fixes of 20 October 2026 changed, sentence by sentence (ADR-0022 follow-up): `tools/.venv/Scripts/python.exe tools/bench/assertion_diff.py [--write]`.

The layer before the fixes is read from git (commit 2f9aa0a, `git show 2f9aa0a:server/classify/assertion.py`) and loaded as a separate module, so the comparison is between the two real files, not a copy. For every sentence of: the benchmark's legitimate rows (all three splits),
the benchmark's non-performed rows, the AI-judged v2 ordinary rows, and the hand-written regression sentences (28 legitimate paraphrases, 84 counterexamples, 4 ambiguous fillers) it records the verdict of both layers. What matters:
  * no legitimate sentence may become non-assertive (a new false rejection), and
  * the non-performed sentences that were NOT caught before and are now.
Writes reports/ai/recall-assertion-diff.json with --write. Standard library plus git.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import subprocess
import sys
import tempfile
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import nli_zero_shot as N  # noqa: E402
from classify import assertion as NEW  # noqa: E402

BEFORE_COMMIT = "2f9aa0a"


def load_before(commit=BEFORE_COMMIT):
    text = subprocess.run(["git", "show", f"{commit}:server/classify/assertion.py"], capture_output=True, text=True, encoding="utf-8", cwd=str(ROOT), check=True).stdout
    tmp = Path(tempfile.mkdtemp()) / "assertion_before.py"
    tmp.write_text(text, encoding="utf-8")
    spec = importlib.util.spec_from_file_location("assertion_before", tmp)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def sets() -> dict[str, list[str]]:
    out: dict[str, list[str]] = {}
    for s in ("dev", "test", "test2"):
        rows = N.load_rows(s)
        out[f"bench {s}: legitimate rows"] = [r["text"] for r in rows if r["category"] in ("paraphrase", "asr_error")]
        out[f"bench {s}: non-performed rows"] = [r["text"] for r in rows if r["category"] in ("negation", "intention", "hypothetical", "reported", "actor_change", "incomplete")]
    crit = {"CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT", "SBAR_ELEMENT"}
    for sp in ("dev", "test"):
        rows = [json.loads(l) for l in (ROOT / "tests" / "classifier_synthetic_v2" / f"{sp}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        out[f"AI-judged v2 {sp}: ordinary rows (plain, recogniser-style)"] = [r["text"] for r in rows if r["act"] not in crit and r["act"] != "NONE" and r["bucket"] in ("plain", "asr_style")]
    rc = json.loads((ROOT / "tests" / "classifier_regression" / "recall_cases.json").read_text(encoding="utf-8"))["rows"]
    out["regression: legitimate paraphrases"] = [r["text"] for r in rc if r["group"] in ("legit_paraphrase", "legit_recovered_by_cap")]
    out["regression: counterexamples (denied, plan, hypothetical, reported, other speaker, broken off)"] = [r["text"] for r in rc if r["group"].startswith("counter_") and r["group"] != "counter_ambiguous"]
    for sp in ("dev", "challenge"):                    # the unsafe-speech sets (a legitimate row is a performed, non-critical sentence)
        rows = [json.loads(l) for l in (ROOT / "tests" / "safety_challenge" / f"{sp}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        out[f"safety {sp}: legitimate rows"] = [r["text"] for r in rows if r["expected"] == "credit_allowed"]
        out[f"safety {sp}: must-not-credit rows"] = [r["text"] for r in rows if r["expected"] == "no_credit"]
    return out


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--before", default=BEFORE_COMMIT, help="commit whose assertion layer is the comparison (default 2f9aa0a, the recall follow-up; 4a2ed00 for the unsafe-speech hardening)")
    ap.add_argument("--out", default="recall-assertion-diff.json")
    a = ap.parse_args(argv)
    old = load_before(a.before)
    res = {}
    for name, texts in sets().items():
        newly, lost = [], []
        flagged_old = flagged_new = 0
        for t in texts:
            o, n = old.non_assertive(t), NEW.non_assertive(t)
            flagged_old += bool(o)
            flagged_new += bool(n)
            if n and not o:
                newly.append({"text": t, "now": n})
            if o and not n:
                lost.append({"text": t, "was": o})
        res[name] = {"sentences": len(texts), "flagged_before": flagged_old, "flagged_now": flagged_new, "newly_flagged": len(newly), "no_longer_flagged": len(lost), "newly_flagged_examples": newly[:6], "no_longer_flagged_examples": lost[:6]}
    legit_new = sum(v["newly_flagged"] for k, v in res.items() if "legitimate" in k or "ordinary" in k or "paraphrases" in k)
    out = {"before": a.before, "sets": res, "legitimate_sentences_newly_flagged": legit_new}
    for k, v in res.items():
        print(f"{k}: {v['sentences']} sentences; flagged before {v['flagged_before']}, now {v['flagged_now']} (newly {v['newly_flagged']}, no longer {v['no_longer_flagged']})")
    print("legitimate sentences newly flagged:", legit_new)
    if a.write:
        (ROOT / "reports" / "ai" / a.out).write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote reports/ai/" + a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
