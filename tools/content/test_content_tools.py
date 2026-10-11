"""Tests for the content tools: the walkthrough extractor, the traceability generator and the CSV reference check.

    uv run --project tools tools/content/test_content_tools.py
Standard-library unittest; CI runs it as "content tools tests".
"""
import copy
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import checks  # noqa: E402
import extract_walkthrough as ex  # noqa: E402
import trace  # noqa: E402

REGISTER = {"SR-0001": {}, "SR-0002": {}}


class ExtractorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = ex.extract()

    def test_the_walkthrough_has_the_expected_counts(self):
        self.assertEqual(ex.problems(self.data), [])

    def test_flow_stages_have_all_ten_fields(self):
        for scenario in ("S1", "S2"):
            self.assertEqual(len(self.data["flow"][scenario]), 20)
            for stage in self.data["flow"][scenario]:
                self.assertEqual(stage["missing_fields"], [], f"{scenario} stage {stage['number']}")

    def test_a_flow_stage_matches_the_book(self):
        stage = self.data["flow"]["S1"][2]   # S1 stage 3 as printed in Part F
        self.assertEqual(stage["title"], "Observations")
        self.assertEqual(stage["fields"]["trainee_action"], "Attach devices; count RR; repeat abnormal readings")
        self.assertEqual(stage["fields"]["if_missed"], "Unexplained abnormality surfaces at Sign In")

    def test_step_one_answers_are_read_without_the_page_footers(self):
        answer = self.data["steps"][0]["questions"]["What does the trainee actually do?"]
        self.assertTrue(answer.startswith("1\\. Introduce yourself"))
        self.assertNotIn("ProcSense Medical Walkthrough v5.1", answer)

    def test_a_missing_what_if_is_reported(self):
        data = copy.deepcopy(self.data)
        data["what_ifs"].pop()
        self.assertTrue(any("what-ifs" in p for p in ex.problems(data)))

    def test_a_missing_flow_stage_is_reported(self):
        data = copy.deepcopy(self.data)
        data["flow"]["S2"].pop()
        self.assertTrue(any("flow S2" in p for p in ex.problems(data)))

    def test_a_step_losing_an_answer_is_reported(self):
        data = copy.deepcopy(self.data)
        data["steps"][3]["questions"].pop("What are we doing?")
        self.assertTrue(any("step answers" in p for p in ex.problems(data)))


class TraceTests(unittest.TestCase):
    def setUp(self):
        self.original = trace.STATUS
        self.tmp = tempfile.TemporaryDirectory()

    def tearDown(self):
        trace.STATUS = self.original
        self.tmp.cleanup()

    def use(self, status: dict):
        path = Path(self.tmp.name) / "status.json"
        path.write_text(json.dumps(status), encoding="utf-8")
        trace.STATUS = path

    def test_the_real_status_file_accounts_for_every_part(self):
        table, problems = trace.build()
        self.assertEqual(problems, [])
        self.assertEqual(len(table), 29)

    def test_a_part_that_is_nowhere_fails_and_is_named(self):
        status = json.loads(self.original.read_text(encoding="utf-8"))
        status.pop("B")
        self.use(status)
        _, problems = trace.build()
        self.assertTrue(any(p.startswith("part B ") and "not mapped, planned or marked" in p for p in problems), problems)

    def test_a_planned_part_must_point_at_a_real_step(self):
        status = json.loads(self.original.read_text(encoding="utf-8"))
        status["B"] = {"status": "planned", "step": "p0-nonsense-s99"}
        self.use(status)
        _, problems = trace.build()
        self.assertTrue(any("p0-nonsense-s99" in p for p in problems), problems)

    def test_not_used_needs_a_reason(self):
        status = json.loads(self.original.read_text(encoding="utf-8"))
        status["B"] = {"status": "not used"}
        self.use(status)
        _, problems = trace.build()
        self.assertTrue(any("needs a reason" in p for p in problems), problems)

    def test_a_part_the_book_does_not_have_is_reported(self):
        status = json.loads(self.original.read_text(encoding="utf-8"))
        status["99"] = {"status": "not used", "reason": "x"}
        self.use(status)
        _, problems = trace.build()
        self.assertTrue(any("'99'" in p for p in problems), problems)

    def test_register_references_name_their_parts(self):
        self.assertEqual(trace.parts_in("WT Part 12 Example Patient A, p.104"), {"12"})
        self.assertEqual(trace.parts_in("WT Part 11 Step 2; Part 12 Scene 2"), {"11", "12"})
        self.assertEqual(trace.parts_in("WT Part E, BP cuff row, p.201"), {"E"})
        self.assertEqual(trace.parts_in("Parts F and G, scenario master maps"), {"F", "G"})
        self.assertEqual(trace.parts_in("Part 7.1 table"), {"7"})
        self.assertEqual(trace.parts_in(""), set())

    def test_the_committed_traceability_file_is_current(self):
        table, _ = trace.build()
        self.assertEqual(trace.OUT.read_text(encoding="utf-8").replace("\r\n", "\n"), trace.render(table))


