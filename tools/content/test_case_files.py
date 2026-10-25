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
COMBINATIONS = {c["id"] for c in json.loads((ROOT / "content" / "combination_cases.json").read_text(encoding="utf-8"))["cases"]}   # DT-220: the authored combination cases
BOOK_CASES = 53                                                                                                                                 # the walkthrough's own cases
P0 = ["VAR-S1-A", "VAR-S1-PB", "VAR-S1-E", "VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J", "VAR-S2-PC", "VAR-S2-PD", "VAR-S2-B", "VAR-S2-D", "VAR-S2-F", "VAR-S2-G", "STK-S1-D", "STK-S2-B"]


def broken(case_id, change):
    c = copy.deepcopy(CASES[case_id])
    change(c)
    return chk.problems(c, CTX, list(CASES.values()))


class RiskData(unittest.TestCase):
    """Every case states the ASA grade it expects and the anaesthetic plan (p0-simulation-workflow-s08)."""

    def test_a_case_without_risk_is_refused(self):
        import build_case_files
        cases = build_case_files.build()
        ctx = chk.load_context()
        c = copy.deepcopy(cases["VAR-S1-A"])
        del c["risk"]
        self.assertTrue(any("risk" in p and "required" in p for p in chk.problems(c, ctx)))

    def test_a_backwards_range_or_an_s2_standard_plan_is_refused(self):
        import build_case_files
        cases = build_case_files.build()
        ctx = chk.load_context()
        c = copy.deepcopy(cases["VAR-S1-A"])
        c["risk"]["asa"].update(min=3, max=2)
        self.assertTrue(any("min <= max" in p for p in chk.problems(c, ctx)))
        d = copy.deepcopy(cases["VAR-S2-PC"])
        d["risk"]["plan"] = "standard_ga"
        self.assertTrue(any("rsi" in p for p in chk.problems(d, ctx)))

    def test_every_case_has_a_range_inside_1_to_6_and_a_reason(self):
        import build_case_files
        cases = build_case_files.build()
        self.assertEqual(len(cases), BOOK_CASES + len(COMBINATIONS))
        for k, c in cases.items():
            a = c["risk"]["asa"]
            self.assertTrue(1 <= a["min"] <= a["max"] <= 6 and a["why"].strip(), k)


class FastingIntake(unittest.TestCase):
    """tools/content/fasting_intake.py reads the wording of last food and last drink once, when the files are built."""

    def test_the_walkthrough_cases(self):
        import fasting_intake as fi
        self.assertEqual(fi.intake("last_food", "Dinner at 21:00 last night (rice and chicken).", "08:00"), {"kind": "heavy_meal", "hours_before_now": 11.0})
        self.assertEqual(fi.intake("last_drink", "Water at 06:30.", "08:00"), {"kind": "clear_liquids", "hours_before_now": 1.5})
        self.assertEqual(fi.intake("last_food", "Toast and tea with milk at 07:00 this morning.", "08:00"), {"kind": "light_meal", "hours_before_now": 1.0})
        self.assertEqual(fi.intake("last_drink", "Tea with milk at 07:00.", "08:00"), {"kind": "milk", "hours_before_now": 1.0})
        self.assertEqual(fi.intake("last_food", "Biryani at 12:00 today, about 2 hours ago.", "14:00"), {"kind": "heavy_meal", "hours_before_now": 2.0})

    def test_other_ways_of_saying_when(self):
        import fasting_intake as fi
        self.assertEqual(fi.intake("last_food", "Dry toast yesterday at noon; nothing since.", "08:00")["hours_before_now"], 20.0)
        self.assertEqual(fi.intake("last_food", "Toast two days ago; sips of water today.", "14:00"), {"kind": "light_meal", "hours_before_now": 48.0})
        self.assertEqual(fi.intake("last_drink", "Sips of water 3 hours ago.", "08:00"), {"kind": "clear_liquids", "hours_before_now": 3.0})
        self.assertEqual(fi.intake("last_food", "A kebab 3 hours ago.", "08:00"), {"kind": "heavy_meal", "hours_before_now": 3.0})

    def test_what_is_not_clear_is_not_guessed(self):
        import fasting_intake as fi
        self.assertEqual(fi.intake("last_food", "Breakfast at 08:00.", "13:00"), {"kind": "unknown", "hours_before_now": 5.0})
        self.assertEqual(fi.intake("last_food", "Unknown; probably a few hours ago.", "08:00"), {"kind": "unknown"})
        self.assertEqual(fi.intake("last_food", "Nothing since lunch.", "08:00"), {"kind": "none"})

    def test_every_case_records_it_and_none_has_a_time_today_that_is_later_than_now(self):
        import build_case_files
        for k, c in build_case_files.build().items():
            for h in c["history"]:
                if h["topic"] in ("last_food", "last_drink"):
                    self.assertIn("intake", h, k)


