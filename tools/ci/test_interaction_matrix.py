"""Tests for the interaction coverage matrix generator (P3-INT-01)."""
import importlib.util
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("interaction_matrix", ROOT / "tools" / "plans" / "interaction_matrix.py")
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class MatrixTests(unittest.TestCase):
    def test_page_is_what_the_sources_give(self):
        self.assertEqual(m.OUT.read_text(encoding="utf-8").replace("\r\n", "\n"), m.build())

    def test_known_verbs_are_found(self):
        v = m.engine_verbs()
        self.assertIn("introduce_self", v)
        self.assertGreater(len(v), 40)

    def test_page_has_a_row_per_catalogue_verb(self):
        text = m.build()
        for verb in ("ask", "sign_consent", "wait"):
            self.assertIn(f"| {verb} |", text)

    def test_emitters_are_read_from_scenes(self):
        self.assertIn("read_wristband", m.emitter_verbs())


if __name__ == "__main__":
    unittest.main()
