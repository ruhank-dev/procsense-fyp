"""Novelty fingerprints, distances and the sealed package (server/generation/novelty.py, package.seal; p1-live-scenario-generation-s09, stage LG-13)."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from generation import novelty as nv, objectives as ob, package, pipeline, retrieve  # noqa: E402
from composer import blocks as B  # noqa: E402

CASES = B.load_cases()
BY_ID = {c["id"]: c for c in CASES}
TABLES = ob.Tables.load()
INDEX = retrieve.build_index()
CTX = pipeline.Context(INDEX, CASES, TABLES)
REQ = {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced"}


def levels(cid):
    return nv.levels_of(BY_ID[cid])


class Fingerprints(unittest.TestCase):
    def test_levels_are_deterministic_and_sorted(self):
        a, b = levels("VAR-S1-B"), levels("VAR-S1-B")
        self.assertEqual(a, b)
        for lv in a.values():
            self.assertEqual(lv, sorted(lv))
        self.assertIn("spine:VAR-S1-B", a["structural"])

    def test_distance_is_zero_for_the_same_case_and_symmetric(self):
        a, b = levels("VAR-S1-B"), levels("VAR-S1-C")
        self.assertEqual(nv.distance(a, a)["total"], 0.0)
        self.assertEqual(nv.distance(a, b), nv.distance(b, a))
        self.assertGreater(nv.distance(a, b)["total"], 0.35)

    def test_weights_sum_to_one(self):
        self.assertAlmostEqual(sum(nv.WEIGHTS.values()), 1.0)

    def test_a_new_name_on_the_same_story_is_not_a_new_story(self):
        c = copy.deepcopy(BY_ID["VAR-S1-B"])
        c["identity"]["full_name"] = "Someone Else"
        d = nv.distance(nv.levels_of(BY_ID["VAR-S1-B"]), nv.levels_of(c))
        self.assertEqual((d["structural"], d["clinical"]), (0.0, 0.0))
        self.assertLess(d["total"], nv.THRESHOLDS["min_distance"])


class Verdicts(unittest.TestCase):
    def test_empty_history_is_novel(self):
        self.assertEqual(nv.check(levels("VAR-S1-B"), []).verdict, "novel")

    def test_the_same_story_as_a_recent_session_is_refused(self):
        r = nv.check(levels("VAR-S1-B"), [levels("VAR-S1-B")])
        self.assertEqual(r.verdict, "not_novel")
        self.assertTrue(any("same structure" in x for x in r.reasons))

    def test_the_structure_rule_only_looks_at_the_last_three(self):
        far = [levels("VAR-S1-B")] + [levels(c) for c in ("VAR-S1-C", "VAR-S1-D", "VAR-S1-E", "VAR-S1-G")]
        self.assertEqual(nv.check(levels("VAR-S1-H"), far).verdict, "novel")

    def test_deliberate_practice_may_repeat_a_structure_when_the_clinical_set_differs(self):
        a = levels("VAR-S1-B")
        b = copy.deepcopy(a)
        b["clinical"] = ["result:made_up:high", "rhythm:irregular", "problem_type:life_threat"]
        b["surface"] = ["name:Another Person"]
        self.assertEqual(nv.check(b, [a], deliberate=True, thresholds={"min_distance": 0.0}).verdict, "novel")
        self.assertEqual(nv.check(b, [a], deliberate=False, thresholds={"min_distance": 0.0}).verdict, "not_novel")

    def test_library_distance_is_reported(self):
        r = nv.check(levels("VAR-S1-B"), [], [levels("VAR-S1-B"), levels("VAR-S1-C")])
        self.assertEqual(r.nearest_library, 0.0)

    def test_after_five_attempts_the_most_novel_is_served_and_logged(self):
        calls = []

        def build(attempt, avoid):
            calls.append(attempt)
            return {"n": attempt}, levels("VAR-S1-B")
        hist = [levels("VAR-S1-B")]
        sk, rep, log = nv.pick_novel(build, hist)
        self.assertEqual(len(calls), 5)
        self.assertEqual(rep.verdict, "low_novelty")
        self.assertIn("LOW_NOVELTY", rep.reasons[-1])
        self.assertEqual(len(log), 5)


class Sealed(unittest.TestCase):
    def test_a_generated_package_is_sealed_and_a_change_breaks_the_seal(self):
        r = pipeline.generate({**REQ, "seed": 11}, [], [], CTX)
        pkg = r.package
        self.assertTrue(package.verify_seal(pkg))
        self.assertEqual(set(pkg["fingerprint_levels"]), {"structural", "clinical", "surface"})
        self.assertEqual(pkg["novelty"]["verdict"], "novel")
        bad = copy.deepcopy(pkg)
        bad["case"]["identity"]["age"] += 1
        self.assertFalse(package.verify_seal(bad))
        self.assertTrue(any(d["code"] == "HASH" for d in package.validate(bad, CASES)["defects"]))

    def test_the_same_request_gives_the_same_package_and_hash(self):
        a = pipeline.generate({**REQ, "seed": 21}, [], [], CTX).package
        b = pipeline.generate({**REQ, "seed": 21}, [], [], CTX).package
        self.assertEqual(a["package_sha256"], b["package_sha256"])
        self.assertEqual(a["reproducibility_key"], b["reproducibility_key"])
        c = pipeline.generate({**REQ, "seed": 22}, [], [], CTX).package
        self.assertNotEqual(a["package_sha256"], c["package_sha256"])


class Simulation(unittest.TestCase):
    TRAINEES, SESSIONS = 25, 10                         # 1,000 trainees run locally: tools/generation/simulate_novelty.py

    def test_simulated_trainees_meet_the_novelty_target(self):
        stats = simulate(self.TRAINEES, self.SESSIONS)
        self.assertEqual(stats["fallbacks"], 0)
        self.assertGreaterEqual(stats["novel_rate"], 0.95, stats)
        self.assertGreater(stats["mean_nearest"], 0.35)


def simulate(trainees: int, sessions: int, scenario_mix=("S1", "S2")) -> dict:
    sessions_total = novel = low = fallbacks = 0
    nearest = []
    for t in range(trainees):
        hist: list[dict] = []
        for s in range(sessions):
            r = pipeline.generate({**REQ, "trainee": f"trainee-{t:03d}", "scenario": scenario_mix[(t + s) % len(scenario_mix)], "seed": t * 1000 + s}, [], hist, CTX)
            sessions_total += 1
            if r.package is None:
                fallbacks += 1
                continue
            n = r.package["novelty"]
            novel += n["verdict"] == "novel"
            low += n["verdict"] == "low_novelty"
            nearest.append(n["nearest"])
            hist.append(r.package["fingerprint_levels"])
    return {"sessions": sessions_total, "novel_rate": novel / max(sessions_total, 1), "low_novelty": low, "fallbacks": fallbacks, "mean_nearest": sum(nearest) / max(len(nearest), 1)}


if __name__ == "__main__":
    unittest.main()
