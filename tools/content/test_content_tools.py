"""Tests for the content tools: the walkthrough extractor, the traceability generator and the CSV reference check.

    uv run --project tools tools/content/test_content_tools.py
Standard-library unittest; CI runs it as "content tools tests".
"""
import copy
import csv
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


class RegisterAuditTests(unittest.TestCase):
    """The register audit (12 Oct 2026): ids, blocks and provenance."""

    def setUp(self):
        import register_audit
        self.audit = register_audit
        self.tmp = tempfile.TemporaryDirectory()
        self.original = register_audit.REGISTER

    def tearDown(self):
        self.audit.REGISTER = self.original
        self.tmp.cleanup()

    def plant(self, edit):
        rows = list(csv.reader(open(self.original, encoding="utf-8", newline="")))
        edit(rows)
        path = Path(self.tmp.name) / "register.csv"
        with open(path, "w", encoding="utf-8", newline="") as f:
            csv.writer(f, lineterminator=chr(10)).writerows(rows)
        self.audit.REGISTER = path
        return self.audit.problems()

    def test_the_real_register_has_no_problem_and_the_document_is_current(self):
        self.assertEqual(self.audit.problems(), [])
        self.assertEqual(self.audit.OUT.read_text(encoding="utf-8").replace(chr(13) + chr(10), chr(10)), self.audit.render())

    def test_a_duplicate_id_is_found(self):
        found = self.plant(lambda rows: rows.append(list(rows[1])))
        self.assertTrue(any("duplicate id" in p for p in found), found)

    def test_a_hand_written_row_outside_every_documented_range_is_found(self):
        def add(rows):
            row = list(rows[-1]); row[0] = "SR-3650"; rows.append(row)
        found = self.plant(add)
        self.assertTrue(any("SR-3650" in p and "documented range" in p for p in found), found)

    def test_a_verified_A_row_without_a_source_or_a_claim_is_found(self):
        def weaken(rows):
            for r in rows:
                if r[0] == "SR-2850":
                    r[7] = ""
                    r[13] = "Looked at it."
        found = self.plant(weaken)
        self.assertTrue(any("SR-2850" in p and "https" in p for p in found), found)
        self.assertTrue(any("SR-2850" in p and "claim id" in p for p in found), found)

    def test_an_unknown_status_and_an_unlisted_awaiting_row_are_found(self):
        def edit(rows):
            for r in rows:
                if r[0] == "SR-2854":
                    r[9] = "MAYBE"
                if r[0] == "SR-2850":
                    r[9] = "AWAITING_CLINICIAN"; r[13] = "A clinician is asked."
        found = self.plant(edit)
        self.assertTrue(any("SR-2854" in p and "unknown status" in p for p in found), found)
        self.assertTrue(any("SR-2850" in p and "clinician_signoff" in p for p in found), found)


