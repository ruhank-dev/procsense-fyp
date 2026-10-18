"""The deterministic critical-content guard measured without any classifier (tools/bench/guard_eval.py), and the adversarial sets kept frozen."""
import hashlib
import json
import sys
import types
import unittest
from unittest import mock
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "server"))
import guard_eval as G  # noqa: E402
from classify.classifier import ActClassifier  # noqa: E402
from classify.guard import critical_cue  # noqa: E402

V2 = ROOT / "tests" / "classifier_synthetic_v2"


class Report(unittest.TestCase):
    def test_the_committed_report_is_what_a_fresh_run_gives(self):
        self.assertEqual(G.REPORT.read_text(encoding="utf-8"), json.dumps(G.run(), indent=1) + "\n")

    def test_adversarial_sets_are_frozen(self):
        m = json.loads((V2 / "adversarial_manifest.json").read_text(encoding="utf-8"))["files"]
        self.assertEqual(len(m), 4)
        for name, digest in m.items():
            self.assertEqual(hashlib.sha256((V2 / name).read_bytes()).hexdigest(), digest, f"{name} was edited after it was scored")

    def test_no_sentence_is_in_two_adversarial_sets(self):
        seen = {}
        for name in json.loads((V2 / "adversarial_manifest.json").read_text(encoding="utf-8"))["files"]:
            for r in json.loads((V2 / name).read_text(encoding="utf-8"))["rows"]:
                self.assertNotIn(r["text"], seen, f"{r['text']!r} in {name} and {seen.get(r['text'])}")
                seen[r["text"]] = name

    def test_the_guard_costs_little_on_ordinary_speech(self):
        for s in G.run()["sets"]:
            if s["ordinary_rows"]:
                self.assertLess(s["false_block_share"], 0.02, s["set"])

    def test_the_known_gap_is_recorded_not_hidden(self):
        fresh = [s for s in G.run()["sets"] if "hold-out 3" in s["set"]][0]
        self.assertGreater(fresh["residual_no_cue"], 0)                  # the cue list alone does not cover fresh wording; the report says so


class HostileClassifier(unittest.TestCase):
    """A classifier that always answers an ordinary act with confidence 1.0 earns credit only where no cue fires: the guard is the only thing between it and credit, and it never credits a critical act."""

    def test_hostile_ordinary_act_is_credited_exactly_where_no_cue_fires(self):
        clf = ActClassifier(object(), thresholds={"per_act": {"ANSWER_QUESTION": 0.0}})
        hostile = types.SimpleNamespace(ok=True, value={"act": "ANSWER_QUESTION", "confidence": 1.0}, audit={"error": ""})
        rows = json.loads((V2 / "adversarial_critical_holdout2.json").read_text(encoding="utf-8"))["rows"]
        with mock.patch("classify.classifier.gateway.run", return_value=hostile):
            credited = [clf.classify(r["text"]).credited for r in rows]
        self.assertEqual(credited, [not critical_cue(r["text"]) for r in rows])
        self.assertLess(sum(credited), len(rows))                         # the guard did stop some

    def test_a_critical_act_is_never_credited_whatever_the_confidence(self):
        clf = ActClassifier(None, thresholds={"per_act": {a: 0.0 for a in clf_critical()}})
        for act in clf_critical():
            self.assertFalse(clf.credit_allowed(act, 1.0), act)
        self.assertFalse(clf.credit_allowed("NONE", 1.0))


def clf_critical():
    return sorted(ActClassifier(None).critical)


if __name__ == "__main__":
    unittest.main()
