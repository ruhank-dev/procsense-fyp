"""The reconciliation page (tools/plans/reconcile.py): it covers every P0, P1 and P2 step, names only evidence that exists, and each kind of mistake in its data is caught."""
import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "plans"))
import reconcile as R  # noqa: E402


class Reconcile(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.found = R.steps()
        cls.data = json.loads(R.DATA.read_text(encoding="utf-8"))

    def test_the_real_data_covers_every_step_and_names_existing_evidence(self):
        self.assertEqual(R.problems(self.found, self.data), [])
        self.assertEqual({t: sum(1 for f in self.found.values() if f["tier"] == t) for t in ("p0", "p1", "p2")}, {"p0": 173, "p1": 77, "p2": 19})          # 269 steps, as docs/plans/milestones.md counts them

    def test_the_committed_page_is_what_the_data_gives(self):
        self.assertEqual(R.OUT.read_text(encoding="utf-8"), R.render(self.found, self.data))

    def test_each_kind_of_mistake_is_reported(self):
        d = copy.deepcopy(self.data)
        some = next(s for s, e in d["steps"].items() if e["now"] == "DONE")
        del d["steps"][some]
        self.assertTrue(any("no entry for step" in p for p in R.problems(self.found, d)))
        d = copy.deepcopy(self.data)
        d["steps"]["p9-nothing-s01"] = d["steps"][some] if some in d["steps"] else next(iter(d["steps"].values()))
        self.assertTrue(any("which is not a step" in p for p in R.problems(self.found, d)))
        d = copy.deepcopy(self.data)
        d["steps"][some]["evidence"] = ["no/such/file.py"]
        self.assertTrue(any("does not exist" in p for p in R.problems(self.found, d)))
        d = copy.deepcopy(self.data)
        d["steps"][some]["now"] = "FINISHED"
        self.assertTrue(any("vocabulary" in p for p in R.problems(self.found, d)))
        d = copy.deepcopy(self.data)
        d["steps"][some]["now"] = "BLOCKED"
        d["steps"][some].pop("open", None)
        self.assertTrue(any("must say what is open" in p for p in R.problems(self.found, d)))

    def test_a_tick_is_not_taken_for_proof(self):
        # every step the page calls blocked, partly done or off carries a reason, whether or not the owner ticked it
        for s, e in self.data["steps"].items():
            if e["now"] in ("PARTLY", "BLOCKED", "DONE_GATE_OFF", "ENG_DONE"):
                self.assertTrue(e.get("open"), s)

    def test_engineering_done_names_who_the_external_acceptance_needs(self):
        for s, e in self.data["steps"].items():
            if e["now"] == "ENG_DONE":
                self.assertTrue(e.get("external"), s)
        d = copy.deepcopy(self.data)
        some = next(s for s, e in d["steps"].items() if e["now"] == "ENG_DONE")
        d["steps"][some].pop("external")
        self.assertTrue(any("ENG_DONE must say" in p for p in R.problems(self.found, d)))


if __name__ == "__main__":
    unittest.main()
