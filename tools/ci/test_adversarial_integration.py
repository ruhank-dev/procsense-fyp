"""Adversarial decision blocks inside generated cases (DT-197): added only where the case holds the anchor, one per case, reproducible, passing the whole generation gate, off when asked, and recorded in the package."""
import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from composer import blocks as B  # noqa: E402
from generation import adversarial, objectives as ob, package, pipeline, quality, retrieve  # noqa: E402

CASES = B.load_cases()
CTX = pipeline.Context(retrieve.build_index(), CASES, ob.Tables.load())
OFF = pipeline.Context(CTX.index, CASES, CTX.tables, adversarial_blocks=False)


def req(scenario, seed):
    return {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": scenario, "level": "advanced", "seed": seed}


class Integration(unittest.TestCase):
    def packages(self):
        out = []
        for scenario in ("S1", "S2"):
            for seed in range(0, 30, 3):
                r = pipeline.generate(req(scenario, seed), [], [], CTX, recent_fingerprints=[])
                if r.package:
                    out.append(r.package)
        return out

    def test_a_block_is_added_only_where_the_case_holds_its_anchor_and_the_package_passes_the_gate(self):
        pkgs = self.packages()
        with_block = [p for p in pkgs if p.get("adversarial")]
        self.assertGreaterEqual(len(pkgs), 8)
        self.assertGreaterEqual(len(with_block), 2, "the blocks reach generated cases")
        blocks = {b["id"]: b for b in adversarial.blocks()["blocks"]}
        for p in with_block:
            self.assertEqual(p["adversarial"]["status"], "awaiting_review")
            self.assertEqual(len(p["adversarial"]["added"]), adversarial.LIMIT)
            for a in p["adversarial"]["added"]:
                self.assertTrue(adversarial.holds(blocks[a["block"]], p["case"]), a)
                d = next(d for d in p["case"]["decisions"] if d["id"] == a["decision"])
                self.assertEqual(sum(o["correct"] for o in d["options"]), 1)
                self.assertEqual(len({o["text"] for o in d["options"]}), 2)
            self.assertTrue(package.validate(p, CASES)["passed"], p["id"])

    def test_a_case_without_a_fitting_block_is_left_alone_and_the_switch_turns_it_off(self):
        plain = {"allergies": [], "allergy_answer": "", "history": [], "results": [], "problems": [], "decisions": [{"id": "x.d1", "prompt": "p", "rule": None, "options": []}], "presentation": {}}
        before = copy.deepcopy(plain)
        self.assertEqual(adversarial.apply(plain, 7), [])
        self.assertEqual(plain, before)
        for p in self.packages():
            pass
        r = pipeline.generate(req("S2", 0), [], [], OFF, recent_fingerprints=[])
        self.assertIsNotNone(r.package)
        self.assertNotIn("adversarial", r.package)

    def test_the_same_request_gives_the_same_decision_and_a_later_call_adds_nothing_twice(self):
        a = pipeline.generate(req("S2", 3), [], [], CTX, recent_fingerprints=[])
        b = pipeline.generate(req("S2", 3), [], [], CTX, recent_fingerprints=[])
        self.assertEqual(a.package["case"]["decisions"], b.package["case"]["decisions"])
        case = copy.deepcopy(a.package["case"])
        n = len(case["decisions"])
        adversarial.apply(case, 3)
        self.assertEqual(len(case["decisions"]), n)                 # the decision is there already: not added a second time

    def test_the_trap_is_fair_the_consequence_names_something_the_case_holds(self):
        for p in [p for p in self.packages() if p.get("adversarial")]:
            mine = {a["decision"] for a in p["adversarial"]["added"]}
            only = dict(p["case"], decisions=[d for d in p["case"]["decisions"] if d["id"] in mine])
            score, defects = quality.trap_fairness(only)            # the spine's own options are measured by the package's quality report; this is the added decision alone
            self.assertEqual(defects, [], p["id"])
            self.assertEqual(score, 1.0)

    def test_the_server_and_the_checker_read_an_anchor_the_same_way(self):
        sys.path.insert(0, str(ROOT / "tools" / "content"))
        import check_adversarial as A
        for b in adversarial.blocks()["blocks"]:
            for cid, c in A.cases().items():
                self.assertEqual(adversarial.holds(b, c), cid in A.holders(b, A.cases()), (b["id"], cid))


if __name__ == "__main__":
    unittest.main()
