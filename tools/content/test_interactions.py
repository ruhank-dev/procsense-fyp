"""Tests for the interaction catalogue generator and validator (P3-CTX-01)."""
from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_interactions as b  # noqa: E402
import validate_interactions as v  # noqa: E402


class Interactions(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(b.build())

    def test_shipped_file_is_what_the_generator_builds_and_passes(self):
        self.assertEqual(b.OUT.read_text(encoding="utf-8").replace("\r\n", "\n"), b.build())
        self.assertEqual(v.problems(), [])

    def test_every_tablet_entry_is_covered(self):
        tablet = json.loads((b.UI / "tablet.json").read_text(encoding="utf-8"))
        self.assertEqual(len(self.data["entries"]), sum(len(t["entries"]) for t in tablet["tabs"]))

    def test_an_orphan_entry_is_caught(self):
        d = copy.deepcopy(self.data)
        d["global"]["entries"] = []
        self.assertTrue(any("belongs to no entity" in x for x in v.problems(d)))

    def test_an_unknown_id_and_a_duplicate_are_caught(self):
        d = copy.deepcopy(self.data)
        d["entities"][0]["groups"][0]["entries"].append("nope.nope")
        d["entries"].append(copy.deepcopy(d["entries"][0]))
        out = v.problems(d)
        self.assertTrue(any("unknown entry nope.nope" in x for x in out))
        self.assertTrue(any("duplicate entry id" in x for x in out))

    def test_a_spoken_physical_action_is_caught(self):
        d = copy.deepcopy(self.data)
        e = next(x for x in d["entries"] if x["verb"] == "confirm_identity")
        e["voice"] = {"spoken": True, "examples": ["x"]}
        self.assertTrue(v.problems(d))

    def test_consequential_actions_need_confirmation(self):
        self.assertTrue(all(e["confirm"] for e in self.data["entries"] if e["verb"] in ("sign_consent", "postpone")))


if __name__ == "__main__":
    unittest.main()