class CsvReferenceTests(unittest.TestCase):
    def check(self, text: str) -> list[str]:
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "table.csv"
            path.write_text(text, encoding="utf-8")
            return checks.csv_problems(path, REGISTER)

    def test_every_row_with_a_known_ref_passes(self):
        self.assertEqual(self.check("name,ref\nA,SR-0001\nB,SR-0001;SR-0002\n"), [])

    def test_a_table_without_a_ref_column_fails(self):
        self.assertIn("needs a 'ref' column", self.check("name,value\nA,1\n")[0])

    def test_a_row_without_a_ref_fails_with_its_line(self):
        problems = self.check("name,ref\nA,SR-0001\nB,\n")
        self.assertEqual(problems, ["table.csv line 3: no source reference"])

    def test_a_ref_that_is_not_in_the_register_fails(self):
        problems = self.check("name,ref\nA,SR-9999\n")
        self.assertEqual(problems, ["table.csv line 2: ref SR-9999 not in register"])


class ExistingChecksStillCatchMissingRefs(unittest.TestCase):
    def test_a_clinical_value_without_a_source_reference_fails(self):
        doc = {"id": "VAR-X", "baseline": {"heart_rate": 76}, "refs": {}}
        self.assertTrue(any("no source reference for 'baseline.heart_rate'" in p for p in checks.case_problems(doc, REGISTER)))

    def test_a_ref_covers_the_value_under_it(self):
        doc = {"id": "VAR-X", "baseline": {"heart_rate": 76}, "refs": {"baseline.heart_rate": "SR-0001"}}
        self.assertEqual(checks.case_problems(doc, REGISTER), [])


class TreatmentChecks(unittest.TestCase):
    """content/treatments.json: the dose policy (DOSE_REQUIRES_REVIEWER) and the explicit checks (p0-clinical-logic-s10)."""

    @classmethod
    def setUpClass(cls):
        root = Path(__file__).resolve().parents[2]
        cls.doc = checks.load_json(root / "content" / "treatments.json")
        cls.register = checks.load_register(root / "docs" / "research" / "source_register.csv")
        import csv
        with open(root / "content" / "medicines.csv", encoding="utf-8", newline="") as f:
            cls.medicines = {r["id"] for r in csv.DictReader(f)}

    def problems(self, doc):
        return checks.treatment_problems(doc, self.register, self.medicines)

    def treatment(self, doc, tid):
        return next(t for t in doc["treatments"] if t["id"] == tid)

    def test_the_real_library_is_clean(self):
        self.assertEqual(self.problems(self.doc), [])

    def test_a_dose_without_a_reviewer_is_an_error(self):
        doc = copy.deepcopy(self.doc)
        self.treatment(doc, "TR-PARACETAMOL")["dose_display"]["reviewer"] = ""
        self.assertTrue(any("DOSE_REQUIRES_REVIEWER" in p for p in self.problems(doc)))

    def test_a_dose_on_a_patient_specific_treatment_is_an_error(self):
        doc = copy.deepcopy(self.doc)
        self.treatment(doc, "TR-OPIOID")["dose_display"] = {"text": "a number", "source": "x", "reviewer": "y"}
        self.assertTrue(any("only an EDUCATIONAL_EXAMPLE" in p for p in self.problems(doc)))

    def test_a_treatment_with_no_listed_checks_is_an_error(self):
        doc = copy.deepcopy(self.doc)
        self.treatment(doc, "TR-OXYGEN")["required_checks"] = []
        self.assertTrue(any("none are implied" in p for p in self.problems(doc)))

    def test_an_unknown_check_effect_or_prerequisite_is_an_error(self):
        doc = copy.deepcopy(self.doc)
        t = self.treatment(doc, "TR-OPIOID")
        t["required_checks"].append("vibes"); t["effects"].append("EFF-MAGIC"); t["requires"].append("central_line")
        found = " ".join(self.problems(doc))
        self.assertIn("unknown check vibes", found)
        self.assertIn("unknown effect EFF-MAGIC", found)
        self.assertIn("unknown prerequisite central_line", found)

    def test_a_missing_register_row_or_medicine_row_is_an_error(self):
        doc = copy.deepcopy(self.doc)
        self.treatment(doc, "TR-NSAID")["refs"].append("SR-9999")
        self.treatment(doc, "TR-NSAID")["medicine"]["id"] = "not_a_medicine"
        found = " ".join(self.problems(doc))
        self.assertIn("SR-9999", found)
        self.assertIn("names no row of medicines.csv", found)

    def test_an_undefined_allergy_group_or_a_missing_workflow_register_row_is_an_error(self):
        doc = copy.deepcopy(self.doc)
        self.treatment(doc, "TR-ABX-S2")["allergen_group"] = "latex"
        doc["workflow"]["ref"] = "SR-9998"
        found = " ".join(self.problems(doc))
        self.assertIn("allergy group latex is not defined", found)
        self.assertIn("SR-9998", found)

    def test_the_restriction_check_and_the_restriction_list_go_together(self):
        doc = copy.deepcopy(self.doc)
        self.treatment(doc, "TR-NSAID")["restricted_by"] = []
        self.assertTrue(any("go together" in p for p in self.problems(doc)))


if __name__ == "__main__":
    unittest.main(verbosity=1)
