"""Tests for the grades.json validator (P3-RES-10, P3-EXM-01)."""
from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_grades as v  # noqa: E402


class Grades(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(v.GRADES.read_text(encoding="utf-8"))

    def bad(self, change) -> list[str]:
        d = copy.deepcopy(self.data)
        change(d)
        return v.problems(d)

    def test_the_shipped_file_passes_and_keeps_the_hint_limit_of_three(self):
        self.assertEqual(v.problems(), [])
        self.assertEqual(self.data["hint_limit"], 3)

    def test_bands_must_descend(self):
        self.assertTrue(any("must be above" in p for p in self.bad(lambda d: d["bands"].update(B=95))))
        self.assertTrue(any("must be above" in p for p in self.bad(lambda d: d["bands"].update(C=80))))

    def test_bands_must_be_whole_numbers_within_0_to_100(self):
        self.assertTrue(self.bad(lambda d: d["bands"].update(A=101)))
        self.assertTrue(self.bad(lambda d: d["bands"].update(D=-1)))
        self.assertTrue(self.bad(lambda d: d["bands"].update(C=70.5)))
        self.assertTrue(self.bad(lambda d: d["bands"].pop("D")))
        self.assertTrue(self.bad(lambda d: d["bands"].update(E=50)))

    def test_pass_mark_must_be_reachable(self):
        self.assertTrue(any("nobody could pass" in p for p in self.bad(lambda d: d.update(pass_mark=95))))
        self.assertTrue(self.bad(lambda d: d.update(pass_mark=-5)))
        self.assertTrue(self.bad(lambda d: d.pop("pass_mark")))

    def test_hint_penalty_and_limit(self):
        self.assertTrue(self.bad(lambda d: d.update(hint_penalty=-1)))
        self.assertTrue(self.bad(lambda d: d.update(hint_penalty=True)))
        self.assertTrue(self.bad(lambda d: d.update(hint_limit=11)))
        self.assertTrue(self.bad(lambda d: d.update(hint_limit="3")))
        self.assertEqual(self.bad(lambda d: d.pop("hint_limit")), [])          # the engine reads 3 when it is missing

    def test_unknown_keys_and_schema(self):
        self.assertTrue(self.bad(lambda d: d.update(pass_mrk=60)))
        self.assertTrue(self.bad(lambda d: d.update(schema="x")))


if __name__ == "__main__":
    unittest.main()
