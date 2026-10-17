"""Numeric generation (server/generation/numeric.py, p1-live-scenario-generation-s05, stage LG-07)."""
import copy
import json
import random
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "content"))
sys.path.insert(0, str(ROOT / "tools" / "data"))
from generation import numeric, packs  # noqa: E402
import news2  # noqa: E402
import profile_numeric as pn  # noqa: E402

LAB = packs.LabPack()
VITALS = packs.load("vitals")


def ks(sample, ps, vs):
    """Largest distance between the sample's empirical CDF and the pack's piecewise-linear CDF inside its p1 to p99 support."""
    xs = sorted(sample)
    worst = 0.0
    for i, x in enumerate(xs):
        f = packs.cdf(ps, vs, x)
        worst = max(worst, abs(f - i / len(xs)), abs(f - (i + 1) / len(xs)))
    return worst


def card(results, obs=None, text="A patient comes for planned surgery."):
    return {"identity": {"sex": "F"}, "presentation": {"summary": text}, "problems": [], "decisions": [], "paths": {}, "debrief": {}, "scores": [{"name": "NEWS2", "value": "0", "note": "", "origin": "book"}],
            "results": results, "observations": obs or [{"time": "08:00", "condition": "initial", "heart_rate": 74, "heart_rhythm": "regular", "systolic_bp": 132, "diastolic_bp": 80, "respiratory_rate": 14,
                                                           "spo2": 98, "oxygen": "room_air", "temperature_c": 36.8, "acvpu": "A", "pain_score": 0, "news2": 0, "note": "Baseline vital signs, all normal.", "origin": "authored"}]}


def result(rid, test, value, flag="normal", normal_range="3.5-5.3", **extra):
    return {"id": rid, "test": test, "value": value, "unit": "x", "normal_range": normal_range, "flag": flag, "comment": "", "quality": None, "previous": None, "minutes_to_result": 0, "origin": "book", **extra}