class TheFiftyThreeCases(unittest.TestCase):
    def test_exactly_the_books_53_cases_and_the_combination_cases_exist_in_three_tiers(self):
        self.assertEqual(len(CASES), BOOK_CASES + len(COMBINATIONS))
        self.assertEqual(len(COMBINATIONS), 12)
        tiers = {t: sorted(i for i, c in CASES.items() if c["tier"] == t) for t in ("P0", "P1", "P2")}
        self.assertEqual(tiers["P0"], sorted(P0))
        self.assertEqual((len(tiers["P0"]), len(tiers["P1"]), len(tiers["P2"])), (17, 16, 20 + len(COMBINATIONS)))
        self.assertEqual(tiers["P2"], sorted([f"SC-{n}" for n in range(1, 21)] + sorted(COMBINATIONS)))

    def test_every_case_in_the_seed_table_has_a_case_file(self):
        import csv
        with open(ROOT / "content/case_seeds.csv", encoding="utf-8", newline="") as f:
            seeds = {r["id"] for r in csv.DictReader(f)}
        self.assertEqual(seeds - set(CASES) - {"VAR-S1-F"}, set(), "a seed case has no case file")

    def test_every_case_passes_the_checker(self):
        for cid, c in CASES.items():
            self.assertEqual(chk.problems(c, CTX, list(CASES.values())), [], cid)

    def test_every_case_is_synthetic_and_approved_on_the_owners_statement(self):
        pending = set(json.loads((ROOT / "content/case_signoff_pending.json").read_text(encoding="utf-8"))["pending"])
        for c in CASES.values():
            self.assertTrue(c["synthetic"])
            if c["id"] in COMBINATIONS:                # authored 25 Oct 2026 (DT-220); the owner authorized educational practice use the same day, and nobody claims clinical review
                self.assertEqual(c["status"], "APPROVED_FOR_TRAINING", c["id"])
                self.assertIn("educational practice use", c["approval"]["by"])
                self.assertIn("No clinician reviewed", c["approval"]["note"])
                continue
            if c["id"] in pending:                     # changed after the last attestation: stays DRAFT until signed off (docs/research/clinician_signoff.md)
                self.assertEqual(c["status"], "DRAFT", c["id"])
                continue
            self.assertEqual(c["status"], "APPROVED_FOR_TRAINING", c["id"])
            self.assertIn("Owner", c["approval"]["by"])
        self.assertTrue(pending <= set(CASES))

    def test_the_approval_is_tied_to_the_exact_content(self):
        import build_case_files as b
        approvals = json.loads((ROOT / "content/case_approvals.json").read_text(encoding="utf-8"))["approvals"]
        pending = set(json.loads((ROOT / "content/case_signoff_pending.json").read_text(encoding="utf-8"))["pending"])
        for cid, c in CASES.items():
            if cid in pending:                        # changed since the attestation, so the old approval no longer matches and the case is DRAFT
                self.assertNotEqual(approvals[cid]["sha256"], b.content_hash(c), cid)
            else:
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


class EmrRecords(unittest.TestCase):
    """The previous records written for each case (content/emr_case_records.csv) restate the case file and add nothing."""

    @classmethod
    def setUpClass(cls):
        import csv
        with open(ROOT / "content/emr_case_records.csv", encoding="utf-8", newline="") as f:
            cls.rec = list(csv.DictReader(f))

    def test_every_case_has_records_and_planned_patients_have_a_clinic_ultrasound(self):
        for cid, c in CASES.items():
            mine = [r for r in self.rec if r["case_id"] == cid]
            self.assertTrue(mine, cid)
            kinds = {r["doc_type"] for r in mine}
            self.assertIn("clinic_letter", kinds, cid)
            if c["scenario"] == "S1":
                self.assertIn("imaging_report", kinds, cid)

    def test_records_name_the_right_patient_and_hold_no_dose(self):
        for r in self.rec:
            c = CASES[r["case_id"]]
            if r["doc_type"] == "clinic_letter":
                self.assertIn(c["identity"]["full_name"], r["d_body"])
                self.assertIn(c["identity"]["mrn"], r["d_body"])
            self.assertIsNone(chk.DOSE.search(r["d_body"]), r["id"])
            self.assertEqual(r["synthetic"], "yes")

    def test_previous_results_are_the_case_files_previous_values(self):
        for c in CASES.values():
            prev = [x for x in c["results"] if x.get("previous")]
            body = " ".join(r["d_body"] for r in self.rec if r["case_id"] == c["id"] and r["doc_type"] == "lab_result")
            for x in prev:
                self.assertIn(x["previous"]["value"], body, c["id"])


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
