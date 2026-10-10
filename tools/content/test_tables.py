"""Meaning checks on the generated content tables (not just 'the quote exists').

    uv run --project tools tools/content/test_tables.py
Each check uses the CSV files in content/ the way the engine will, and compares with examples printed in the walkthrough.
"""
import csv
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
REGISTER = ROOT / "docs" / "research" / "source_register.csv"


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def num(x: str):
    return float(x) if x.strip() else None


def in_range(value: float, row: dict, low_col: str = "v_low", high_col: str = "v_high") -> bool:
    low, high = num(row[low_col]), num(row[high_col])
    return (low is None or value >= low) and (high is None or value <= high)


class News2(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cells = rows("news2_scoring")

    def points(self, parameter: str, value: float, scale: str = "1") -> int:
        hits = [c for c in self.cells if c["scale"] == scale and c["parameter"] == parameter and not c["v_state"] and in_range(value, c)]
        self.assertEqual(len(hits), 1, f"{parameter}={value} matched {len(hits)} cells")
        return int(hits[0]["o_points"])

    def state_points(self, parameter: str, state: str) -> int:
        hits = [c for c in self.cells if c["parameter"] == parameter and c["v_state"] == state]
        self.assertEqual(len(hits), 1, f"{parameter}={state}")
        return int(hits[0]["o_points"])

    def total(self, rr, spo2, on_oxygen, sbp, hr, acvpu, temp) -> int:
        return (self.points("respiratory_rate", rr) + self.points("spo2", spo2) + (2 if on_oxygen else self.state_points("supplemental_oxygen", "room air"))
                + self.points("systolic_bp", sbp) + self.points("heart_rate", hr)
                + (0 if acvpu == "A" else self.state_points("acvpu", "Voice")) + self.points("temperature_c", temp))

    def test_the_books_worked_example_scores_ten(self):
        # Walkthrough 4.9: breathing 26 (3) + oxygen 94% (1) + room air (0) + BP 92 (2) + heart rate 128 (2) + alert (0) + temperature 39.2 (2) = 10
        self.assertEqual(self.total(26, 94, False, 92, 128, "A", 39.2), 10)

    def test_a_calm_planned_patient_scores_zero(self):
        # Part 12 Example Patient A: HR 76, BP 124, RR 14, SpO2 98 on air, 36.7, alert (4.11 'all normal')
        self.assertEqual(self.total(14, 98, False, 124, 76, "A", 36.7), 0)

    def test_the_books_sepsis_pattern_example(self):
        # 4.11 sepsis pattern: HR 128, BP 92/58, RR 26, T 39.2 (SpO2 and alertness not given: assume 96 on air, alert)
        self.assertEqual(self.total(26, 96, False, 92, 128, "A", 39.2), 3 + 0 + 0 + 2 + 2 + 0 + 2)

    def test_every_parameter_is_covered_exactly_once_over_a_wide_range(self):
        spans = {"respiratory_rate": range(0, 61), "spo2": range(40, 101), "systolic_bp": range(30, 301), "heart_rate": range(10, 251)}
        for parameter, values in spans.items():
            for v in values:
                self.points(parameter, float(v))
        for tenth in range(300, 441):
            self.points("temperature_c", tenth / 10)

    def test_scale_two_covers_oxygen_saturation(self):
        scale2 = [c for c in self.cells if c["scale"] == "2"]
        for v in range(60, 101):
            on_air = [c for c in scale2 if c["v_state"] in ("", "on air") and in_range(v, c)]
            self.assertEqual(len(on_air), 1, f"on air {v}")
        for v in range(93, 101):
            on_o2 = [c for c in scale2 if c["v_state"] == "on oxygen" and in_range(v, c)]
            self.assertEqual(len(on_o2), 1, f"on oxygen {v}")
        self.assertEqual(sorted({c["o_points"] for c in scale2}), ["0", "1", "2", "3"])

    def test_scale_two_gives_88_to_92_on_air_zero_where_scale_one_gives_three(self):
        s2 = [c for c in self.cells if c["scale"] == "2" and not c["v_state"] and in_range(90, c)]
        self.assertEqual(int(s2[0]["o_points"]), 0)
        self.assertEqual(self.points("spo2", 90), 3)

    def test_single_parameter_three_is_marked_by_extreme_values(self):
        for parameter, extreme in (("heart_rate", 40), ("respiratory_rate", 8), ("temperature_c", 35.0), ("systolic_bp", 90), ("spo2", 91)):
            self.assertEqual(self.points(parameter, extreme), 3)


class DisplayBands(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bands = rows("vitals_bands")

    def zones(self, parameter: str, value: float) -> set[str]:
        return {b["zone"] for b in self.bands if b["parameter"] == parameter and not b["v_state"] and not b["v_condition"] and in_range(value, b) and b["bound"] == ""}

    def test_the_planned_patients_numbers_are_all_normal(self):
        self.assertEqual(self.zones("heart_rate", 76), {"normal"})
        self.assertEqual(self.zones("respiratory_rate", 14), {"normal"})
        self.assertEqual(self.zones("spo2", 98), {"normal"})
        self.assertEqual(self.zones("temperature_c", 36.7), {"normal"})
        self.assertEqual(self.zones("systolic_bp_low", 124), {"normal"})

    def test_a_conditional_zone_is_kept_apart(self):
        # 'worry: 22 or more with infection' must not be applied to everyone
        conditional = [b for b in self.bands if b["v_condition"]]
        self.assertEqual({(b["parameter"], b["zone"], b["v_condition"]) for b in conditional},
                         {("respiratory_rate", "worry", "infection"), ("bp_high", "emergency", "symptoms")})

    def test_the_books_sepsis_numbers_are_red_or_emergency(self):
        self.assertEqual(self.zones("heart_rate", 128), {"worry"})
        self.assertEqual(self.zones("respiratory_rate", 26), {"emergency"})
        self.assertEqual(self.zones("temperature_c", 39.2), {"worry"})

    def test_temperature_display_zones_differ_from_the_news2_cells_on_purpose(self):
        # WT-I04: display normal ends at 37.9, NEWS2 scores 0 up to 38.0
        self.assertEqual(self.zones("temperature_c", 38.0), {"watch"})
        news2 = [c for c in rows("news2_scoring") if c["parameter"] == "temperature_c" and in_range(38.0, c)]
        self.assertEqual(news2[0]["o_points"], "0")

    def test_every_parameter_has_a_normal_zone(self):
        for parameter in {b["parameter"] for b in self.bands if b["zone"] != "critical" and b["parameter"] != "mean_arterial_pressure"}:
            self.assertTrue(any(b["parameter"] == parameter and b["zone"] == "normal" for b in self.bands), parameter)


class Sepsis(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.rows = rows("sepsis_ng253_tiers")

    def tier(self, news2: int, single3: bool) -> str:
        order = ["very_low", "low", "moderate", "high"]
        best = "very_low"
        for r in self.rows:
            if r["kind"] != "criteria":
                continue
            if r["single3"] == "yes" and not single3:
                continue
            if in_range(news2, r, "v_news2_min", "v_news2_max") and order.index(r["tier"]) > order.index(best):
                best = r["tier"]
        return best

    def test_tiers_follow_the_news2_score(self):
        self.assertEqual(self.tier(0, False), "very_low")
        self.assertEqual(self.tier(1, False), "low")
        self.assertEqual(self.tier(4, False), "low")
        self.assertEqual(self.tier(5, False), "moderate")
        self.assertEqual(self.tier(6, False), "moderate")
        self.assertEqual(self.tier(7, False), "high")
        self.assertEqual(self.tier(12, False), "high")

    def test_a_single_parameter_of_three_raises_the_tier_as_the_book_says(self):
        self.assertEqual(self.tier(3, True), "moderate")
        self.assertEqual(self.tier(4, True), "moderate")
        self.assertEqual(self.tier(5, True), "high")
        self.assertEqual(self.tier(6, True), "high")
        self.assertEqual(self.tier(2, True), "low")

    def test_times_and_intervals_per_tier(self):
        def get(tier, kind, col):
            return [r[col] for r in self.rows if r["tier"] == tier and r["kind"] == kind][0]
        self.assertEqual(get("high", "antibiotics", "v_hours"), "1")
        self.assertEqual(get("moderate", "antibiotics", "v_hours"), "3")
        self.assertEqual(get("low", "antibiotics", "v_hours"), "6")
        self.assertEqual(get("high", "reassess", "d_minutes"), "30")
        self.assertEqual(get("moderate", "reassess", "d_minutes"), "60")


def lab_zone(analyte: str, value: float, sex: str = "") -> set[str]:
    """Zones a value falls in (sex-specific rows apply only to that sex; multiples of baseline/upper limit are skipped)."""
    out = set()
    for b in rows("lab_bands"):
        if b["analyte"] != analyte or b["basis"] or b["v_state"] or (b["sex"] and sex and b["sex"] != sex):
            continue
        low, high = num(b["v_low"]), num(b["v_high"])
        ok = ((low is None) or (value > low if b["bound"] == "above" else value >= low)) and ((high is None) or (value < high if b["bound"] == "below" else value <= high))
        if ok:
            out.add(b["zone"])
    return out


class Labs(unittest.TestCase):
    def test_the_planned_patients_results_are_normal(self):
        # Part 12 Example Patient A has normal bloods; the typical values below sit inside the Part 17 normal bands
        for analyte, value in (("potassium", 4.2), ("sodium", 140), ("bilirubin", 12), ("alt", 25), ("crp", 2), ("platelets", 250), ("wbc", 7.0), ("lactate", 1.0)):
            self.assertIn("normal", lab_zone(analyte, value), (analyte, value))

    def test_the_books_examples_land_in_the_right_zone(self):
        self.assertIn("severe", lab_zone("potassium", 6.5))                # critical list: 6.5 or above
        self.assertIn("significant", lab_zone("potassium", 6.4))           # S1-J: K 6.4 'haemolysed'
        self.assertEqual(lab_zone("potassium", 6.4), {"significant"})
        self.assertIn("severe", lab_zone("bilirubin", 72))                 # 'Bilirubin 72 H' (Step 7)
        self.assertEqual(lab_zone("lactate", 3.8), {"significant"})        # sepsis pattern: lactate 3.8
        self.assertEqual(lab_zone("haemoglobin", 118, "F"), {"mild"})      # 5.28 borderline: Hb 118 in a woman
        self.assertEqual(lab_zone("haemoglobin", 118, "M"), {"mild"})

    def test_a_critical_value_is_never_in_a_normal_band(self):
        crit = rows("critical_results")
        by_name = {"potassium": "potassium", "sodium": "sodium", "crp": "crp", "platelets": "platelets", "lactate": "lactate", "neutrophils": "neutrophils"}
        for c in crit:
            if c["analyte"] not in by_name or c["v_condition"] or c["direction"] == "fall":
                continue
            v = float(c["v_value"])
            self.assertNotIn("normal", lab_zone(c["analyte"], v), c["id"])

    def test_the_gaps_in_part_17_are_known_and_listed(self):
        # Part 17 leaves K 5.4 and CRP 5-9 without a band (the book's own gaps); they are noted in the rows, not filled in
        self.assertEqual(lab_zone("potassium", 5.4), set())
        self.assertEqual(lab_zone("crp", 7), set())
        with open(REGISTER, encoding="utf-8", newline="") as f:
            notes = " ".join(r["notes"] for r in csv.DictReader(f) if r["item"].startswith("Lab band"))
        self.assertIn("leaves 5.4 uncovered", notes)
        self.assertIn("leaves 5–9 uncovered", notes)

    def test_critical_results_cover_the_eleven_tests_of_5_27(self):
        names = {c["analyte"] for c in rows("critical_results")}
        self.assertEqual(names, {"potassium", "sodium", "glucose", "calcium", "creatinine", "crp", "haemoglobin", "platelets", "neutrophils", "inr", "lactate"})

    def test_turnaround_gives_only_what_the_book_gives(self):
        t = {r["test"]: r for r in rows("turnaround")}
        self.assertEqual((t["fbc"]["v_minutes_low"], t["fbc"]["v_minutes_high"]), ("45", "45"))
        self.assertEqual((t["imaging"]["v_minutes_low"], t["imaging"]["v_minutes_high"]), ("60", "120"))
        self.assertEqual(t["other_tests"]["v_minutes_low"], "")

    def test_every_artefact_names_its_effect(self):
        self.assertEqual({r["artefact"] for r in rows("sample_quality")}, {"haemolysed", "drip_arm", "wrong_tube_edta", "mislabelled", "rejected", "clumped"})


class Decisions(unittest.TestCase):
    def test_the_five_outcomes_and_five_end_states_are_the_ones_the_plan_names(self):
        self.assertEqual([r["code"] for r in rows("decision_outcomes")], ["SAFE", "INVESTIGATE", "OPTIMISE", "REVIEW", "EMERGENCY"])
        self.assertEqual([r["code"] for r in rows("end_states")],
                         ["READY_SAFE", "POSTPONED_APPROPRIATELY", "REDIRECTED_APPROPRIATELY", "SAFE_BUT_DELAYED", "SAFETY_FAILURE"])

    def test_every_inventory_rule_d187_to_d205_appears_exactly_once(self):
        # D204 (turnaround) lives in turnaround.csv; the others are in decision_rules.csv
        ids = [r["inventory"] for r in rows("decision_rules")]
        expected = [f"D{n}" for n in range(187, 206) if n != 204]
        self.assertEqual(sorted(ids), sorted(expected))
        self.assertEqual(len(ids), len(set(ids)))
        self.assertTrue(any(r["test"] == "fbc" for r in rows("turnaround")))

    def test_no_threshold_says_cancel(self):
        # the walkthrough decision principle: thresholds make the trainee think or involve someone, never cancel by themselves
        for r in rows("decision_rules"):
            self.assertNotIn("cancel", r["triggers"].lower(), r["id"])

    def test_simulator_triggers_say_they_are_not_guideline_cut_offs(self):
        with open(REGISTER, encoding="utf-8", newline="") as f:
            reg = {r["id"]: r for r in csv.DictReader(f)}
        for r in rows("decision_rules"):
            if r["inventory"] in ("D202", "D203", "D205"):
                row = reg[r["ref"]]
                self.assertEqual(row["evidence_type"], "D", r["id"])
                self.assertTrue("Simulator" in row["notes"] or "simulator" in row["notes"], r["id"])

    def test_fasting_hours_follow_part_9(self):
        h = {r["item"]: r["v_hours"] for r in rows("fasting_rules")}
        self.assertEqual((h["clear_liquids"], h["milk"], h["light_meal"], h["heavy_meal"]), ("2", "6", "6", "8"))

    def test_the_priority_ladder_has_seven_ranks_in_order(self):
        self.assertEqual([int(r["rank"]) for r in rows("priority_ladder")], [1, 2, 3, 4, 5, 6, 7])
        self.assertEqual({r["type"] for r in rows("problem_types")},
                         {"life_threat", "safety_blocker", "time_critical", "pathway_changing", "optimisable", "information_only"})
        self.assertEqual([int(r["priority"]) for r in rows("priority_scale_part_d")], [1, 2, 3, 4])

    def test_test_indications_cover_the_fifteen_rows_of_step_6(self):
        t = {r["test"]: r for r in rows("test_indications")}
        self.assertEqual(len(t), 17)
        self.assertEqual(t["urine_dipstick"]["d_s1_kind"], "not_routine")
        self.assertEqual(t["urine_dipstick"]["d_s2_kind"], "always")
        self.assertEqual(t["pregnancy"]["d_s1_kind"], "conditional")
        self.assertEqual(t["mrcp"]["d_s2_kind"], "not_used")

    def test_the_clash_table_has_the_18_rows_of_14b3(self):
        c = rows("treatment_clashes")
        self.assertEqual(len(c), 18)
        dose_controlled = [r for r in c if "PATIENT-SPECIFIC DOSE" in r["v_tag"]]
        self.assertGreaterEqual(len(dose_controlled), 8)
        self.assertEqual(len({r["clash_key"] for r in c}), 18)


class Scores(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.items = rows("score_items")
        cls.bands = rows("score_bands")

    def pts(self, score, item, value=None, cond=""):
        hits = [r for r in self.items if r["score"] == score and r["item"] == item and (not cond or r["v_condition"] == cond)
                and (value is None or in_range_bound(value, r))]
        self.assertEqual(len(hits), 1, (score, item, value, cond, len(hits)))
        return int(hits[0]["v_points"])

    def band(self, score, total):
        hits = [b for b in self.bands if b["score"] == score and b["band"] != "max" and in_range(total, b, "v_low", "v_high")]
        self.assertEqual(len(hits), 1, (score, total))
        return hits[0]["band"]

    def test_the_books_worked_example_air_is_seven_and_unsure(self):
        # S2-A: man, 19; vomited once; lower-right pain; medium rebound; 37.9; WBC 13.6; neutrophils 82%; CRP 38
        total = (self.pts("air", "vomiting") + self.pts("air", "pain_rlq") + self.pts("air", "rebound_medium")
                 + 0                                         # temperature 37.9 is below 38.5: no points
                 + self.pts("air", "wbc", 13.6) + self.pts("air", "neutrophils_pct", 82) + self.pts("air", "crp", 38))
        self.assertEqual(total, 7)
        self.assertEqual(self.band("air", total), "unsure")

    def test_the_books_worked_example_aas_is_nineteen_and_high(self):
        total = (self.pts("aas", "pain_rlq") + self.pts("aas", "pain_moved") + self.pts("aas", "rlq_tenderness", cond="Everyone else")
                 + self.pts("aas", "guarding_mild") + self.pts("aas", "wbc", 13.6) + self.pts("aas", "neutrophils_pct", 82)
                 + self.pts("aas", "crp_pain_under_24h", 38))
        self.assertEqual(total, 19)
        self.assertEqual(self.band("aas", total), "high")

    def test_the_two_scores_disagree_on_this_patient_as_the_book_says(self):
        self.assertNotEqual(self.band("air", 7), self.band("aas", 19))

    def test_the_maximum_of_each_score_matches_the_books_maximum(self):
        def best(score):
            groups = {}
            for r in self.items:
                if r["score"] == score:
                    key = r["item"].replace("_pain_under_24h", "").replace("_pain_24h_or_more", "").replace("rebound_light", "rebound").replace("rebound_medium", "rebound").replace("rebound_strong", "rebound")
                    key = key.replace("guarding_mild", "guarding").replace("guarding_moderate_or_severe", "guarding")
                    groups[key] = max(groups.get(key, 0), int(r["v_points"]))
            return sum(groups.values())
        maxima = {b["score"]: int(b["v_high"]) for b in self.bands if b["band"] == "max"}
        self.assertEqual({s: best(s) for s in ("alvarado", "air", "aas")}, maxima)
        self.assertEqual(maxima, {"alvarado": 10, "air": 12, "aas": 23})

    def test_every_band_covers_its_score_range_without_gaps(self):
        for score, top in (("alvarado", 10), ("air", 12), ("aas", 23)):
            for total in range(0, top + 1):
                self.band(score, total)

    def test_the_book_notes_that_a_very_high_crp_gets_fewer_aas_points(self):
        self.assertEqual(self.pts("aas", "crp_pain_under_24h", 90), 1)
        self.assertEqual(self.pts("aas", "crp_pain_under_24h", 30), 5)

    def test_tg18_and_asge_facts_are_present(self):
        facts = rows("score_facts")
        groups = {f["group"] for f in facts}
        self.assertEqual(groups, {"tg18_cholecystitis", "tg18_cholangitis", "asge", "asa", "airway", "stopbang", "vte", "sort", "aki"})
        self.assertEqual([f["item"] for f in facts if f["group"] == "asge"], ["cholangitis_first", "high", "intermediate", "low"])
        self.assertEqual(len([f for f in facts if f["group"] == "asa"]), 8)


def in_range_bound(value: float, r: dict) -> bool:
    low, high = num(r["v_low"]), num(r["v_high"])
    if low is not None and (value <= low if r["bound"] == "above" else value < low):
        return False
    return high is None or value <= high


class Medicines(unittest.TestCase):
    TAGS = {"EDUCATIONAL_EXAMPLE": "EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED)", "PATIENT_SPECIFIC": "PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
            "INSTITUTIONAL": "CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL"}

    @classmethod
    def setUpClass(cls):
        cls.meds = rows("medicines")

    def test_every_policy_matches_the_tag_printed_in_the_book_row(self):
        for r in self.meds:
            self.assertIn(r["d_policy"], {"EDUCATIONAL_EXAMPLE", "PATIENT_SPECIFIC", "INSTITUTIONAL", "NONE"}, r["id"])
            if r["d_policy"] in self.TAGS:
                self.assertIn(self.TAGS[r["d_policy"]], r["quote"], r["id"])
            else:
                for tag in self.TAGS.values():
                    self.assertNotIn(tag, r["quote"], f"{r['id']} has a tag but policy NONE")

    def test_patient_specific_rows_carry_no_dose_text(self):
        # the trainee must never see a patient-specific dose: no example text and no digits in what is shown
        ps = [r for r in self.meds if r["d_policy"] == "PATIENT_SPECIFIC"]
        self.assertGreaterEqual(len(ps), 6)
        for r in ps:
            self.assertEqual(r["v_example"], "", r["id"])
            self.assertEqual(r["v_source"], "", r["id"])
            for col in ("v_for", "v_care"):
                self.assertFalse(any(ch.isdigit() for ch in r[col]), f"{r['id']} {col}: {r[col]}")

    def test_long_acting_insulin_keeps_the_20_percent_out_of_the_content_columns(self):
        r = [x for x in self.meds if x["group"] == "long_acting_insulin"][0]
        self.assertEqual(r["v_timing"], "")
        self.assertEqual(r["d_policy"], "PATIENT_SPECIFIC")

    def test_every_example_dose_names_its_source(self):
        examples = [r for r in self.meds if r["d_policy"] == "EDUCATIONAL_EXAMPLE"]
        self.assertGreaterEqual(len(examples), 9)
        for r in examples:
            self.assertTrue(r["v_example"] and r["v_source"], r["id"])
            self.assertIn(r["v_source"], r["quote"], r["id"])

    def test_only_example_rows_have_example_text(self):
        for r in self.meds:
            if r["d_policy"] != "EDUCATIONAL_EXAMPLE":
                self.assertEqual(r["v_example"], "", r["id"])

    def test_the_four_drug_checks_and_the_role_rules_exist(self):
        checks = rows("drug_checks")
        self.assertEqual([c["name"] for c in checks if c["kind"] == "check"], ["allergy", "kidneys_liver", "pregnancy", "blood_thinner"])
        self.assertEqual({c["name"] for c in checks if c["kind"] == "role"}, {"role_convention", "never_prescribe"})
        self.assertEqual({c["name"] for c in checks if c["kind"] == "dose_policy"}, {"PATIENT_SPECIFIC", "EDUCATIONAL_EXAMPLE", "INSTITUTIONAL"})

    def test_the_medicine_groups_named_in_the_plan_are_all_present(self):
        groups = {r["group"] for r in self.meds}
        for needed in ("paracetamol", "nsaids", "opioids", "antiemetics", "iv_fluids", "warfarin", "aspirin", "sglt2_inhibitors", "glp1_agonists", "long_acting_insulin",
                       "ace_inhibitors_arbs", "beta_blockers", "diuretics", "steroid_tablets", "combined_contraceptive_pill_hrt", "herbal_remedies",
                       "epilepsy_parkinsons_medicines", "vte_prophylaxis", "adrenaline_anaphylaxis", "hyperkalaemia_treatment", "naloxone_opioid_overdose"):
            self.assertIn(needed, groups)


class Structure(unittest.TestCase):
    def test_the_who_checklist_has_its_three_pauses_and_five_extensions_kept_apart(self):
        who = rows("who_checklist")
        by = {}
        for r in who:
            by.setdefault(r["phase"], []).append(r)
        self.assertEqual({k: len(v) for k, v in by.items()}, {"sign_in": 7, "time_out": 7, "sign_out": 5, "extension": 5})
        self.assertTrue(all(r["extension"] == "yes" for r in by["extension"]))
        self.assertTrue(all(r["extension"] == "no" for p in ("sign_in", "time_out", "sign_out") for r in by[p]))
        self.assertEqual([int(r["order"]) for r in by["sign_in"]], list(range(1, 8)))

    def test_who_wording_uses_the_2009_form_the_book_corrected_to(self):
        items = {r["v_item"] for r in rows("who_checklist")}
        self.assertIn("Confirm the patient's name, procedure, and where the incision will be made", items)
        self.assertIn("Is the anaesthesia machine and medication check complete?", items)

    def test_the_stop_items_of_sign_in_are_critical(self):
        critical = [r for r in rows("who_checklist") if r["d_critical"] == "yes"]
        self.assertEqual([r["order"] for r in critical], ["1", "3", "4"])
        self.assertTrue(all(r["phase"] == "sign_in" and "STOP" in r["v_if_no"] for r in critical))
        self.assertTrue(all(r["d_critical"] == "no" for r in rows("who_checklist") if "STOP" not in r["v_if_no"]))

    def test_every_step_is_there_and_answers_what_are_we_doing(self):
        steps = rows("steps_21_questions")
        self.assertEqual(len(steps), 270)
        per = {}
        for r in steps:
            per.setdefault(int(r["step"]), []).append(r)
        self.assertEqual(sorted(per), list(range(1, 16)))
        for n, rs in per.items():
            self.assertEqual(rs[0]["question"], "What are we doing?", n)
            self.assertEqual(len({r["question_no"] for r in rs}), len(rs), n)
        self.assertEqual(len(per[15]), 5)                    # Step 15 answers 5 of the 21 questions
        self.assertEqual(len(per[1]), 21)                    # Step 1 answers all of them

    def test_step_answers_no_longer_swallow_the_source_lines(self):
        for r in rows("steps_21_questions"):
            self.assertNotIn("Where this comes from", r["v_answer"], r["id"])
            self.assertNotIn("Clinician validation:", r["v_answer"], r["id"])

    def test_both_flow_maps_have_twenty_numbered_stages_with_every_field(self):
        flow = rows("flow_stages")
        for scenario in ("S1", "S2"):
            st = [r for r in flow if r["scenario"] == scenario]
            self.assertEqual([int(r["stage"]) for r in st], list(range(1, 21)))
            for r in st:
                for col in ("v_medical_detail", "v_trainee_action", "v_sees", "v_normal_pathway", "v_abnormal_branches", "v_escalation", "v_equipment_response", "v_if_missed", "v_source"):
                    self.assertTrue(r[col].strip(), f"{r['id']} {col}")

    def test_a_flow_stage_matches_the_book(self):
        r = [x for x in rows("flow_stages") if x["id"] == "S1.03"][0]
        self.assertEqual(r["title"], "Observations")
        self.assertEqual(r["v_normal_pathway"], "NEWS2 0–1, BP <180/110")

    def test_player_view_vr_cards_and_glossary_are_complete(self):
        pv = rows("player_view")
        self.assertEqual([r["stage"] for r in pv], [str(n) for n in range(1, 13)] + ["13–14"])
        for r in pv:
            for col in ("v_on_screen", "v_patient_says", "v_monitor", "v_interaction", "v_information", "v_decision", "v_correct", "v_incorrect", "v_missed", "v_escalation"):
                self.assertTrue(r[col].strip(), f"{r['id']} {col}")
        vr = rows("vr_cards")
        self.assertEqual([r["card"] for r in vr], [f"C.{n}" for n in range(1, 25)])
        for r in vr:
            for col, value in r.items():
                if col.startswith("v_"):
                    self.assertTrue(value.strip(), f"{r['id']} {col}")
        gl = rows("glossary")
        self.assertGreaterEqual(len(gl), 130)
        self.assertTrue(gl[0]["v_entry"].startswith("AAS "))


class WhatIfs(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.w = rows("whatif_library")

    def test_there_are_47_whatifs_and_the_part_9_heading_is_not_one_of_them(self):
        self.assertEqual(len(self.w), 47)
        self.assertNotIn("What if the patient ate recently? (S1)", {r["title"] for r in self.w})
        self.assertEqual(len({r["title"] for r in self.w}), 47)

    def test_every_whatif_has_all_eleven_fields(self):
        for r in self.w:
            for col, value in r.items():
                if col.startswith("v_"):
                    self.assertTrue(value.strip(), f"{r['id']} {col}")

    def test_a_whatif_does_not_swallow_the_next_section_heading(self):
        for r in self.w:
            for col, value in r.items():
                if col.startswith("v_"):
                    self.assertNotIn("## 14.", value, f"{r['id']} {col}")

    def test_sections_and_trigger_types(self):
        sections = {}
        for r in self.w:
            sections[r["section"]] = sections.get(r["section"], 0) + 1
        self.assertEqual(sections, {"14.1": 11, "14.2": 13, "14.3": 1, "14.4": 7, "14.5": 2, "14.6": 4, "14.7": 2, "14.8": 3, "14.9": 4})
        self.assertEqual({r["d_trigger_type"] for r in self.w}, {"value_threshold", "trainee_action", "omission", "system"})

    def test_low_blood_pressure_matches_the_book(self):
        r = [x for x in self.w if x["title"] == "What if blood pressure is too low? (top number 90 or less)"][0]
        self.assertEqual(r["v_trigger"], "Monitor shows 86/52; patient pale, sweaty, dizzy.")
        self.assertIn("Call for help; lie flat", r["v_correct"])
        self.assertIn("Giving a BP tablet or opioid", r["v_incorrect"])


def news2_total(rr, spo2, on_oxygen, sbp, hr, confused, temp) -> int:
    cells = [c for c in rows("news2_scoring") if c["scale"] == "1"]

    def pts(parameter, value):
        hits = [c for c in cells if c["parameter"] == parameter and not c["v_state"] and in_range(value, c)]
        assert len(hits) == 1, (parameter, value)
        return int(hits[0]["o_points"])
    return (pts("respiratory_rate", rr) + pts("spo2", spo2) + (2 if on_oxygen else 0) + pts("systolic_bp", sbp) + pts("heart_rate", hr)
            + (3 if confused else 0) + pts("temperature_c", temp))


class Cases(unittest.TestCase):
    P0 = ["VAR-S1-A", "VAR-S1-PB", "VAR-S1-E", "VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J",
          "VAR-S2-PC", "VAR-S2-PD", "VAR-S2-B", "VAR-S2-D", "VAR-S2-F", "VAR-S2-G", "STK-S1-D", "STK-S2-B"]

    @classmethod
    def setUpClass(cls):
        cls.seeds = rows("case_seeds")

    def test_the_17_p0_cases_are_present_and_nothing_else_is_p0(self):
        p0 = [r["case_id"] for r in self.seeds if r["tier"] == "P0"]
        self.assertEqual(sorted(p0), sorted(self.P0))
        self.assertEqual(len(p0), 17)

    def test_the_other_tiers_match_the_plan(self):
        p1 = [r["case_id"] for r in self.seeds if r["tier"] == "P1"]
        p2 = [r["case_id"] for r in self.seeds if r["tier"] == "P2"]
        self.assertEqual(len(p1), 4 + 6 + 6)     # S1-I,K,L,M + S2-E,H,I,J,K,L + six stacked cases
        self.assertEqual(p2, [f"SC-{n}" for n in range(1, 21)])

    def test_each_seed_carries_the_walkthrough_label_and_its_story(self):
        by = {r["case_id"]: r for r in self.seeds}
        self.assertEqual(by["VAR-S1-E"]["wt_label"], "Patient S1-E")
        self.assertIn("Mrs Rabia, 48", by["VAR-S1-E"]["v_text"])
        self.assertIn("Mrs Farzana Ahmed, 61", by["VAR-S1-PB"]["v_text"])
        self.assertIn("Mr Hamza Ali, 19", by["VAR-S2-PC"]["v_text"])
        self.assertIn("Mr Usman, 34", by["VAR-S2-PD"]["v_text"])
        self.assertIn("Mr Kamran, 50", by["VAR-S1-J"]["v_text"])

    def test_a_seed_story_does_not_run_into_the_next_one(self):
        by = {r["case_id"]: r for r in self.seeds}
        self.assertNotIn("Patient S1-B", by["VAR-S1-E"]["v_text"])
        self.assertNotIn("Example Patient D", by["VAR-S2-PC"]["v_text"])
        self.assertNotIn("# Part 13", by["VAR-S1-I"]["v_text"])

    def test_the_book_prints_news2_totals_that_the_table_reproduces(self):
        # Hamza at triage: HR 108 scores 1, everything else 0, total 1 (Part 13)
        self.assertEqual(news2_total(18, 98, False, 128, 108, False, 37.9), 1)
        # Mrs Rabia (S1-E): HR 108 (1) + RR 20 (0) + T 38.4 (1) = 2 (Part 12)
        self.assertEqual(news2_total(20, 97, False, 118, 108, False, 38.4), 2)
        # Usman, three of his four rows (Part 13)
        self.assertEqual(news2_total(20, 97, False, 124, 102, False, 38.3), 2)
        self.assertEqual(news2_total(22, 96, False, 112, 116, False, 38.9), 5)
        self.assertEqual(news2_total(26, 94, False, 92, 128, False, 39.2), 10)

    def test_the_last_usman_row_disagrees_with_the_table_and_is_recorded(self):
        row = [r for r in rows("case_timelines") if r["time"] == "15:45"][0]
        computed = news2_total(int(row["v_rr"]), int(row["v_spo2"]), False, int(row["v_bp"].split("/")[0]), int(row["v_hr"]), True, float(row["v_temp"]))
        self.assertEqual(row["v_news2_book"], "16")
        self.assertEqual(computed, 17)                         # ISS-0016: the book prints 16, the table gives 17
        with open(ROOT / "docs" / "research" / "issues.md", encoding="utf-8") as f:
            self.assertIn("ISS-0016", f.read())

    def test_the_timeline_follows_the_deterioration(self):
        t = rows("case_timelines")
        self.assertEqual([r["time"] for r in t], ["14:00", "14:45", "15:15", "15:45"])
        self.assertEqual([int(r["v_hr"]) for r in t], sorted(int(r["v_hr"]) for r in t))
        self.assertEqual([int(r["v_news2_book"]) for r in t], sorted(int(r["v_news2_book"]) for r in t))

    def test_measurement_tables_exist_for_the_example_patients(self):
        m = rows("case_measurements")
        self.assertEqual({r["case_id"] for r in m}, {"VAR-S1-A", "VAR-S1-PB", "VAR-S2-PC", "VAR-S2-PD", "VAR-S1-E"})
        bili = [r for r in m if r["case_id"] == "VAR-S1-PB" and r["name"] == "bilirubin"][0]
        self.assertEqual(bili["v_value"], "72")
        # the book's own zone words agree with our lab bands: bilirubin 72 is above the 68 line
        self.assertIn("severe", lab_zone("bilirubin", 72))


class Register(unittest.TestCase):
    def test_unverified_rows_are_honest_and_verified_rows_name_their_source(self):
        with open(REGISTER, encoding="utf-8", newline="") as f:
            reg = list(csv.DictReader(f))
        generated = [r for r in reg if 100 <= int(r["id"][3:]) < 10000]
        self.assertGreater(len(generated), 100)
        for r in generated:
            if r["status"] == "VERIFIED" and r["evidence_type"] == "A":
                self.assertTrue(r["source_url"].startswith("https://"), r["id"])
                self.assertTrue("opened" in r["notes"] or "Checked 10 Oct 2026" in r["notes"], r["id"])
            if r["status"] == "UNVERIFIED":
                self.assertTrue("not been opened" in r["notes"] or "NOT confirmed" in r["notes"], r["id"])


class LabelSafety(unittest.TestCase):
    """openFDA label wording (DT-060): reference wording only, never a dose."""

    DOSE = __import__("re").compile(r"\d+(?:\.\d+)?\s?(?:mg|mcg|g|ml|mL|units?|IU|mEq|mmol|%)|mg/|units/", __import__("re").I)

    def test_every_row_belongs_to_a_medicine_group_and_has_wording(self):
        meds = {r["id"] for r in rows("medicines")}
        got = rows("drug_label_safety")
        self.assertGreaterEqual(len(got), 20)
        for r in got:
            self.assertIn(r["medicine_id"], meds)
            self.assertTrue(r["d_contraindications"] or r["d_boxed_warning"], r["id"])
            self.assertIn("set id", r["d_label"])

    def test_no_dose_rate_or_strength_is_imported(self):
        for r in rows("drug_label_safety"):
            for col in ("d_contraindications", "d_boxed_warning"):
                self.assertIsNone(self.DOSE.search(r[col]), f"{r['id']} {col}: {r[col][:80]}")

    def test_the_known_safety_facts_are_there(self):
        by = {r["medicine_id"]: r for r in rows("drug_label_safety")}
        self.assertIn("hypersensitivity", by["paracetamol"]["d_contraindications"].lower())
        self.assertIn("acidosis", (by["metformin"]["d_contraindications"] + by["metformin"]["d_boxed_warning"]).lower())
        self.assertIn("bleeding", (by["warfarin"]["d_boxed_warning"] + by["doacs"]["d_contraindications"]).lower())


class TypeAChecks(unittest.TestCase):
    def test_teaching_script_claims_each_have_source_and_level(self):
        import csv
        with open(ROOT / "docs" / "research" / "teaching_script_check.csv", encoding="utf-8", newline="") as f:
            rows = list(csv.DictReader(f))
        self.assertGreaterEqual(len(rows), 50)
        for r in rows:
            self.assertIn(r["level"], ("primary", "secondary", "design"), r["claim_id"])
            self.assertTrue(r["source"].strip() and r["result"].strip(), r["claim_id"])
            if r["level"] != "design":
                self.assertTrue(r["url"].startswith("https://"), r["claim_id"])

    """The second look at type A rows (tools/content/tables/type_a_checks.py): no row is VERIFIED without a level and a V-id that exists in the log."""

    def test_every_verified_claim_cites_a_logged_claim_with_a_url(self):
        import re
        log = (ROOT / "docs/research/clinical_verification.md").read_text(encoding="utf-8")
        with open(REGISTER, encoding="utf-8", newline="") as f:
            reg = list(csv.DictReader(f))
        cited = [r for r in reg if "Checked 10 Oct 2026 (" in r["notes"]]
        self.assertGreater(len(cited), 150)
        for r in cited:
            m = re.search(r"Checked 10 Oct 2026 \((primary|secondary), (V-\d\d)", r["notes"])
            self.assertTrue(m, r["id"])
            self.assertIn(f"| {m.group(2)} |", log, r["id"])
            self.assertEqual(r["status"], "VERIFIED")
            self.assertTrue(r["source_url"].startswith("https://"), r["id"])

    def test_no_register_row_is_left_unverified(self):
        with open(REGISTER, encoding="utf-8", newline="") as f:
            reg = list(csv.DictReader(f))
        self.assertEqual([r["id"] for r in reg if r["status"] == "UNVERIFIED"], [])

    def test_the_rows_still_open_say_why(self):
        with open(REGISTER, encoding="utf-8", newline="") as f:
            reg = list(csv.DictReader(f))
        left = [r for r in reg if r["evidence_type"] == "A" and r["status"] == "UNVERIFIED"]
        self.assertLess(len(left), 100)
        for r in left:
            self.assertIn("NOT confirmed", r["notes"], r["id"])


if __name__ == "__main__":
    unittest.main(verbosity=1)
