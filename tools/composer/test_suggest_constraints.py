"""Compatibility suggestions (tools/composer/suggest_constraints.py, A12.c2): suggestions are never constraints, tags of one block type are not paired, a pair that never meets although chance says it should is found, and the committed file is current."""
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import suggest_constraints as S  # noqa: E402


class Suggest(unittest.TestCase):
    def test_the_committed_file_is_current_and_changes_no_block(self):
        doc = json.loads(S.OUT.read_text(encoding="utf-8"))
        self.assertEqual(json.dumps(S.suggestions(), indent=1) + "\n", S.OUT.read_text(encoding="utf-8"))
        self.assertTrue(all(s["status"] == "SUGGESTED" and s["accepted"] is None for s in doc["suggestions"]))
        self.assertEqual(doc["count"], len(doc["suggestions"]))

    def test_an_exclusion_is_found_when_two_tags_never_meet_but_chance_says_they_should(self):
        per = {f"C{i}": {"X": {"a"} if i < 10 else set(), "Y": {"b"} if i >= 10 else set()} for i in range(20)}
        orig = S.case_tags
        S.case_tags = lambda: (per, {})
        try:
            out = S.suggestions()["suggestions"]
        finally:
            S.case_tags = orig
        self.assertIn(("excludes", "a", "b"), [(s["kind"], s["a"], s["b"]) for s in out])

    def test_tags_of_one_block_type_are_alternatives_and_are_not_paired(self):
        per = {f"C{i}": {"X": {"a"} if i < 10 else {"b"}} for i in range(20)}
        orig = S.case_tags
        S.case_tags = lambda: (per, {})
        try:
            self.assertEqual(S.suggestions()["suggestions"], [])
        finally:
            S.case_tags = orig


if __name__ == "__main__":
    unittest.main()
