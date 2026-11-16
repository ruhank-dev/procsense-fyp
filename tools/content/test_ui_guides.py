"""Tests for the UI guide validator and generator (P3-TRN-01, P3-TRN-02 content half, P3-VOX-07)."""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_training_guide as b  # noqa: E402
import validate_ui_guides as v  # noqa: E402


class UiGuides(unittest.TestCase):
    def test_the_shipped_files_pass(self):
        self.assertEqual(v.training_guide_problems(), [])
        self.assertEqual(v.voice_feedback_problems(), [])

    def test_the_training_guide_is_what_the_generator_builds(self):
        self.assertEqual(v.UI.joinpath("training_guide.json").read_text(encoding="utf-8").replace("\r\n", "\n"), b.build())

    def test_a_non_diabetic_case_lists_the_glucose_step_as_not_needed(self):
        g = json.loads(b.build())
        r = next(x for x in g["inactive_reasons"] if x["id"] == "ward.cbg")
        self.assertEqual(r["reason_id"], "NOT_DIABETIC")

    def test_every_conditional_goal_has_a_reason(self):
        g = json.loads(b.build())
        wf = json.loads((v.ROOT / "content/workflow/workflow.json").read_text(encoding="utf-8"))
        conditional = {x["id"] for x in wf["goals"] if x["applies_if"] is not True}
        self.assertTrue(conditional <= {r["id"] for r in g["inactive_reasons"]})

    def test_the_validator_catches_a_missing_entry_and_a_missing_voice_code(self):
        orig = v.load
        try:
            def drop_entry(path):
                d = orig(path)
                if path.name == "training_guide.json":
                    d["entries"] = [e for e in d["entries"] if e["id"] != "G-FASTING"]
                return d
            v.load = drop_entry
            self.assertTrue(any("G-FASTING" in x for x in v.training_guide_problems()))

            def drop_code(path):
                d = orig(path)
                if path.name == "voice_feedback.json":
                    d["outcomes"] = [o for o in d["outcomes"] if o["code"] != "AMBIGUOUS"]
                return d
            v.load = drop_code
            self.assertTrue(any("AMBIGUOUS" in x for x in v.voice_feedback_problems()))
        finally:
            v.load = orig


if __name__ == "__main__":
    unittest.main()
