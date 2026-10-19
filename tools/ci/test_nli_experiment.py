"""The zero-shot NLI experiment (tools/bench/nli_zero_shot.py, nli_baseline.py, nli_compare.py): the parts that need no model are tested here, so the experiment cannot drift without notice. One hypothesis per label of the taxonomy and
none changed except NONE (documented in nli_config.json); single-label mode fixed by the ontology; scores are a softmax and nothing more; the ground-truth rule; the metrics on a toy case; the thresholds are computed from the dev rows handed in and nothing else; the gate never credits a critical label or NONE.
The model itself (torch, transformers, weights) lives in an isolated environment and is not needed for CI."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import nli_compare as NC  # noqa: E402
import nli_zero_shot as NZ  # noqa: E402


class Setup(unittest.TestCase):
    def test_one_hypothesis_per_label_and_only_none_is_reworded(self):
        h = NZ.hypotheses()
        tax = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]
        self.assertEqual(list(h), [a["id"] for a in tax])
        self.assertEqual(len(h), 20)
        for a in tax:
            if a["id"] != "NONE":
                self.assertTrue(h[a["id"]].startswith("The speaker "), a["id"])
                self.assertIn(a["definition"].rstrip(".")[1:], h[a["id"]])                # the definition is used verbatim apart from the first letter
        self.assertEqual(h["NONE"], NZ.load_config()["hypothesis_overrides"]["NONE"])
        self.assertEqual(NZ.load_config()["mode"], "single_label")
        self.assertEqual(NZ.load_config()["model"], "facebook/bart-large-mnli")

    def test_the_scores_are_a_softmax_and_the_frozen_rows_are_checked_by_hash(self):
        s = NZ.softmax([2.0, 1.0, -1.0])
        self.assertAlmostEqual(sum(s), 1.0)
        self.assertEqual(s.index(max(s)), 0)
        self.assertEqual(len(NZ.load_rows("dev")), 488)


class Metrics(unittest.TestCase):
    def test_truth_rule_and_metrics_on_a_toy_case(self):
        self.assertEqual(NC.truth({"status": "actual", "act": "REASSURE"}), "REASSURE")
        self.assertEqual(NC.truth({"status": "none", "act": "NONE"}), "NONE")
        self.assertEqual(NC.truth({"status": "denied", "act": "REASSURE"}), "NONE")                 # the derived label of a sentence that does not perform the act
        m = NC.prf([("A", "A"), ("A", "B"), ("B", "B"), ("B", None)])
        self.assertEqual(m["accuracy"], 0.5)
        self.assertEqual(m["per_class"]["A"]["recall"], 0.5)
        self.assertEqual(m["per_class"]["B"]["precision"], 0.5)
        self.assertEqual(NC.confusion([("A", None)]), {"A": {"ABSTAIN": 1}})

    def test_the_thresholds_come_only_from_the_rows_given_and_are_deterministic(self):
        rows = [{"id": f"r{i}", "status": "actual", "act": "REASSURE"} for i in range(10)]
        scores = {f"r{i}": {"scores": {**{l: 0.0 for l in NC.LABELS}, "REASSURE" if i < 8 else "ASK_TOPIC": 0.5 + i / 100}} for i in range(10)}
        for k in scores:
            scores[k]["scores"] = {**{l: 0.001 for l in NC.LABELS}, **scores[k]["scores"]}
        a = NC.abstention_tau(rows, scores)
        self.assertEqual(a, NC.abstention_tau(rows, scores))
        self.assertEqual(NC.credit_thresholds(rows, scores), NC.credit_thresholds(rows, scores))
        import inspect
        self.assertEqual(list(inspect.signature(NC.abstention_tau).parameters), ["dev_rows", "dev_nli"])        # no way to hand it another split by mistake

    def test_the_gate_never_credits_a_critical_label_or_none(self):
        thr = {l: 0.0 for l in NC.ORDINARY}
        for label in list(NC.CRITICAL) + ["NONE"]:
            scores = {l: 0.0 for l in NC.LABELS}
            scores[label] = 1.0
            self.assertFalse(NC.gate_nli({"text": "x"}, scores, thr, False)[0], label)
        scores = {l: 0.0 for l in NC.LABELS}
        scores["REASSURE"] = 1.0
        self.assertTrue(NC.gate_nli({"text": "You are in good hands."}, scores, thr, True)[0])
        self.assertFalse(NC.gate_nli({"text": "I haven't reassured you yet."}, scores, thr, True)[0])           # the production assertion check still applies
        self.assertTrue(NC.gate_nli({"text": "I haven't reassured you yet."}, scores, thr, False)[0])           # the thresholds-only variant has no such check: that is the point of reporting both


if __name__ == "__main__":
    unittest.main()
