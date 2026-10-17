"""Quality metrics and bounded repair (server/generation/quality.py, repair.py, pipeline.py; p1-live-scenario-generation-s08, stages LG-11 and LG-12).

The gold set is made here and is synthetic: 'good' is each of the 53 approved authored cases against objectives set to its own tier; 'poor' is each of them damaged in one way the metrics should catch.
Human ratings of real generated cases are not available yet (DT-196), so this measures whether the gate catches what it is built to catch, not whether it agrees with a teacher.
"""
import copy
import json
import sys
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from generation import contracts, objectives as ob, package, pipeline, quality, repair, retrieve  # noqa: E402
from composer import blocks as B  # noqa: E402

CASES = B.load_cases()
TABLES = ob.Tables.load()
DISC = json.loads((ROOT / "content" / "problems" / "discovery.json").read_text(encoding="utf-8"))["discovery"]
CTX = pipeline.Context(retrieve.build_index(), CASES, TABLES)
REQ = {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced"}


def objectives_for(c):
    o = ob.derive(contracts.accept_request({**REQ, "scenario": c["scenario"], "seed": 1}, lambda n: 0), [], TABLES)
    o["difficulty_vector"] = {k: TABLES.case_rating(c["tier"]) for k in o["difficulty_vector"]}
    return o


def evaluate(c, o=None):
    return quality.evaluate(c, o or objectives_for(c), TABLES, [d for d in DISC if d["case"] == c["id"]])


def codes(r):
    return {f["code"] for f in r["failures"]}


# one way to spoil a case each; every one should be rejected on its own code
def no_actions(c):
    c["problems"][0]["expected_actions"] = []


def no_correct(c):
    for o in c["decisions"][0]["options"]:
        o["correct"] = False


def silent_wrong(c):
    for d in c["decisions"]:
        for o in d["options"]:
            if not o["correct"]:
                o["consequence"] = ""


def placeholder(c):
    c["history"][0]["answer"] = "I take {medicine} every day for {condition}."


def wrong_pronoun(c):
    c["history"][0]["answer"] = ("She says that her pain began last night." if c["identity"]["sex"] == "M" else "He says that his pain began last night.")


def rambling(c):
    c["history"][0]["answer"] = " ".join(["The pain began some time ago and then moved around the stomach and the back and the side"] * 6) + "."


def duplicate_options(c):
    o = c["decisions"][0]["options"]
    o[1]["text"] = o[0]["text"]


def all_correct(c):
    for o in c["decisions"][0]["options"]:
        o["correct"] = True


POOR = [("Q_TEACHABLE", no_actions), ("Q_FAIRNESS", no_correct), ("Q_FAIRNESS", silent_wrong), ("Q_COHERENCE", placeholder), ("Q_PERSONA", wrong_pronoun), ("Q_READING", rambling)]


class Gold(unittest.TestCase):
    def test_at_least_90_percent_of_good_cases_pass(self):
        good = [evaluate(c) for c in CASES]
        passed = sum(r["verdict"] == "pass" for r in good)
        self.assertGreaterEqual(passed / len(good), 0.9, [(c["id"], codes(r)) for c, r in zip(CASES, good) if r["verdict"] != "pass"][:5])

    def test_every_degraded_case_is_rejected_on_the_expected_code(self):
        missed = []
        for code, damage in POOR:
            for c in CASES:
                d = copy.deepcopy(c)
                damage(d)
                r = evaluate(d)
                if code not in codes(r):
                    missed.append((code, damage.__name__, c["id"]))
        self.assertEqual(missed, [])

    def test_duplicate_and_all_correct_options_are_rejected(self):
        for damage in (duplicate_options, all_correct):
            for c in CASES:
                if len(c["decisions"][0]["options"]) < 2:
                    continue
                d = copy.deepcopy(c)
                damage(d)
                self.assertIn("Q_FAIRNESS", codes(evaluate(d)), c["id"])

    def test_a_case_far_from_the_target_difficulty_is_rejected(self):
        c = next(x for x in CASES if x["tier"] == "P0")
        o = objectives_for(c)
        o["difficulty_vector"] = {k: TABLES.case_rating("P3") + 300 for k in o["difficulty_vector"]}
        self.assertIn("Q_DIFFICULTY", codes(evaluate(c, o)))

    def test_a_spine_with_a_problem_nothing_reveals_is_rejected(self):
        c = copy.deepcopy(next(x for x in CASES if x["id"] == "VAR-S1-B"))
        c["problems"].append({**c["problems"][0], "id": "b.extra_unfindable"})
        self.assertIn("Q_DISCOVERABLE", codes(evaluate(c)))

    def test_coverage_counts_only_what_a_case_file_can_show(self):
        c = next(x for x in CASES if len(x["problems"]) < 3)
        o = objectives_for(c)
        o["dimensions"] = ["competing_priorities", "complex_handover"]
        r = evaluate(c, o)
        self.assertIn("Q_COVERAGE", codes(r))
        self.assertEqual(r["headset_dimensions"], ["complex_handover"])

    def test_the_trap_metric_is_advisory_and_never_fails_a_case(self):
        for c in CASES:
            self.assertNotIn("Q_TRAP", codes(evaluate(c)))

    def test_the_judge_slot_is_logged_and_does_not_change_the_verdict(self):
        c = CASES[0]
        r = quality.evaluate(c, objectives_for(c), TABLES, [], judge={"narrative": 1})
        self.assertEqual(r["judge"], {"narrative": 1})
        self.assertEqual(r["verdict"], evaluate(c)["verdict"])


class Repair(unittest.TestCase):
    def test_the_table_picks_the_cheapest_fix_then_the_next(self):
        d = [{"code": "VR-CM-01"}]
        self.assertEqual(repair.choose(d, []), repair.REDRAW_NUMBERS)
        self.assertEqual(repair.choose(d, [repair.REDRAW_NUMBERS]), repair.RESELECT_MEDIA)
        self.assertIsNone(repair.choose([{"code": "Q_TEACHABLE"}], [repair.SWAP_SPINE]))
        self.assertEqual(repair.choose([{"code": "SOMETHING_NEW"}], []), repair.SWAP_BLOCKS)

    def test_every_code_the_gate_can_raise_has_a_strategy(self):
        for code in ("VR-CM-0%d" % i for i in range(1, 9)):
            self.assertIn(code, repair.TABLE)
        for c in ("Q_TEACHABLE", "Q_DISCOVERABLE", "Q_DIFFICULTY", "Q_COVERAGE", "Q_FAIRNESS", "Q_READING", "Q_PERSONA", "Q_COHERENCE"):
            self.assertIn(c, repair.TABLE)

    def test_a_defect_is_repaired_and_every_check_is_run_again(self):
        real = pipeline._gate
        seen = []

        def flaky(pkg, o, ctx):
            seen.append(pkg["provenance"]["seed"])
            return [{"stage": "crossmodal", "code": "VR-CM-01", "detail": "x"}] if len(seen) == 1 else real(pkg, o, ctx)
        with mock.patch.object(pipeline, "_gate", flaky):
            r = pipeline.generate({**REQ, "seed": 5}, [], [], CTX)
        self.assertIsNotNone(r.package)
        self.assertEqual(len(seen), 2)
        rep = r.package["provenance"]["repairs"]
        self.assertEqual((rep[0]["strategy"], rep[0]["result"]), (repair.REDRAW_NUMBERS, "passed"))
        self.assertTrue(package.verify_seal(r.package))

    def test_the_budget_is_never_exceeded_and_the_fallback_is_named(self):
        calls = []

        def always(pkg, o, ctx):
            calls.append(1)
            return [{"stage": "stage_b", "code": "STAGE_B_FAIL", "detail": "x"}]
        with mock.patch.object(pipeline, "_gate", always):
            r = pipeline.generate({**REQ, "seed": 6}, [], [], CTX)
        self.assertIsNone(r.package)
        self.assertEqual(r.fallback, "FALLBACK-POOL-LIBRARY")
        self.assertLessEqual(len(calls), 1 + 3)
        self.assertTrue(r.defects)

    def test_a_spent_time_budget_stops_repair(self):
        b = repair.Budget(rounds=3, seconds=-1)
        self.assertTrue(b.spent())
        with mock.patch.object(pipeline, "_gate", lambda *a: [{"stage": "x", "code": "Q_TEACHABLE", "detail": "x"}]):
            r = pipeline.generate({**REQ, "seed": 7}, [], [], CTX, budget=b)
        self.assertEqual(r.fallback, "FALLBACK-POOL-LIBRARY")
        self.assertEqual(b.used, 0)

    def test_swap_spine_leaves_out_the_spine_and_changes_the_seed(self):
        o = {"seed": 10, "exclusions": {"spines": ["A"], "fingerprints": []}}
        change, kw = repair.kwargs_for(repair.SWAP_SPINE, o, "B", 1)
        self.assertEqual(change["exclusions"]["spines"], ["A", "B"])
        self.assertNotEqual(change["seed"], 10)
        self.assertEqual(kw, {})


if __name__ == "__main__":
    unittest.main()
