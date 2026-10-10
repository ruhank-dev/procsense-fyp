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
        status.pop("10")
        self.use(status)
        _, problems = trace.build()
        self.assertTrue(any(p.startswith("part 10 ") and "not mapped, planned or marked" in p for p in problems), problems)

    def test_a_planned_part_must_point_at_a_real_step(self):
        status = json.loads(self.original.read_text(encoding="utf-8"))
        status["10"] = {"status": "planned", "step": "p0-nonsense-s99"}
        self.use(status)
        _, problems = trace.build()
        self.assertTrue(any("p0-nonsense-s99" in p for p in problems), problems)

    def test_not_used_needs_a_reason(self):
        status = json.loads(self.original.read_text(encoding="utf-8"))
        status["10"] = {"status": "not used"}
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


if __name__ == "__main__":
    unittest.main(verbosity=1)
