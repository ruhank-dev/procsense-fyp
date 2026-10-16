"""The voice-line catalogue (tools/voice/catalogue.py, p0-voice-audio-s01 and s06): every line has a source, the nurse has a line for every step the engine publishes, and a missing nurse line is reported."""
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "voice"))
import catalogue  # noqa: E402


class VoiceCatalogue(unittest.TestCase):
    def setUp(self):
        text, self.problems, self.notes = catalogue.build()
        self.doc = json.loads(text)

    def test_every_line_has_a_source_and_an_event(self):
        self.assertEqual(self.problems, [])
        for l in self.doc["lines"]:
            self.assertTrue(l["source"], l["id"])
            self.assertTrue(l["event"], l["id"])

    def test_the_seventeen_p0_cases_each_have_their_patient_lines(self):
        self.assertEqual(len(self.doc["cases"]), 17)
        for case in self.doc["cases"]:
            ids = {l["id"] for l in self.doc["lines"] if l.get("case") == case}
            self.assertIn(f"PAT-{case}-IDENTITY", ids)
            self.assertIn(f"PAT-{case}-COMPLAINT", ids)

    def test_the_nurse_has_a_line_for_every_wristband_step_and_outcome(self):
        nurse = [l for l in self.doc["lines"] if l["id"].startswith("NUR-WRISTBAND")]
        self.assertEqual(len(nurse), 10)
        self.assertEqual(catalogue.nurse_coverage(self.doc["lines"]), [])
        self.assertTrue(all(l["spoken_variant"] == "authored" for l in nurse))

    def test_a_missing_nurse_line_is_reported(self):
        without_done = [l for l in self.doc["lines"] if l["id"] != "NUR-WRISTBAND-DONE"]
        self.assertEqual(catalogue.nurse_coverage(without_done), ["nurse step DONE: no line"])

    def test_nurse_lines_carry_no_patient_specific_words(self):
        for l in self.doc["lines"]:
            if l["id"].startswith("NUR-WRISTBAND"):
                self.assertNotRegex(l["text"], r"\d", l["id"])                      # no number, date or record number
                self.assertNotIn("{", l["text"], l["id"])


if __name__ == "__main__":
    unittest.main(verbosity=1)
