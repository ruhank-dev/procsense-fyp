"""The usability analysis (tools/eval/analyse_usability.py): SUS and raw NASA-TLX are scored right on known answers, a half-filled form is refused, EXP-UI-1 is judged on
Step 1 with controllers in round 1, and a simulated table is marked and cannot be written into the results folders. The rows here are SIMULATED to exercise the code."""
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "eval"))
import analyse_usability as U  # noqa: E402


def form(p, answers, tlx=(40, 20, 30, 25, 45, 15), group="medical_student"):
    row = {"participant": p, "group": group, "case": "VAR-S1-A", "task": "step1_identity", "success": "1", "seconds": "95", "errors": "0", "end_state": "PROCEED"}
    row.update({f"sus{i + 1}": str(a) for i, a in enumerate(answers)})
    row.update(dict(zip(U.TLX, map(str, tlx))))
    return row


def task(p, name, success, seconds, group="medical_student"):
    row = {"participant": p, "group": group, "case": "VAR-S2-B", "task": name, "success": str(success), "seconds": str(seconds), "errors": "1", "end_state": ""}
    row.update({k: "" for k in U.SUS_ITEMS + U.TLX})
    return row


class Questionnaires(unittest.TestCase):
    def test_sus_best_worst_and_neutral(self):
        self.assertEqual(U.sus(form("SIM1", [5, 1] * 5)), 100.0)
        self.assertEqual(U.sus(form("SIM1", [1, 5] * 5)), 0.0)
        self.assertEqual(U.sus(form("SIM1", [3] * 10)), 50.0)
        self.assertEqual(U.sus(form("SIM1", [4, 2, 4, 2, 4, 2, 4, 2, 4, 2])), 75.0)

    def test_a_bad_answer_is_refused_not_scored(self):
        with self.assertRaises(ValueError):
            U.sus(form("SIM1", [6] + [3] * 9))
        with self.assertRaises(ValueError):
            U.raw_tlx(form("SIM1", [3] * 10, tlx=(120, 0, 0, 0, 0, 0)))

    def test_raw_tlx_is_the_unweighted_mean(self):
        self.assertAlmostEqual(U.raw_tlx(form("SIM1", [3] * 10)), 175 / 6)

    def test_describe_gives_median_iqr_and_range(self):
        self.assertEqual(U.describe([1, 2, 3, 4, 5]), "3.0 [2.0 to 4.0] (1.0 to 5.0), n = 5")
        self.assertIn("IQR needs 4", U.describe([1, 2]))


class Pages(unittest.TestCase):
    def rows(self):
        return [form("SIM1", [4, 2] * 5), task("SIM1", "sign_in", 1, 300), form("SIM2", [5, 1] * 5, group="junior_doctor"), task("SIM2", "sign_in", 0, 420, "junior_doctor")]

    def test_study_page_is_marked_simulated_and_scores_each_person_once(self):
        page, data = U.study(self.rows())
        self.assertTrue(page.startswith("> **SIMULATED DATA."))
        self.assertEqual(data["sus"], {"SIM1": 75.0, "SIM2": 100.0})
        self.assertIn("| sign_in | 1 of 2 |", page)
        self.assertNotIn("significant", page.replace("no claim of a difference between groups or of significance", ""))

    def test_simulated_rows_cannot_be_written_into_results(self):
        with tempfile.TemporaryDirectory() as tmp:
            csv_path = Path(tmp) / "study.csv"
            rows = self.rows()
            with csv_path.open("w", encoding="utf-8", newline="") as f:
                import csv
                w = csv.DictWriter(f, fieldnames=list(rows[0]))
                w.writeheader()
                w.writerows(rows)
            self.assertEqual(U.main(["study", "--csv", str(csv_path), "--out", str(U.RESULTS["study"] / "usability-study.md")]), 2)
            self.assertFalse((U.RESULTS["study"] / "usability-study.md").exists() and "SIMULATED" in (U.RESULTS["study"] / "usability-study.md").read_text(encoding="utf-8"))
            self.assertEqual(U.main(["study", "--csv", str(csv_path), "--out", str(Path(tmp) / "page.md")]), 0)

    def test_exp_ui_1_needs_four_of_five_on_step1_with_controllers(self):
        def r(p, ok, inp="controllers", rnd="1", t=U.STEP1):
            return {"participant": p, "round": rnd, "task": t, "input": inp, "success": str(ok), "seconds": "80", "confusions": "1", "note": ""}
        rows = [r(f"SIM{i}", 1 if i < 4 else 0) for i in range(5)] + [r("SIM0", 1, "hands"), r("SIM9", 1, rnd="2")]
        page, data = U.usability_pass(rows)
        self.assertEqual(data["verdict"], "met")
        self.assertEqual((data["step1_unaided"], data["step1_n"]), (4, 5))
        self.assertIn("hands 1 of 1", page)
        self.assertIn("Re-test after the fixes", page)
        rows[3]["success"] = "0"
        self.assertEqual(U.usability_pass(rows)[1]["verdict"], "not met")
        self.assertEqual(U.usability_pass(rows[:3])[1]["verdict"], "not yet run with 5 people")


if __name__ == "__main__":
    unittest.main(verbosity=1)
