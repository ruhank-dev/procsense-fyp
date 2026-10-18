"""The blind-comparison tool for the human raters (tools/eval/blind_compare.py, EX-03): the arithmetic of the analysis on made-up sheets (a unit test of the tool, NOT a rating of anything), the refusal to fill a missing score, and the
plain-text rendering of a generated case."""
import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "eval"))
sys.path.insert(0, str(ROOT / "server"))
import blind_compare as BC  # noqa: E402


def sheet(path: Path, scores: dict[str, tuple[int, int]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=BC.COLUMNS)
        w.writeheader()
        for item, (c, fit) in scores.items():
            w.writerow({"item": item, "objectives": "x", "case": "y", "coherence": c, "fit": fit})


class Analysis(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.key = {}
        self.a, self.b = {}, {}
        for r in range(12):                                    # 12 made-up requests: the planner arm is scored one point higher by both raters
            for tag, arm in (("A", "planner"), ("B", "solver")) if r % 2 else (("A", "solver"), ("B", "planner")):
                item = f"R{r:02d}{tag}"
                self.key[item] = arm
                base = 4 if arm == "planner" else 3
                jitter = (r % 3) - 1
                self.a[item] = (min(5, max(1, base + jitter)), base)
                self.b[item] = (min(5, max(1, base + jitter)), base)

    def test_a_clear_and_agreed_difference_is_reported_as_better(self):
        sheet(self.tmp / "a.csv", self.a)
        sheet(self.tmp / "b.csv", self.b)
        out = BC.analyse_scores(BC.read_scores(self.tmp / "a.csv"), BC.read_scores(self.tmp / "b.csv"), self.key)
        for crit in ("coherence", "fit"):
            self.assertEqual(out["criteria"][crit]["verdict"], "planner better", crit)
            self.assertGreater(out["criteria"][crit]["ci95"][0], 0)
        self.assertTrue(out["planner_adopted_over_solver"])

    def test_raters_who_disagree_make_the_result_unreliable(self):
        flipped = {i: (6 - c, f) for i, (c, f) in self.b.items()}
        sheet(self.tmp / "a.csv", self.a)
        sheet(self.tmp / "b.csv", flipped)
        out = BC.analyse_scores(BC.read_scores(self.tmp / "a.csv"), BC.read_scores(self.tmp / "b.csv"), self.key)
        self.assertEqual(out["criteria"]["coherence"]["verdict"], "unreliable: raters disagree")
        self.assertFalse(out["planner_adopted_over_solver"])

    def test_no_difference_is_not_a_win(self):
        same = {i: (3, 3) for i in self.key}
        sheet(self.tmp / "a.csv", same)
        sheet(self.tmp / "b.csv", same)
        out = BC.analyse_scores(BC.read_scores(self.tmp / "a.csv"), BC.read_scores(self.tmp / "b.csv"), self.key)
        self.assertFalse(out["planner_adopted_over_solver"])

    def test_a_missing_score_stops_instead_of_being_filled(self):
        bad = dict(self.a)
        sheet(self.tmp / "a.csv", bad)
        rows = list(csv.DictReader((self.tmp / "a.csv").open(encoding="utf-8")))
        rows[0]["coherence"] = ""
        with (self.tmp / "a.csv").open("w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(f, fieldnames=BC.COLUMNS)
            w.writeheader()
            w.writerows(rows)
        with self.assertRaises(SystemExit):
            BC.read_scores(self.tmp / "a.csv")

    def test_kappa_of_identical_ratings_is_one(self):
        self.assertAlmostEqual(BC.weighted_kappa([1, 2, 3, 4, 5, 3], [1, 2, 3, 4, 5, 3]), 1.0)

    def test_mismatched_items_are_refused(self):
        with self.assertRaises(SystemExit):
            BC.analyse_scores({"R00A": {"coherence": 3, "fit": 3}}, {"R00B": {"coherence": 3, "fit": 3}}, {"R00A": "solver"})


class Render(unittest.TestCase):
    def test_a_generated_case_renders_without_naming_its_arm(self):
        pkg = json.loads((ROOT / "tests" / "golden_generated" / "generated_VAR-S1-A.package.json").read_text(encoding="utf-8"))
        text = BC.render_case(pkg)
        self.assertIn("Problems in the case", text)
        self.assertNotIn("solver", text.lower())
        self.assertNotIn("planner", text.lower())


if __name__ == "__main__":
    unittest.main(verbosity=1)
