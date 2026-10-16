"""The engine version rule (tools/ci/engine_version.py): changed golden hashes need a raised version."""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import engine_version as ev  # noqa: E402


class EngineVersionRule(unittest.TestCase):
    def test_the_real_lock_matches_the_engine_and_the_goldens(self):
        self.assertEqual(ev.check(ev.core_version(), ev.golden_digest(), ev.read_lock()), [])

    def test_changed_goldens_with_the_same_version_fail_and_say_how_to_fix_it(self):
        lock = {"version": "1.0.0", "golden_digest": "a" * 64}
        problems = ev.check("1.0.0", "b" * 64, lock)
        self.assertEqual(len(problems), 1)
        self.assertIn("raise it", problems[0])
        self.assertIn("ENGINE_VERSION_UNSUPPORTED", problems[0])

    def test_a_raised_version_whose_lock_was_not_updated_fails(self):
        lock = {"version": "1.0.0", "golden_digest": "a" * 64}
        self.assertEqual(len(ev.check("1.1.0", "b" * 64, lock)), 1)
        self.assertEqual(len(ev.check("1.1.0", "a" * 64, lock)), 1)

    def test_unchanged_goldens_and_version_pass(self):
        self.assertEqual(ev.check("1.0.0", "a" * 64, {"version": "1.0.0", "golden_digest": "a" * 64}), [])

    def test_a_missing_lock_fails(self):
        self.assertEqual(len(ev.check("1.0.0", "a" * 64, None)), 1)

    def test_the_digest_changes_when_a_golden_hash_changes(self):
        with tempfile.TemporaryDirectory() as d:
            g = Path(d)
            (g / "x.expected.json").write_text('{"log_hash": "1", "state_hash": "2"}', encoding="utf-8")
            first = ev.golden_digest(g)
            (g / "x.expected.json").write_text('{"log_hash": "1", "state_hash": "3"}', encoding="utf-8")
            self.assertNotEqual(first, ev.golden_digest(g))

    def test_the_version_is_read_from_the_source(self):
        self.assertEqual(ev.core_version('public const string Version = "9.8.7";'), "9.8.7")


if __name__ == "__main__":
    unittest.main()
