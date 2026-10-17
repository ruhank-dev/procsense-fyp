"""The joint model of laboratory results (tools/generation/fit_joint.py, server/generation/packs.py draw_panel; p1-live-scenario-generation-s14, LG-07)."""
import json
import random
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "generation"))
sys.path.insert(0, str(ROOT / "tools" / "data"))
import fit_joint  # noqa: E402
import profile_numeric as pn  # noqa: E402
from generation import packs  # noqa: E402

PACK = packs.LabPack()
JOINT = json.loads((ROOT / "content" / "generation" / "packs" / "joint.json").read_text(encoding="utf-8"))


def spearman(xs, ys):
    return pn.pearson(pn.ranks(xs), pn.ranks(ys))


class Joint(unittest.TestCase):
    def test_the_fitted_matrix_is_a_valid_correlation_matrix_and_the_pack_matches_the_profile(self):
        self.assertEqual(fit_joint.check_doc(JOINT), [])
        profile = json.loads((ROOT / "tools" / "data" / "derived" / "profiles" / "joint.json").read_text(encoding="utf-8"))
        self.assertEqual(profile, JOINT)

    def test_it_is_small_data_and_says_so(self):
        self.assertIn("Small data", JOINT["card"])
        self.assertTrue(all(p["pairs"] >= fit_joint.MIN_PAIRS for p in JOINT["pairs"]))
        for p in JOINT["pairs"]:
            if not p["used"]:
                self.assertEqual(p["normal_correlation"], 0.0)

    def test_drawn_panels_keep_the_correlations_of_the_data_far_better_than_independent_draws(self):
        curve = {a for a, row in PACK.doc["analytes"].items() if row["quantiles"]}          # an analyte with no dataset curve cannot be drawn without a zone
        used = [p for p in JOINT["pairs"] if p["used"] and p["a"] in curve and p["b"] in curve and abs(p["spearman"]) >= 0.3]
        names = sorted({x for p in used for x in (p["a"], p["b"])})
        self.assertGreaterEqual(len(used), 4)
        n = 1500
        rng = random.Random(11)
        joint_cols = {a: [] for a in names}
        indep_cols = {a: [] for a in names}
        for _ in range(n):
            for a, d in zip(names, PACK.draw_panel([(a, None) for a in names], rng)):
                joint_cols[a].append(d.value)
            for a in names:
                indep_cols[a].append(PACK.draw(a, None, rng).value)
        err_joint = sum(abs(spearman(joint_cols[p["a"]], joint_cols[p["b"]]) - p["spearman"]) for p in used) / len(used)
        err_indep = sum(abs(spearman(indep_cols[p["a"]], indep_cols[p["b"]]) - p["spearman"]) for p in used) / len(used)
        self.assertLess(err_joint, 0.5 * err_indep, (err_joint, err_indep))

    def test_a_panel_is_the_same_for_the_same_seed_whatever_the_order_asked(self):
        a = PACK.draw_panel([("potassium", "normal"), ("sodium", "normal"), ("creatinine", "normal")], random.Random(5), "F")
        b = PACK.draw_panel([("creatinine", "normal"), ("potassium", "normal"), ("sodium", "normal")], random.Random(5), "F")
        by = lambda draws: {d.analyte: round(d.value, 6) for d in draws}          # noqa: E731
        self.assertEqual(by(a), by(b))

    def test_every_value_is_inside_its_zone(self):
        rng = random.Random(2)
        for _ in range(300):
            for a, d in zip(("potassium", "sodium", "creatinine", "wbc"), PACK.draw_panel([(x, "normal") for x in ("potassium", "sodium", "creatinine", "wbc")], rng, "M")):
                lows_highs = PACK.zones(a, "M")["normal"]
                self.assertTrue(any((lo is None or d.value >= lo - 1e-9) and (hi is None or d.value <= hi + 1e-9) for lo, hi in lows_highs), (a, d.value))

    def test_an_analyte_the_model_does_not_know_is_drawn_on_its_own(self):
        d = PACK.draw_panel([("beta_hcg", "normal"), ("potassium", "normal")], random.Random(1))
        self.assertEqual(len(d), 2)


if __name__ == "__main__":
    unittest.main()
