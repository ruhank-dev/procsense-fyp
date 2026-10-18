"""Seeded requests of five kinds put to the live scenario generator (tools/generation/request_fuzz.py): the set is what the seed gives, and no request breaks an invariant (refused with a reason, or served with a sealed, gate-passing, reproducible package, or the documented fallback)."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "generation"))
import request_fuzz as F  # noqa: E402


class Fuzz(unittest.TestCase):
    def test_the_committed_set_is_what_the_seed_gives(self):
        self.assertEqual(F.OUT.read_text(encoding="utf-8"), json.dumps(F.build(), indent=1) + "\n")

    def test_every_kind_is_present_and_the_set_says_it_is_synthetic(self):
        d = json.loads(F.OUT.read_text(encoding="utf-8"))
        self.assertTrue(d["synthetic"] and not d["independent"])
        self.assertEqual({r["kind"] for r in d["requests"]}, {"normal", "boundary", "ambiguous", "contradictory", "adversarial"})
        self.assertGreaterEqual(len(d["requests"]), 100)

    def test_no_request_breaks_an_invariant(self):
        out = F.run()
        self.assertEqual(out["breaks"], [], out["breaks"][:5])
        self.assertGreater(out["served"], 40)
        self.assertGreater(out["refused"], 30)
        self.assertEqual(out["by_kind"]["adversarial"]["served"], 0)                     # nothing hostile is ever served


if __name__ == "__main__":
    unittest.main()
