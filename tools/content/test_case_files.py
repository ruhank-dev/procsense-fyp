"""Tests for the playable case files and their checker.

    uv run --project tools tools/content/test_case_files.py
CI runs it as "case file tests". The negative tests copy a real case, break one thing and expect the checker to name it.
"""
import copy
import json
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import case_file_checks as chk  # noqa: E402

CASES = {p.stem: json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "case_files").glob("*.json"))}
CTX = chk.load_context()
P0 = ["VAR-S1-A", "VAR-S1-PB", "VAR-S1-E", "VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J", "VAR-S2-PC", "VAR-S2-PD", "VAR-S2-B", "VAR-S2-D", "VAR-S2-F", "VAR-S2-G", "STK-S1-D", "STK-S2-B"]


def broken(case_id, change):
    c = copy.deepcopy(CASES[case_id])
    change(c)
    return chk.problems(c, CTX, list(CASES.values()))


class TheSeventeenCases(unittest.TestCase):
    def test_exactly_the_seventeen_p0_cases_exist(self):
        self.assertEqual(sorted(CASES), sorted(P0))

    def test_every_case_passes_the_checker(self):
        for cid, c in CASES.items():
            self.assertEqual(chk.problems(c, CTX, list(CASES.values())), [], cid)

    def test_every_case_is_synthetic_and_approved_on_the_owners_statement(self):
        for c in CASES.values():
            self.assertTrue(c["synthetic"])
            self.assertEqual(c["status"], "APPROVED_FOR_TRAINING")
            self.assertIn("Owner", c["approval"]["by"])

    def test_the_approval_is_tied_to_the_exact_content(self):
        import build_case_files as b
        approvals = json.loads((ROOT / "content/case_approvals.json").read_text(encoding="utf-8"))["approvals"]
        for cid, c in CASES.items():
            self.assertEqual(approvals[cid]["sha256"], b.content_hash(c), cid)

    def test_the_usman_timeline_uses_17_not_the_books_16(self):
        usman = {o["time"]: o["news2"] for o in CASES["VAR-S2-PD"]["observations"] if o["condition"] != "after_correct_action"}
        self.assertEqual(usman, {"14:00": 2, "14:45": 5, "15:15": 10, "15:45": 17})

    def test_example_patient_a_agrees_with_the_core_loaded_card(self):
        card = json.loads((ROOT / "content/cases/VAR-S1-A.json").read_text(encoding="utf-8"))
        c = CASES["VAR-S1-A"]
        self.assertEqual(c["base_card"], "VAR-S1-A")
        self.assertEqual({k: c["identity"][k] for k in ("title", "full_name", "age", "occupation", "dob", "mrn")}, card["identity"])
        self.assertEqual({h["topic"]: h["answer"] for h in c["history"]}, {h["topic"]: h["answer"] for h in card["history"]})
        bv = c["observations"][0]
        self.assertEqual((bv["heart_rate"], bv["systolic_bp"], bv["diastolic_bp"], bv["respiratory_rate"], bv["spo2"], bv["temperature_c"]),
                         (card["baseline_vitals"]["heart_rate"], card["baseline_vitals"]["systolic_bp"], card["baseline_vitals"]["diastolic_bp"], card["baseline_vitals"]["respiratory_rate"],
                          card["baseline_vitals"]["spo2"], card["baseline_vitals"]["temperature_c"]))

    def test_the_books_numbers_are_in_the_cases(self):
        r = {x["id"]: x["value"] for x in CASES["VAR-S1-PB"]["results"]}
        self.assertEqual((r["lab.bilirubin"], r["lab.alp"], r["lab.ggt"], r["lab.alt"]), ("72", "380", "410", "160"))
        self.assertEqual(CASES["VAR-S1-PB"]["expected_end_state"], "postponed")
        self.assertEqual(CASES["STK-S2-B"]["identity"]["full_name"], "Ravi Patel")
        self.assertEqual(CASES["STK-S2-B"]["allergies"][0]["substance"], "Penicillin")
        self.assertEqual(CASES["VAR-S2-B"]["expected_end_state"], "redirected")

    def test_no_case_file_contains_a_dose(self):
        for cid, c in CASES.items():
            self.assertIsNone(chk.DOSE.search(json.dumps(c, ensure_ascii=False)), cid)

    def test_every_picture_result_of_a_case_points_at_a_real_case_file_or_scenario_card(self):
        ids = {r["case_id"] for r in CTX["imaging"]}
        self.assertTrue(set(P0) & ids)
        self.assertTrue(all(i.startswith(("VAR-", "STK-", "SC-")) for i in ids))


class BreakingSomethingIsCaught(unittest.TestCase):
    def has(self, case_id, change, expect):
        found = broken(case_id, change)
        self.assertTrue(any(expect in p for p in found), found)

    def test_a_wrong_news2(self):
        self.has("VAR-S2-PC", lambda c: c["observations"][0].update(news2=5), "table gives")

    def test_a_flag_that_contradicts_the_range(self):
        self.has("VAR-S1-PB", lambda c: c["results"][0].update(flag="normal"), "outside")

    def test_a_normal_value_flagged_high(self):
        self.has("VAR-S1-PB", lambda c: next(r for r in c["results"] if r["id"] == "lab.wbc").update(flag="high"), "inside")

    def test_a_critical_potassium_not_flagged_critical(self):
        def change(c):
            r = next(r for r in c["results"] if r["id"] == "lab.k1")
            r.update(value="6.8", flag="high")
        self.has("VAR-S1-J", change, "critical value")

    def test_a_book_value_that_differs_from_the_walkthrough_row(self):
        self.has("VAR-S1-PB", lambda c: c["results"][0].update(value="70"), "differs from the walkthrough row")

    def test_a_dose_is_rejected(self):
        self.has("VAR-S2-PC", lambda c: c["decisions"][0]["options"][0].update(text="Give 1 g of paracetamol now"), "dose")

    def test_an_unknown_rule(self):
        self.has("VAR-S1-C", lambda c: c["decisions"][0].update(rule="no_such_rule"), "unknown rule")

    def test_a_decision_with_no_correct_option(self):
        def change(c):
            for o in c["decisions"][0]["options"]:
                o["correct"] = False
        self.has("VAR-S1-C", change, "needs at least one correct")

    def test_an_age_that_does_not_match_the_date_of_birth(self):
        self.has("VAR-S1-C", lambda c: c["identity"].update(age=60), "age")

    def test_a_wristband_that_does_not_match_the_identity(self):
        self.has("VAR-S1-C", lambda c: c["wristband"].update(mrn_text="MR 000000"), "wristband")

    def test_a_repeated_mrn(self):
        self.has("VAR-S1-C", lambda c: c["identity"].update(mrn=CASES["VAR-S1-G"]["identity"]["mrn"]), "repeats")

    def test_an_unknown_end_state_and_verification_id(self):
        self.has("VAR-S1-C", lambda c: c.update(expected_end_state="ok"), "schema")
        self.has("VAR-S1-C", lambda c: c["verification"].append("V-99"), "verification id")

    def test_a_hidden_fact_with_no_trigger(self):
        self.has("VAR-S1-C", lambda c: c["history"][1].update(disclose_on=[]), "no disclose_on")

    def test_problem_ranks_with_a_gap(self):
        self.has("VAR-S1-PB", lambda c: c["problems"][1].update(rank=5), "ranks")

    def test_a_missing_required_section(self):
        def change(c):
            del c["debrief"]
        self.has("VAR-S1-C", change, "schema")


if __name__ == "__main__":
    unittest.main(verbosity=1)
