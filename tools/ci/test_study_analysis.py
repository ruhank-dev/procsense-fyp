"""The analysis of the two p1-evaluation studies (tools/eval/analyse_study.py): the permutation test and effect sizes are right on known data, a safety issue keeps the AI dialogue off, a simulated table is marked as
such and cannot be written into the results folder, and thin data is flagged. The rows here are SIMULATED to exercise the code; they are not a result about people."""
import csv
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "eval"))
import analyse_study as S  # noqa: E402


def learning_rows(n, gain=3.0):
    rows = []
    for i in range(n):
        rows.append({"participant": f"SIM{i}", "session": 1, "quiz_before": 10 + i % 3, "quiz_after": 12, "critical_items": 3, "correct_end_state": 0, "seconds_to_decision": 300})
        rows.append({"participant": f"SIM{i}", "session": 2, "quiz_before": 12, "quiz_after": 10 + i % 3 + gain, "critical_items": 1, "correct_end_state": 1, "seconds_to_decision": 240})
    return rows


def ab_rows(n, ai_realism=6, menu_realism=4, safety=0):
    rows = []
    for i in range(n):
        order = "ai_first" if i % 2 else "menu_first"
        rows.append({"participant": f"SIM{i}", "order": order, "mode": "menu", "realism": menu_realism + i % 2, "task_success": 1, "seconds": 200, "rejections": 0, "fallbacks": 0, "safety_issues": 0})
        rows.append({"participant": f"SIM{i}", "order": order, "mode": "ai", "realism": ai_realism + i % 2, "task_success": 1, "seconds": 230, "rejections": 2, "fallbacks": 1, "safety_issues": safety if i == 0 else 0})
    return rows


class Stats(unittest.TestCase):
    def test_the_sign_flip_test_is_exact_for_small_samples(self):
        self.assertEqual(S.sign_flip_p([1, 1, 1, 1]), 0.125)                 # 2 of 16 sign patterns are as extreme
        self.assertEqual(S.sign_flip_p([1, -1, 1, -1]), 1.0)
        self.assertEqual(S.sign_flip_p([0, 0]), 1.0)

    def test_effect_sizes_and_intervals(self):
        e = S.effect([2, 3, 4, 3, 2, 4, 3, 3])
        self.assertEqual((e["n"], e["mean_difference"]), (8, 3.0))
        self.assertGreater(e["effect_size_g"], 2)
        self.assertTrue(e["ci95"][0] <= 3.0 <= e["ci95"][1])
        self.assertEqual(S.effect([1.0])["ci95"], [None, None])


class Learning(unittest.TestCase):
    def test_a_gain_shows_and_only_complete_pairs_count(self):
        rows = learning_rows(10) + [{"participant": "SIM99", "session": 1, "quiz_before": 5, "quiz_after": 6, "critical_items": 2, "correct_end_state": 0, "seconds_to_decision": 400}]
        res = S.learning([{k: str(v) for k, v in r.items()} for r in rows])
        self.assertEqual((res["participants"], res["complete"]), (11, 10))
        self.assertGreater(res["measures"]["quiz_score"]["mean_difference"], 0)
        self.assertLess(res["measures"]["critical_items"]["mean_difference"], 0)
        self.assertEqual(res["warnings"], [])

    def test_few_participants_are_flagged(self):
        self.assertTrue(S.learning([{k: str(v) for k, v in r.items()} for r in learning_rows(4)])["warnings"])


class AB(unittest.TestCase):
    def test_higher_realism_and_no_safety_issue_allows_the_default(self):
        res = S.ab([{k: str(v) for k, v in r.items()} for r in ab_rows(12)])
        self.assertEqual(res["ai_totals"], {"validator_rejections": 24, "fallbacks": 12, "safety_issues": 0})
        self.assertIn("may be switched on by default", res["decision"])
        self.assertIn("realism", res["order_effect"])

    def test_one_safety_issue_keeps_it_off_whatever_the_realism(self):
        res = S.ab([{k: str(v) for k, v in r.items()} for r in ab_rows(12, safety=1)])
        self.assertIn("stays OFF", res["decision"])

    def test_no_difference_is_not_a_benefit(self):
        res = S.ab([{k: str(v) for k, v in r.items()} for r in ab_rows(12, ai_realism=4)])
        self.assertIn("no clear benefit", res["decision"])


class Output(unittest.TestCase):
    def write(self, rows, d):
        p = Path(d) / "x.csv"
        with p.open("w", encoding="utf-8", newline="") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0]))
            w.writeheader()
            w.writerows(rows)
        return p

    def test_a_simulated_table_says_so_and_cannot_enter_the_results_folder(self):
        with tempfile.TemporaryDirectory() as d:
            csv_path = self.write(learning_rows(8), d)
            out = Path(d) / "page.md"
            self.assertEqual(S.main(["learning", "--csv", str(csv_path), "--out", str(out)]), 0)
            self.assertTrue(out.read_text(encoding="utf-8").startswith("> **SIMULATED DATA"))
            with self.assertRaises(SystemExit):
                S.main(["learning", "--csv", str(csv_path), "--out", str(S.RESULTS / "learning-effect.md")])
            self.assertFalse((S.RESULTS / "learning-effect.md").exists())

    def test_limitations_are_always_on_the_page(self):
        res = S.learning([{k: str(v) for k, v in r.items()} for r in learning_rows(8)])
        self.assertIn("no control group", S.page("learning", res, False))


if __name__ == "__main__":
    unittest.main()