class ContentLibraryChecks(unittest.TestCase):
    """The shared schema, the content packages per mode and the plan tests T-CL-1, T-CL-4 and T-CL-5 (p0-case-library-s01)."""

    @classmethod
    def setUpClass(cls):
        import package
        cls.package = package
        cls.root = Path(__file__).resolve().parents[2]
        cls.case_schema = checks.load_json(cls.root / "content" / "schema" / "case_file.schema.json")
        cls.case = checks.load_json(cls.root / "content" / "case_files" / "VAR-S1-A.json")

    def test_T_CL_4_a_schema_violation_is_reported_with_its_path(self):
        doc = copy.deepcopy(self.case)
        doc["identity"]["age"] = "forty"
        found = checks.schema_errors(doc, self.case_schema)
        self.assertTrue(any(p.startswith("identity/age") for p in found), found)

    def test_the_status_list_comes_from_the_shared_schema(self):
        doc = copy.deepcopy(self.case)
        doc["status"] = "NEARLY_APPROVED"
        found = checks.schema_errors(doc, self.case_schema)
        self.assertTrue(any(p.startswith("status") and "NEARLY_APPROVED" in p for p in found), found)

    def test_T_CL_1_a_dangling_reference_is_an_error(self):
        card = checks.load_json(self.root / "content" / "cases" / "VAR-S1-A.json")
        card["refs"]["history[0].answer"] = "SR-9999"
        register = checks.load_register(self.root / "docs" / "research" / "source_register.csv")
        self.assertTrue(any("SR-9999" in p for p in checks.case_problems(card, register)))

    def test_the_assessment_package_holds_no_case_file_that_is_not_approved_for_assessment(self):
        shipped, left_out = self.package.plan("assessment")
        self.assertEqual([p.name for p in shipped if p.parent.name == "case_files"], [])
        self.assertEqual(len(left_out), len(list((self.root / "content" / "case_files").glob("*.json"))))   # every case file, whatever their number

    def test_the_practice_package_leaves_out_exactly_the_case_files_not_yet_approved(self):
        _, left_out = self.package.plan("practice")
        self.assertTrue(left_out)
        self.assertTrue(all(item["status"] in ("DRAFT", "CLINICAL_REVIEW") for item in left_out))
        _, dev_left_out = self.package.plan("dev")
        self.assertEqual(dev_left_out, [])

    def test_the_package_manifest_has_the_entries_of_the_repo_manifest_and_a_version(self):
        with tempfile.TemporaryDirectory() as tmp:
            m = self.package.write("practice", Path(tmp) / "c")
            repo = checks.load_json(self.root / "content" / "manifest.json")
            some = next(iter(m["entries"]))
            self.assertEqual(sorted(m["entries"][some]), sorted(repo["entries"][some]))
            self.assertRegex(m["content_version"], r"^[0-9a-f]{12}-practice$")
            self.assertEqual(self.package.verify(Path(tmp) / "c", "practice"), [])

    def test_T_CL_5_a_changed_or_stray_file_in_a_package_is_found(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "c"
            self.package.write("assessment", out)
            (out / "fasting" / "fasting.json").write_text("{}", encoding="utf-8")
            (out / "stray.json").write_text("{}", encoding="utf-8")
            found = " ".join(self.package.verify(out, "assessment"))
            self.assertIn("fasting/fasting.json: does not match its hash", found)
            self.assertIn("stray.json: in the folder but not in the manifest", found)

    def test_a_draft_case_file_slipped_into_a_practice_package_is_found(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "c"
            self.package.write("practice", out)
            draft = checks.load_json(self.root / "content" / "case_files" / "VAR-S1-B.json")
            self.assertEqual(draft["status"], "DRAFT")
            (out / "case_files" / "VAR-S1-B.json").write_text(json.dumps(draft), encoding="utf-8")
            found = " ".join(self.package.verify(out, "practice"))
            self.assertIn("a case file this mode may not ship", found)


class SessionLogSchemaChecks(unittest.TestCase):
    """The published session-file schema against a file the engine really wrote (p1-backend-s02), with a validator that is not the server's own."""

    @classmethod
    def setUpClass(cls):
        from jsonschema import Draft202012Validator
        root = Path(__file__).resolve().parents[2]
        schema = checks.load_json(root / "content" / "schema" / "session_log.schema.json")
        cls.header = Draft202012Validator({"$ref": "#/$defs/header", "$defs": schema["$defs"]})
        cls.entry = Draft202012Validator({"$ref": "#/$defs/entry", "$defs": schema["$defs"]})
        lines = (root / "server" / "tests" / "fixtures" / "golden_session_v1.ndjson").read_text(encoding="utf-8").split("\n")
        cls.head = json.loads(lines[0])["header"]
        cls.entries = [json.loads(line) for line in lines[1:] if line]

    def test_the_engines_header_and_every_action_validate(self):
        self.assertEqual(list(self.header.iter_errors(self.head)), [])
        self.assertEqual(len(self.entries), 78)
        for entry in self.entries:
            self.assertEqual(list(self.entry.iter_errors(entry)), [], entry)

    def test_the_header_the_engine_writes_now_is_version_2_and_validates(self):
        root = Path(__file__).resolve().parents[2]
        lines = (root / "server" / "tests" / "fixtures" / "golden_session_v2.ndjson").read_text(encoding="utf-8").split("\n")
        head = json.loads(lines[0])["header"]
        self.assertEqual(list(self.header.iter_errors(head)), [])
        self.assertTrue(head["session_id"] and head["content_version"] and head["engine_version"] and "scenario_package_hash" in head)
        for line in lines[1:]:
            if line:
                self.assertEqual(list(self.entry.iter_errors(json.loads(line))), [])

    def test_a_header_with_the_version_2_fields_validates_too(self):
        v2 = dict(self.head, session_id="5b1d4a52-8a3e-4c2f-9c53-1c0f1f6e9d11", content_version="0123456789ab-practice", engine_version="0.1.0", scenario_package_hash="")
        self.assertEqual(list(self.header.iter_errors(v2)), [])

    def test_unknown_fields_and_wrong_values_are_refused(self):
        self.assertTrue(list(self.entry.iter_errors(dict(self.entries[0], surprise=1))))
        self.assertTrue(list(self.entry.iter_errors(dict(self.entries[0], channel="telepathy"))))
        self.assertTrue(list(self.header.iter_errors(dict(self.head, mode="exam"))))


class VerificationContentChecks(unittest.TestCase):
    """The content the service verifies with: one folder per version, for practice and assessment (p1-backend-s09)."""

    def test_each_mode_gets_a_folder_named_by_its_version_and_each_folder_verifies(self):
        import package
        import package_versions
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "content"
            versions = package_versions.build(out)
            self.assertEqual(len(versions), 3)
            self.assertTrue(versions[0].endswith("-practice") and versions[1].endswith("-assessment"))
            dev = versions.pop()   # the whole content/ folder, named by the version the engine gives it: 12 characters of its hash, no mode
            manifest = json.loads((package.CONTENT / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(dev, manifest["content_hash"][:12])
            self.assertTrue((out / dev / "manifest.json").exists() and (out / dev / "case_files").is_dir())
            self.assertEqual(sorted(p.relative_to(out / dev).as_posix() for p in (out / dev).rglob("*") if p.is_file()), sorted(list(manifest["entries"]) + ["manifest.json"]))
            for version in versions:
                self.assertRegex(version, r"^[0-9a-f]{12}-[a-z]+$")
                self.assertEqual(package.verify(out / version, version.split("-")[1]), [])
                self.assertEqual(json.loads((out / version / "manifest.json").read_text(encoding="utf-8"))["content_version"], version)
            self.assertEqual(json.loads((out / "versions.json").read_text(encoding="utf-8"))["versions"], versions + [dev])
            self.assertEqual(len(package_versions.build(Path(tmp) / "release", with_dev=False)), 2)   # --no-dev: a release image holds only the packages


if __name__ == "__main__":
    unittest.main(verbosity=1)
