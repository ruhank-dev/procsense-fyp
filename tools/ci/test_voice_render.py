"""The offline voice renderer (tools/voice/render.py, p0-voice-audio-s02): the parts that need neither Piper nor ffmpeg, so CI can run them."""
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "voice"))
import render  # noqa: E402


class Render(unittest.TestCase):
    def test_a_respelling_changes_whole_words_only_and_never_the_subtitle(self):
        words = {"ASA": "A S A", "BP": "B P"}
        self.assertEqual(render.respell("ASA grade 2, BP fine.", words), "A S A grade 2, B P fine.")
        self.assertEqual(render.respell("BASAL BPM", words), "BASAL BPM")

    def test_the_line_hash_changes_with_the_words_the_role_or_the_voice_and_not_otherwise(self):
        role = {"pitch": 1.0, "pace": 1.0}
        h = render.line_hash("Hello.", role, "v")
        self.assertEqual(h, render.line_hash("Hello.", dict(role), "v"))
        self.assertNotEqual(h, render.line_hash("Hello!", role, "v"))
        self.assertNotEqual(h, render.line_hash("Hello.", {"pitch": 0.9, "pace": 1.0}, "v"))
        self.assertNotEqual(h, render.line_hash("Hello.", role, "w"))

    def test_the_pitch_is_changed_by_resampling_and_the_speed_it_causes_is_undone(self):
        f = render.audio_filter({"pitch": 0.8, "pace": 1.0}, 22050)
        self.assertIn("asetrate=17640", f)
        self.assertIn("atempo=1.2500", f)                         # 1.0 / 0.8
        self.assertEqual(render.audio_filter({"pitch": 1.0, "pace": 1.0}, 22050), "anull")

    def test_every_role_the_catalogue_uses_has_a_voice_setting(self):
        cfg = json.loads(render.VOICES.read_text(encoding="utf-8"))
        cat = json.loads(render.CATALOGUE.read_text(encoding="utf-8"))
        for line in cat["lines"]:
            self.assertIn(render.role_of(line), cfg["roles"], line["id"])

    def test_patients_get_their_role_from_the_case_and_staff_from_their_job(self):
        self.assertEqual(render.role_of({"speaker": "patient", "case": "VAR-S1-A", "id": "x"}), "patient_female")
        self.assertEqual(render.role_of({"speaker": "nurse", "id": "x"}), "nurse")

    def test_templates_and_lines_without_words_are_left_out_of_the_plan(self):
        cat = {"lines": [
            {"id": "A", "speaker": "nurse", "text": "Hello."},
            {"id": "B", "speaker": "nurse", "text": "Allergy to {allergy}.", "template": ["allergy"]},
            {"id": "C", "speaker": "nurse", "text": ""},
        ]}
        cfg = json.loads(render.VOICES.read_text(encoding="utf-8"))
        todo, templates = render.plan(cat, cfg, {}, {"lines": {}})
        self.assertEqual([t[0]["id"] for t in todo], ["A"])
        self.assertEqual([t["id"] for t in templates], ["B"])

    def test_the_catalogue_has_unique_line_ids(self):
        ids = [l["id"] for l in json.loads(render.CATALOGUE.read_text(encoding="utf-8"))["lines"]]
        self.assertEqual(len(ids), len(set(ids)))

    def test_the_voice_in_use_is_the_one_whose_licence_allows_it(self):
        cfg = json.loads(render.VOICES.read_text(encoding="utf-8"))
        self.assertEqual(cfg["model"], "en_GB-cori-high")          # DT-184: the other British voices wait for a licence answer


if __name__ == "__main__":
    unittest.main()
