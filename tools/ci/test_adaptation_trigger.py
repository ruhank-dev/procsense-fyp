"""The adaptation trigger: it is met only when every condition holds, a skipped suite is not a gap, and the committed page matches a fresh run."""
import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import adaptation_trigger as A  # noqa: E402

CFG = json.loads(A.CFG.read_text(encoding="utf-8"))
FEAT = json.loads(A.FEATURES.read_text(encoding="utf-8"))


def ready(cfg):
    """A configuration in which every condition except the gap holds."""
    c = copy.deepcopy(cfg)
    for t in c["tracks"]:
        for r in t["remedy_ladder"]:
            r["tried"] = True
        t.update(permitted_training_examples=t["min_training_examples"], heldout_available=True, compute_free_gpu_hours=10)
    return c


class Trigger(unittest.TestCase):
    def test_today_no_track_is_met(self):
        self.assertFalse(any(r["met"] for r in A.evaluate(CFG, FEAT)))

    def test_the_committed_page_is_current(self):
        self.assertEqual(A.PAGE.read_text(encoding="utf-8"), A.render(A.evaluate(CFG, FEAT)))

    def test_everything_else_in_place_but_no_gap_is_not_met(self):
        r = A.evaluate(ready(CFG), FEAT)
        llm = [x for x in r if x["id"] == "llm-adaptation"][0]
        self.assertFalse(llm["met"])                       # every gate suite passes: nothing to adapt for
        self.assertFalse(llm["conditions"][0][1])

    def test_a_failing_suite_plus_everything_else_meets_it(self):
        feat = copy.deepcopy(FEAT)
        feat["features"]["patient_dialogue"]["suites"][0]["status"] = "FAIL"
        llm = [x for x in A.evaluate(ready(CFG), feat) if x["id"] == "llm-adaptation"][0]
        self.assertTrue(llm["met"])

    def test_a_skipped_suite_is_not_a_gap(self):
        feat = copy.deepcopy(FEAT)
        feat["features"]["patient_dialogue"]["suites"][0]["status"] = "SKIPPED"
        llm = [x for x in A.evaluate(ready(CFG), feat) if x["id"] == "llm-adaptation"][0]
        self.assertFalse(llm["met"])

    def test_one_missing_condition_blocks_it(self):
        feat = copy.deepcopy(FEAT)
        feat["features"]["patient_dialogue"]["suites"][0]["status"] = "FAIL"
        for field, value in (("permitted_training_examples", 0), ("heldout_available", False), ("compute_free_gpu_hours", 0)):
            c = ready(CFG)
            c["tracks"][0][field] = value
            self.assertFalse(A.evaluate(c, feat)[0]["met"], field)
        c = ready(CFG)
        c["tracks"][0]["remedy_ladder"][0]["tried"] = False
        self.assertFalse(A.evaluate(c, feat)[0]["met"])

    def test_the_image_gap_is_counted_from_the_generated_gap_page(self):
        self.assertEqual(A.image_gap(), 29)


if __name__ == "__main__":
    unittest.main()
