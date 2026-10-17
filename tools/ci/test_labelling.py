"""The labelling toolkit (tools/labelling/label.py; p1-ai-infrastructure-s04): agreement is computed correctly, two different labellers are required, a disagreement needs a third person, and a set cannot be frozen
too small or without enough agreement. No real labels are used: the rows here exist only to exercise the rules."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "labelling"))
import label as L  # noqa: E402

ACTS = L.labels_for("utterance")


def sheet(labels, who):
    return [{"id": f"u{i}", "text": f"t{i}", "label": x, "labeller": who} for i, x in enumerate(labels)]


class Agreement(unittest.TestCase):
    def test_kappa_and_weighted_kappa_match_known_values(self):
        a = ["correct", "correct", "wrong", "wrong"]
        self.assertEqual(L.agreement(sheet(a, "ann"), sheet(a, "bob"), "relation", L.labels_for("relation"))["kappa"], 1.0)
        r = L.agreement(sheet(a, "ann"), sheet(["correct", "wrong", "wrong", "correct"], "bob"), "relation", L.labels_for("relation"))
        self.assertEqual((r["agreement"], r["kappa"], len(r["disagreements"])), (0.5, 0.0, 2))
        near = L.weighted_kappa(["1", "2", "3", "4", "5"], ["1", "2", "3", "4", "4"], L.labels_for("scenario"))
        far = L.weighted_kappa(["1", "2", "3", "4", "5"], ["1", "2", "3", "4", "1"], L.labels_for("scenario"))
        self.assertGreater(near, far)                                                 # a near miss costs less than a far one

    def test_one_labeller_twice_or_a_label_outside_the_set_is_a_problem(self):
        a = sheet(["correct", "wrong"], "ann")
        self.assertTrue(L.agreement(a, sheet(["correct", "wrong"], "ann"), "relation", L.labels_for("relation"))["problems"])
        self.assertTrue(L.agreement(a, sheet(["correct", "banana"], "bob"), "relation", L.labels_for("relation"))["problems"])
        self.assertTrue(L.agreement(a, sheet(["correct"], "bob"), "relation", L.labels_for("relation"))["problems"])

    def test_the_utterance_label_set_is_the_taxonomy(self):
        self.assertEqual(len(ACTS), 20)
        self.assertIn("SBAR_ELEMENT", ACTS)


class Adjudication(unittest.TestCase):
    def test_a_disagreement_is_settled_by_a_third_person_only(self):
        a, b = sheet(["ASK_TOPIC", "CLARIFY"], "ann"), sheet(["ASK_TOPIC", "REASSURE"], "bob")
        out, problems = L.adjudicate(a, b, [], ACTS)
        self.assertTrue(problems)
        for who in ("ann", "bob", ""):
            _, problems = L.adjudicate(a, b, [{"id": "u1", "label": "CLARIFY", "adjudicator": who}], ACTS)
            self.assertTrue(problems, who)
        out, problems = L.adjudicate(a, b, [{"id": "u1", "label": "CLARIFY", "adjudicator": "carol"}], ACTS)
        self.assertEqual(problems, [])
        self.assertEqual([(r["id"], r["label"], r["adjudicated"]) for r in out], [("u0", "ASK_TOPIC", "false"), ("u1", "CLARIFY", "true")])


class Freezing(unittest.TestCase):
    def rows(self, n):
        final = [{"id": f"u{i}", "label": ACTS[i % len(ACTS)], "adjudicated": "false", "adjudicator": ""} for i in range(n)]
        items = [{"id": f"u{i}", "text": f"t{i}", "group": f"g{i % 10}"} for i in range(n)]
        return final, items

    def test_too_small_or_too_little_agreement_is_refused(self):
        final, items = self.rows(50)
        with tempfile.TemporaryDirectory() as t:
            with self.assertRaises(SystemExit) as e:
                L.freeze(final, items, "utterance", Path(t), 0.8, 300, 0.6)
            self.assertIn("at least 300", str(e.exception))
            with self.assertRaises(SystemExit) as e:
                L.freeze(final, items, "utterance", Path(t), 0.4, 20, 0.6)
            self.assertIn("kappa", str(e.exception))
            with self.assertRaises(SystemExit):
                L.freeze(final, items, "utterance", Path(t), None, 20, 0.6)

    def test_a_frozen_set_is_split_by_group_and_hashed(self):
        final, items = self.rows(60)
        with tempfile.TemporaryDirectory() as t:
            m = L.freeze(final, items, "utterance", Path(t), 0.7, 20, 0.6)
            groups = {s: {json.loads(l)["group"] for l in (Path(t) / f"{s}.jsonl").read_text(encoding="utf-8").splitlines()} for s in ("train", "dev", "test")}
            self.assertEqual(groups["train"] & groups["test"], set())
            self.assertEqual(groups["train"] & groups["dev"], set())
            self.assertEqual(groups["dev"] & groups["test"], set())
            self.assertEqual(sum(m["rows"].values()), 60)
            self.assertEqual(L.freeze(final, items, "utterance", Path(t) / "again", 0.7, 20, 0.6)["sha256"], m["sha256"])      # the same input freezes to the same bytes


if __name__ == "__main__":
    unittest.main()