class Numbers(unittest.TestCase):
    def test_every_normal_draw_is_inside_the_band_over_10000_seeds(self):
        for analyte in ("potassium", "sodium", "haemoglobin", "creatinine", "wbc", "platelets", "alt", "bilirubin", "albumin", "inr", "lactate", "crp"):
            for sex in ("F", "M"):
                intervals = LAB._intervals(analyte, "normal", sex)
                for seed in range(10000 // 12):
                    d = LAB.draw(analyte, "normal", numeric.stream(seed, analyte), sex)
                    self.assertTrue(any(a <= d.value <= b for a, b in intervals), (analyte, sex, seed, d.value))

    def test_marginals_follow_the_profile_for_a_normal_slot(self):
        for analyte in ("potassium", "sodium", "haemoglobin"):
            row = LAB.doc["analytes"][analyte]
            ps, vs = packs.quantile_curve(row["quantiles"])
            sample = [LAB.draw(analyte, None, random.Random(s)).value for s in range(5000)]
            self.assertLess(ks(sample, ps, vs), 0.03, analyte)

    def test_the_same_seed_gives_identical_values_and_another_seed_other_ones(self):
        base = card([result("r1", "Potassium", "4.1"), result("r2", "Sodium", "139", normal_range="133-146")])
        a, b, c = copy.deepcopy(base), copy.deepcopy(base), copy.deepcopy(base)
        numeric.vary_numbers(a, 5, LAB, VITALS, news2.score)
        numeric.vary_numbers(b, 5, LAB, VITALS, news2.score)
        numeric.vary_numbers(c, 6, LAB, VITALS, news2.score)
        self.assertEqual(a, b)
        self.assertNotEqual(a["results"], c["results"])

    def test_the_numbers_the_story_is_built_on_are_never_touched(self):
        base = card([result("r1", "Potassium", "6.4", flag="high"),                              # abnormal
                     result("r2", "Sodium", "139", normal_range="133-146", source_row="SR-0001"), # named in the story below
                     result("r3", "Albumin", "40", normal_range="35-50", quality="haemolysed"),   # a sample with a fault
                     result("r4", "Haemoglobin", "130", normal_range="120-150", variant="C2"),    # belongs to a variant
                     result("r5", "ECG", "Sinus rhythm"),                                          # not a number
                     result("r6", "Creatinine", "70", normal_range="45-90", previous={"value": "66", "when": "last year"}),
                     result("r7", "Lactate", "1.1", normal_range="0.5-2.0")],                      # free
                    text="Her sodium is 139 and she is well.")
        c = copy.deepcopy(base)
        log = numeric.vary_numbers(c, 9, LAB, VITALS, news2.score)
        for i in range(6):
            self.assertEqual(c["results"][i], base["results"][i], base["results"][i]["id"])
        self.assertNotEqual(c["results"][6]["value"], "1.1")
        self.assertEqual({e["slot"] for e in log if e["action"] == "drawn"} & {"r1", "r2", "r3", "r4", "r5", "r6"}, set())
        self.assertNotIn("source_row", c["results"][6])
        self.assertEqual(c["results"][6]["origin"], "authored")

    def test_a_drawn_value_keeps_the_decimals_and_lies_in_the_cases_own_range(self):
        for seed in range(300):
            c = card([result("r1", "Potassium", "4.1", normal_range="3.5-5.3")])
            numeric.vary_numbers(c, seed, LAB, VITALS, news2.score)
            v = c["results"][0]["value"]
            self.assertRegex(v, r"^\d\.\d$")
            self.assertTrue(3.5 <= float(v) <= 5.3, v)

    def test_linked_analytes_that_have_a_solid_correlation_are_drawn_together(self):
        c = card([result("r1", "ALT", "20", normal_range="7-40"), result("r2", "Bilirubin", "10", normal_range="3-21")])
        log = numeric.vary_numbers(c, 4, LAB, VITALS, news2.score)
        together = [e for e in log if e.get("together_with")]
        self.assertEqual({e["slot"] for e in together}, {"r1", "r2"})

    def test_a_weak_pair_is_drawn_independently(self):
        c = card([result("r1", "WBC", "7.0", normal_range="4.0-11.0"), result("r2", "CRP", "2", normal_range="below 5")])
        log = numeric.vary_numbers(c, 4, LAB, VITALS, news2.score)
        self.assertFalse([e for e in log if e.get("together_with")])
        self.assertEqual({e["slot"] for e in log if e["action"] == "drawn"} & {"r1", "r2"}, {"r1", "r2"})

    def test_starting_vitals_stay_in_the_range_that_scores_0_and_the_total_does_not_change(self):
        moved = 0
        for seed in range(400):
            c = card([])
            numeric.vary_numbers(c, seed, LAB, VITALS, news2.score)
            o = c["observations"][0]
            for k, (lo, hi) in numeric.NEWS2_ZERO.items():
                self.assertTrue(lo <= o[k] <= hi, (seed, k, o[k]))
            self.assertEqual(news2.score(o["heart_rate"], o["systolic_bp"], o["respiratory_rate"], o["spo2"], o["temperature_c"], o["acvpu"], o["oxygen"]), 0)
            moved += o["heart_rate"] != 74
        self.assertGreater(moved, 200)

    def test_an_observation_that_is_not_all_normal_is_left_alone(self):
        sick = [{"time": "08:00", "condition": "initial", "heart_rate": 118, "heart_rhythm": "regular", "systolic_bp": 96, "diastolic_bp": 60, "respiratory_rate": 22, "spo2": 94, "oxygen": "room_air",
                 "temperature_c": 38.4, "acvpu": "A", "pain_score": 5, "news2": 6, "note": "x", "origin": "authored"}]
        c = card([], sick)
        before = copy.deepcopy(c)
        numeric.vary_numbers(c, 3, LAB, VITALS, news2.score)
        self.assertEqual(c["observations"], before["observations"])

    def test_a_vital_named_in_the_story_stays(self):
        c = card([], text="Her pulse is 74 and she is well.")
        numeric.vary_numbers(c, 3, LAB, VITALS, news2.score)
        self.assertEqual(c["observations"][0]["heart_rate"], 74)

    def test_a_range_that_cannot_be_read_falls_back_to_the_pack_band(self):
        self.assertIsNone(numeric.in_normal(5.0, "see the lab's note"))
        c = card([result("r1", "Potassium", "4.1", normal_range="lab-specific")])
        numeric.vary_numbers(c, 2, LAB, VITALS, news2.score)
        self.assertTrue(3.5 <= float(c["results"][0]["value"]) <= 5.3)

    def test_composed_skeletons_with_drawn_numbers_still_pass_stage_b(self):
        sys.path.insert(0, str(ROOT / "tools"))
        from composer import blocks as B, stage_b   # noqa: E402
        from generation import contracts, objectives as ob, retrieve, skeleton  # noqa: E402
        cases, tables, index = B.load_cases(), ob.Tables.load(), retrieve.build_index()
        ctx = stage_b.load_ctx(False, cases)
        changed = 0
        for seed in range(60):
            req = contracts.accept_request({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1" if seed % 2 == 0 else "S2", "level": "advanced", "seed": seed}, lambda n: 0)
            sk = skeleton.build_skeleton(ob.derive(req, [], tables), index, cases, tables)
            ctx["others"] = cases
            r = stage_b.validate(sk["case"], ctx)
            self.assertTrue(r.passed, (seed, [d.detail for d in r.defects][:2]))
            changed += any(e["action"] == "drawn" for e in sk["numeric_log"])
        self.assertGreater(changed, 20)


if __name__ == "__main__":
    unittest.main()
