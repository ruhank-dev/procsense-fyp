"""The generator packs and their sampler (tools/generation, p1-dataset-analysis-s05): packs rebuild byte-identically, 10,000 draws reproduce the profile, zones are respected, linked analytes move together."""
import json
import random
import statistics
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "generation"))
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "data"))
import build_packs  # noqa: E402
import packs  # noqa: E402
import profile_numeric as pn  # noqa: E402


def q(xs, p):
    return pn.percentile(sorted(xs), p)


class Packs(unittest.TestCase):
    def setUp(self):
        self.pack = packs.LabPack()

    def test_the_committed_packs_and_evaluation_sets_are_current_and_hold_no_raw_rows(self):
        self.assertEqual(build_packs.problems(), [])

    def test_packs_rebuild_byte_identically(self):
        a, b = build_packs.pack_files(), build_packs.pack_files()
        self.assertEqual(a, b)
        manifest = json.loads(a["manifest"])
        import hashlib
        for name, digest in manifest["files"].items():
            self.assertEqual(hashlib.sha256(a[name[:-5]].encode("utf-8")).hexdigest(), digest)

    def test_ten_thousand_draws_from_the_whole_curve_reproduce_the_profile_within_tolerance(self):
        rng = random.Random(7)
        for analyte in ("potassium", "sodium", "haemoglobin", "creatinine", "wbc", "alt"):
            row = self.pack.doc["analytes"][analyte]
            xs = [self.pack.draw(analyte, None, rng).value for _ in range(10000)]
            span = row["quantiles"]["p95"] - row["quantiles"]["p5"]
            for p in (5, 25, 50, 75, 95):
                self.assertLess(abs(q(xs, p) - row["quantiles"][f"p{p}"]), 0.05 * span, f"{analyte} p{p}")

    def test_a_drawn_value_lies_in_its_zone_and_says_where_it_came_from(self):
        rng = random.Random(3)
        for analyte, zone in (("potassium", "normal"), ("potassium", "mild"), ("potassium", "severe"), ("crp", "critical"), ("sodium", "significant")):
            intervals = self.pack._intervals(analyte, zone, "")
            for _ in range(300):
                d = self.pack.draw(analyte, zone, rng)
                self.assertTrue(any(a <= d.value <= b for a, b in intervals), (analyte, zone, d))
                self.assertIn(d.source, ("dataset", "band_only"))

    def test_a_zone_the_dataset_never_saw_is_drawn_inside_the_band_and_marked_band_only(self):
        d = self.pack.draw("crp", "critical", random.Random(1))       # CRP 300 or more: above the demo's p99
        self.assertEqual(d.source, "band_only")
        self.assertGreaterEqual(d.value, 300)

    def test_an_analyte_with_no_dataset_curve_is_drawn_from_its_band_and_refuses_a_whole_curve_draw(self):
        d = self.pack.draw("qtc", "normal", random.Random(1), sex="F")
        self.assertEqual(d.source, "band_only")
        with self.assertRaises(ValueError):
            self.pack.draw("qtc", None, random.Random(1))

    def test_the_same_seed_gives_the_same_values(self):
        a = [self.pack.draw("potassium", "mild", random.Random(11)).value for _ in range(3)]
        b = [self.pack.draw("potassium", "mild", random.Random(11)).value for _ in range(3)]
        self.assertEqual(a, b)

    def test_a_sex_specific_zone_uses_the_right_interval(self):
        lo_m, hi_m = self.pack._intervals("creatinine", "normal", "M")[0]
        lo_f, hi_f = self.pack._intervals("creatinine", "normal", "F")[0]
        self.assertEqual((lo_m, hi_m, lo_f, hi_f), (60.0, 110.0, 45.0, 90.0))

    def test_linked_analytes_drawn_together_rise_together_and_unlinked_ones_do_not(self):
        rng = random.Random(5)
        pairs = [self.pack.draw_pair("urea_nitrogen_unused", "creatinine", None, None, rng) if False else None]
        del pairs
        corr = {frozenset((c["a"], c["b"])): c for c in json.loads((packs.PACKS / "correlations.json").read_text(encoding="utf-8"))["pairs"]}
        self.assertIn(frozenset(("wbc", "crp")), corr)
        # WBC and CRP are a weak pair (37 pairs): drawn independently. Sodium and glucose have 2701 pairs and a small negative rho.
        pair = corr[frozenset(("sodium", "glucose"))]
        self.assertFalse(pair["weak"])
        xs, ys = [], []
        for _ in range(4000):
            a, b = self.pack.draw_pair("sodium", "glucose", None, None, rng)
            xs.append(a.value)
            ys.append(b.value)
        got = pn.correlation(xs, ys)["spearman"]
        self.assertAlmostEqual(got, pair["spearman"], delta=0.06)
        xs, ys = [], []
        for _ in range(3000):
            a, b = self.pack.draw_pair("wbc", "crp", None, None, rng)
            xs.append(a.value)
            ys.append(b.value)
        self.assertLess(abs(pn.correlation(xs, ys)["spearman"]), 0.08)        # a weak pair is not used

    def test_evaluation_sets_cover_every_case_every_mutation_and_each_query_has_an_answer(self):
        gold = json.loads((build_packs.EVAL / "gold_scenarios.json").read_text(encoding="utf-8"))["scenarios"]
        self.assertEqual(len(gold), 53)
        self.assertEqual(len(json.loads((build_packs.EVAL / "mutations.json").read_text(encoding="utf-8"))["types"]), 12)
        for query in json.loads((build_packs.EVAL / "retrieval_queries.json").read_text(encoding="utf-8"))["queries"]:
            self.assertTrue(query["expected_cases"], query["query"])


if __name__ == "__main__":
    unittest.main()
