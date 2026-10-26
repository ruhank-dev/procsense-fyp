"""The adversarial decision blocks (DT-197): fair, different, anchored, never presented as reviewed; and the checker catches a broken block."""
import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "content"))
import check_adversarial as A  # noqa: E402

DATA = json.loads(A.BLOCKS.read_text(encoding="utf-8"))
CASES = A.cases()


class Adversarial(unittest.TestCase):
    def test_the_shipped_blocks_pass_every_check_and_the_page_is_current(self):
        self.assertEqual(A.problems(DATA, CASES), [])
        self.assertEqual(A.PAGE.read_text(encoding="utf-8"), A.page(DATA, CASES))
        self.assertGreaterEqual(len(DATA["blocks"]), 12)

    def test_every_block_is_held_by_a_case_and_the_wrong_option_is_not_the_right_one(self):
        for b in DATA["blocks"]:
            self.assertTrue(A.holders(b, CASES), b["id"])
            self.assertNotEqual(b["wrong"].lower(), b["right"].lower())
            self.assertTrue(b["source"])

    def test_the_status_never_claims_a_review(self):
        self.assertEqual(DATA["status"], "awaiting_review")
        bad = copy.deepcopy(DATA)
        bad["status"] = "clinician_approved"
        self.assertTrue(any("awaiting_review" in p for p in A.problems(bad, CASES)))

    def test_a_trap_nobody_can_be_fairly_given_a_repeated_id_and_a_consequence_that_teaches_nothing_are_caught(self):
        bad = copy.deepcopy(DATA)
        bad["blocks"][0]["anchor"]["match"] = "zzzzzz-no-such-thing"
        bad["blocks"][1]["id"] = bad["blocks"][0]["id"]
        bad["blocks"][2]["consequence"] = "That was not the best choice."
        found = " ".join(A.problems(bad, CASES))
        self.assertIn("no case holds the anchor", found)
        self.assertIn("id repeated", found)
        self.assertIn("names nothing of the anchor", found)

    def test_a_wrong_option_that_gives_itself_away_by_length_is_caught(self):
        bad = copy.deepcopy(DATA)
        bad["blocks"][0]["wrong"] = "Do it. " * 30
        self.assertTrue(any("similar and short length" in p for p in A.problems(bad, CASES)))


if __name__ == "__main__":
    unittest.main()
