"""Tests for the examination staff-dialogue policy validator (owner decision D3)."""
from __future__ import annotations

import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_staff_exam_policy as v  # noqa: E402


class StaffExamPolicy(unittest.TestCase):
    def setUp(self):
        self.policy = v.load("staff_exam_policy.json")

    def test_the_shipped_policy_passes(self):
        self.assertEqual(v.problems(), [])

    def test_every_hinting_practice_line_was_replaced(self):
        practice = v.load("staff_intent_lines.json")["roles"]
        for r in self.policy["review"]:
            if r["verdict"] == "replaced":
                self.assertNotEqual(practice[r["role"]][r["intent"]], self.policy["roles"][r["role"]]["lines"][r["intent"]])

    def test_a_hinting_line_is_refused_even_if_the_file_list_is_emptied(self):
        bad = copy.deepcopy(self.policy)
        bad["forbidden_words"] = []
        bad["roles"]["nurse"]["lines"]["ask_prep_status"] = "Has the consent been signed? That is your next step."
        found = v.problems(bad)
        self.assertTrue(any("consent" in x for x in found), found)
        self.assertTrue(any("next step" in x for x in found), found)

    def test_a_missing_intent_line_no_address_words_and_an_unknown_role_are_caught(self):
        bad = copy.deepcopy(self.policy)
        del bad["roles"]["anaesthesia"]["lines"]["sign_in_answer"]
        bad["roles"]["nurse"]["address_words"] = []
        bad["roles"]["janitor"] = {"address_words": ["x"], "lines": {}}
        found = v.problems(bad)
        self.assertIn("anaesthesia.sign_in_answer: no examination line", found)
        self.assertIn("nurse: needs address words", found)
        self.assertIn("janitor: not a staff role of intents.json", found)

    def test_a_changed_line_without_review_and_a_number_are_caught(self):
        bad = copy.deepcopy(self.policy)
        bad["review"] = [r for r in bad["review"] if r["intent"] != "report_patient_request"]
        bad["roles"]["nurse"]["lines"]["report_observation"] = "The pressure is 90 over 60."
        found = v.problems(bad)
        self.assertIn("nurse.report_patient_request: differs from the practice line but has no review entry", found)
        self.assertIn("nurse.report_observation: examination line holds a number", found)


if __name__ == "__main__":
    unittest.main()
