"""The flat retrieval index and the recorded decision about it (server/retrieval/flat.py, tools/ai/eval_retrieval.py; p2-ai-extensions-s02): exact cosine ranking, stable ties, an embedder that fails is an error and not
a silent gap, and the committed measurement says what was decided and why."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from retrieval.flat import FlatIndex, unit  # noqa: E402

VEC = {"allergy": [1.0, 0.0, 0.0], "medicines": [0.0, 1.0, 0.0], "smoking": [0.0, 0.0, 1.0], "tablets please": [0.1, 0.9, 0.0], "same as allergy": [1.0, 0.0, 0.0]}


class Flat(unittest.TestCase):
    def index(self):
        return FlatIndex.build(lambda t: VEC[t], [("h.allergy", "allergy"), ("h.medicines", "medicines"), ("h.smoking", "smoking")])

    def test_the_nearest_fact_comes_first(self):
        hits = self.index().top_k(VEC["tablets please"], 2)
        self.assertEqual([h[0] for h in hits], ["h.medicines", "h.allergy"])
        self.assertGreater(hits[0][1], hits[1][1])

    def test_vectors_are_length_normalised_and_ties_break_by_id(self):
        self.assertAlmostEqual(sum(x * x for x in unit([3.0, 4.0])), 1.0)
        idx = FlatIndex.build(lambda t: VEC[t], [("b", "allergy"), ("a", "same as allergy")])
        self.assertEqual([h[0] for h in idx.top_k(VEC["allergy"], 2)], ["a", "b"])

    def test_an_embedder_that_cannot_answer_is_an_error(self):
        with self.assertRaises(RuntimeError):
            FlatIndex.build(lambda t: None, [("x", "allergy")])


class Decision(unittest.TestCase):
    path = ROOT / "reports" / "ai" / "retrieval-eval.json"

    def test_the_committed_measurement_applies_its_own_rule(self):
        if not self.path.exists():
            self.skipTest("no measurement committed yet")
        d = json.loads(self.path.read_text(encoding="utf-8"))
        ship = d["asked_fact_in_top"]["3"] >= 0.95 and d["saving_top3_against_allowed_today"] >= 0.30
        self.assertEqual(d["dialogue_decision"], "ship" if ship else "do not ship")
        self.assertTrue(d["debrief_decision"].startswith("do not ship"))
        self.assertGreaterEqual(d["questions"], 100)


if __name__ == "__main__":
    unittest.main()
