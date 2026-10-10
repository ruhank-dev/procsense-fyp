"""Tests for the image + report checks and for the dataset results the images and tables rely on.

    uv run --project tools tools/content/test_images.py
Standard-library unittest; CI runs it as "image and dataset tests". The negative tests copy the real set to a temp folder, break one thing, and expect the checker to say so.
"""
import csv
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import imaging_library as lib  # noqa: E402
import validate_images as vi  # noqa: E402

US = (ROOT / "content/images/index.json", ROOT / "content/imaging_reports.json", ROOT / "content/ultrasound_findings.csv", ROOT / "content/images")
ECG = (ROOT / "content/ecg/index.json", ROOT / "content/ecg_reports.json", ROOT / "content/ecg_findings.csv", ROOT / "content/ecg")


XSEC = (ROOT / "content/xsec/index.json", ROOT / "content/xsec_reports.json", ROOT / "content/xsec_findings.csv", ROOT / "content/xsec")
CXR = (ROOT / "content/cxr/index.json", ROOT / "content/cxr_reports.json", ROOT / "content/cxr_findings.csv", ROOT / "content/cxr")


class Copy:
    """A throwaway copy of one image set whose files the test may break."""

    def __init__(self, paths):
        self.tmp = Path(tempfile.mkdtemp())
        self.dir = self.tmp / "img"
        shutil.copytree(paths[3], self.dir)
        self.index_path, self.reports_path = self.tmp / "index.json", self.tmp / "reports.json"
        shutil.copy(paths[0], self.index_path)
        shutil.copy(paths[1], self.reports_path)
        self.findings = paths[2]
        self.index = json.loads(self.index_path.read_text(encoding="utf-8"))
        self.reports = json.loads(self.reports_path.read_text(encoding="utf-8"))

    def problems(self):
        self.index_path.write_text(json.dumps(self.index), encoding="utf-8")
        self.reports_path.write_text(json.dumps(self.reports), encoding="utf-8")
        return vi.validate(self.index_path, self.reports_path, self.findings, self.dir)

    def close(self):
        shutil.rmtree(self.tmp, ignore_errors=True)


class RealSetsPass(unittest.TestCase):
    def test_ultrasound_set_is_valid(self):
        self.assertEqual(vi.validate(*US), [])

    def test_chest_xray_set_is_valid_and_carries_no_patient_data(self):
        self.assertEqual(vi.validate(*CXR), [])
        for i in json.loads(CXR[0].read_text(encoding="utf-8"))["images"]:
            if not i["synthetic"]:
                self.assertEqual(set(i["source"]), {"dataset", "class", "shard", "row", "url"})   # no patient id, age or sex

    def test_ct_mri_mrcp_set_is_valid_and_synthetic_ones_name_their_real_base(self):
        self.assertEqual(vi.validate(*XSEC), [])
        for i in json.loads(XSEC[0].read_text(encoding="utf-8"))["images"]:
            if i["synthetic"] and i["finding_code"].startswith(("CT-", "MR-AP")):
                self.assertIn("amos_", i["generator"]["base"])      # painted into a real slice, so clean and problem pictures look alike
            if not i["synthetic"]:
                self.assertEqual(i["source"]["dataset"], "AMOS 2022")

    def test_ecg_set_is_valid(self):
        self.assertEqual(vi.validate(*ECG), [])

    def test_every_ultrasound_code_has_a_picture_and_the_split_is_as_planned(self):
        index = json.loads(US[0].read_text(encoding="utf-8"))["images"]
        real = {i["finding_code"] for i in index if not i["synthetic"]}
        synthetic = {i["finding_code"] for i in index if i["synthetic"]}
        self.assertEqual(real, {"US-GB-01", "US-GB-02", "US-GB-05", "US-GB-06"})
        self.assertEqual(len(synthetic), 15)     # every other code, including the clean gallbladder, equivocal gallbladder, pelvis and kidney results
        self.assertEqual(real & synthetic, set())

    def test_every_approval_is_recorded_against_the_exact_picture(self):
        approvals = json.loads((ROOT / "content/image_approvals.json").read_text(encoding="utf-8"))["approvals"]
        for paths in (US, ECG, CXR, XSEC):
            for i in json.loads(paths[0].read_text(encoding="utf-8"))["images"]:
                if i["status"] != "DRAFT":
                    self.assertEqual(approvals[i["id"]]["sha256"], i["sha256"], i["id"])
                    self.assertIn("by", i["approval"])

    def test_ecg_reports_are_consistent_with_their_rhythm(self):
        expect = {"ECG-03": ("af", "no"), "ECG-10": ("af", "no"), "ECG-05": ("chb", "yes"), "ECG-06": ("svt", "yes")}
        for r in json.loads(ECG[1].read_text(encoding="utf-8"))["reports"]:
            m = r["measurements"]
            self.assertEqual((m["rhythm"], m["regular"]), expect.get(r["finding_code"], ("sinus", "yes")), r["id"])
            self.assertIn(f"{m['rate_bpm']} per minute", r["text"])

    def test_the_ecg_scenarios_the_book_names_all_have_a_strip(self):
        codes = {i["finding_code"] for i in json.loads(ECG[0].read_text(encoding="utf-8"))["images"]}
        self.assertEqual(codes, {f"ECG-{n:02d}" for n in range(1, 11)})     # rhythms, heart block, SVT, ST elevation, potassium changes, fast AF

    def test_synthetic_ecg_strips_are_edited_real_ones(self):
        for i in json.loads(ECG[0].read_text(encoding="utf-8"))["images"]:
            if i["synthetic"]:
                self.assertIn("PTB-XL ecg_id", i["generator"]["base"])

class BreakingSomethingIsCaught(unittest.TestCase):
    def check(self, paths, change, expect):
        c = Copy(paths)
        try:
            change(c)
            self.assertTrue(any(expect in p for p in c.problems()), c.problems())
        finally:
            c.close()

    def test_a_changed_picture(self):
        def change(c):
            f = c.dir / c.index["images"][0]["file"]
            f.write_bytes(f.read_bytes() + b"x")
        self.check(US, change, "sha256 does not match")

    def test_a_synthetic_picture_without_its_mark_text(self):
        def change(c):
            next(i for i in c.index["images"] if i["synthetic"])["mark"]["text"] = "nothing"
        self.check(US, change, "needs the mark text")

    def test_a_synthetic_picture_whose_mark_was_painted_over(self):
        def change(c):
            i = next(i for i in c.index["images"] if i["synthetic"])
            im = Image.open(c.dir / i["file"]).convert("L")
            h = i["mark"]["strip_height"]
            im.paste(0, (0, im.height - h, im.width, im.height))
            im.save(c.dir / i["file"], "PNG")
            i["sha256"] = vi.hashlib.sha256((c.dir / i["file"]).read_bytes()).hexdigest()
        self.check(US, change, "mark strip")

    def test_a_real_picture_without_licence(self):
        def change(c):
            next(i for i in c.index["images"] if not i["synthetic"])["licence"] = ""
        self.check(US, change, "needs licence")

    def test_a_report_number_outside_the_books_limit(self):
        def change(c):
            r = next(r for r in c.reports["reports"] if r["finding_code"] == "US-GB-03")
            r["measurements"]["duct_mm"] = 4.0
            r["text"] = r["text"].replace(f"{9:g} mm", "4 mm")
        self.check(US, change, "duct_mm>6")

    def test_a_report_stating_a_number_it_does_not_measure(self):
        def change(c):
            r = next(r for r in c.reports["reports"] if r["synthetic"])
            r["text"] += " Also 77 mm."
        self.check(US, change, "77 mm")

    def test_a_report_naming_a_missing_picture(self):
        def change(c):
            c.reports["reports"][0]["image_id"] = "nope"
        self.check(US, change, "does not exist")

    def test_approved_status_without_an_approval_record(self):
        def change(c):
            c.index["images"][0].pop("approval")
        self.check(US, change, "needs an approval record")

    def test_an_approval_that_belongs_to_a_different_file_is_caught(self):
        c = Copy(US)
        try:
            c.index["images"][0]["sha256"] = "0" * 64      # as if the picture was edited after approval
            c.index_path.write_text(json.dumps(c.index), encoding="utf-8")
            approvals = json.loads((ROOT / "content/image_approvals.json").read_text(encoding="utf-8"))["approvals"]
            found = vi.validate(c.index_path, c.reports_path, c.findings, c.dir, approvals)
            self.assertTrue(any("does not match this exact picture" in p for p in found), found)
        finally:
            c.close()

    def test_an_ecg_report_that_contradicts_its_rhythm(self):
        def change(c):
            r = next(r for r in c.reports["reports"] if r["finding_code"] == "ECG-02")
            r["measurements"]["rate_bpm"] = 70
        self.check(ECG, change, "rate_bpm>=100")

    def test_a_picture_without_a_report(self):
        def change(c):
            c.reports["reports"].pop(0)
        self.check(US, change, "has no report")

    def test_embedded_metadata_is_caught(self):
        def change(c):
            i = next(i for i in c.index["images"] if not i["synthetic"])
            im = Image.open(c.dir / i["file"])
            exif = Image.Exif()
            exif[270] = "patient name"
            im.save(c.dir / i["file"], "JPEG", exif=exif)
            i["sha256"] = vi.hashlib.sha256((c.dir / i["file"]).read_bytes()).hexdigest()
        self.check(US, change, "embedded metadata")


class LibraryTests(unittest.TestCase):
    def test_the_library_has_no_hole(self):
        self.assertEqual(lib.problems(), [])

    def test_every_test_has_a_clean_result_and_problems_and_pictures_to_reuse(self):
        rows = lib.library()
        for test in lib.TESTS:
            mine = [r for r in rows if r["test"] == test]
            self.assertEqual(sum(r["role"] == "clean" for r in mine), 1, test)
            self.assertGreaterEqual(sum(r["role"] == "problem" for r in mine), 1, test)
            if lib.TESTS[test][0] in lib.TEXT_KINDS:
                self.assertTrue(all(r["pictures"] == 0 and lib.result_text(r["finding_code"]) for r in mine), test)      # written result only, no picture
            else:
                self.assertTrue(all(r["pictures"] >= 4 for r in mine), test)

    def test_every_what_if_that_names_a_picture_test_can_produce_a_picture(self):
        """A what-if whose 'check next' mentions the ECG or imaging must have at least one picture result mapped, so no scenario is left without its picture."""
        with open(ROOT / "content/whatif_library.csv", encoding="utf-8", newline="") as f:
            whatifs = list(csv.DictReader(f))
        mapped = {}
        for w in lib.whatif_rows():
            mapped.setdefault(w["whatif_id"], set()).add(w["test"])
        for w in whatifs:
            text = w["v_check_next"] + " " + w["v_correct"]
            if "ECG" in w["v_check_next"]:
                self.assertIn("ecg", mapped.get(w["id"], set()), w["title"])
            if "imaging" in w["v_check_next"].lower() or "ultrasound" in w["v_check_next"].lower():
                self.assertTrue(mapped.get(w["id"]), w["title"])

    def test_every_result_is_produced_by_a_case_or_a_what_if(self):
        produced = {c["d_finding_code"] for c in lib.case_rows()} | {w["d_finding_code"] for w in lib.whatif_rows()} | {code for _, code in lib.TESTS.values()}
        self.assertEqual({r["finding_code"] for r in lib.library()} - produced, set())

    def test_pregnancy_dipstick_and_blood_sample_are_results_only_with_no_picture(self):
        for test in ("pregnancy_test", "urine_dipstick", "blood_sample_appearance"):
            self.assertIn(lib.TESTS[test][0], lib.TEXT_KINDS)
        self.assertFalse((ROOT / "content/bedside").exists())
        self.assertIn("positive", lib.result_text("PREG-01"))
        self.assertIn("haemolysis", lib.result_text("TUBE-01"))
        self.assertEqual(lib.result_code("VAR-S1-J", "blood_sample_appearance"), "TUBE-01")
        with self.assertRaises(LookupError):
            lib.pick("TUBE-01", 1)       # there is no picture to pick: the engine shows the text

    def test_a_test_a_case_does_not_list_returns_the_clean_result(self):
        self.assertEqual(lib.result_code("VAR-S1-A", "chest_xray"), "CXR-01")
        self.assertEqual(lib.result_code("VAR-S1-A", "ultrasound_gallbladder"), "US-GB-01")
        self.assertEqual(lib.result_code("VAR-S1-PB", "mrcp"), "MRCP-01")
        self.assertEqual(lib.result_code("VAR-S2-PD", "ecg"), "ECG-01")
        self.assertEqual(lib.result_code("VAR-S1-H", "pregnancy_test"), "PREG-01")
        self.assertEqual(lib.result_code("VAR-S1-A", "pregnancy_test"), "PREG-00")      # not listed: the clean result

    def test_a_scenario_event_changes_the_finding_not_the_picture(self):
        self.assertEqual(lib.result_code("VAR-S1-A", "ultrasound_gallbladder", event_override="US-GB-03"), "US-GB-03")

    def test_picking_is_repeatable_and_does_not_repeat_the_last_picture(self):
        a = lib.pick("US-GB-01", 7)
        self.assertEqual(a, lib.pick("US-GB-01", 7))
        self.assertNotEqual(lib.pick("US-GB-01", 7, last=a), a)
        self.assertEqual({lib.pick("US-GB-05", n) for n in range(8)}, {"us-gb-05-r0", "us-gb-05-r1", "us-gb-05-r2", "us-gb-05-r3"})   # all four are reachable
        self.assertGreaterEqual(len({lib.pick("ECG-02", n) for n in range(8)}), 4)

    def test_only_pictures_at_the_required_status_are_eligible(self):
        with self.assertRaises(LookupError):
            lib.pick("US-GB-01", 1, minimum="APPROVED_FOR_ASSESSMENT")      # nothing is approved that high yet (owner's approval is at training level)

    def test_a_case_listing_the_clean_code_or_a_code_of_another_test_is_caught(self):
        bad = [{"case_id": "VAR-S1-A", "test": "ecg", "d_finding_code": "ECG-01"}, {"case_id": "VAR-S1-A", "test": "mrcp", "d_finding_code": "US-GB-01"}]
        original = lib.case_rows
        lib.case_rows = lambda: bad
        try:
            found = lib.problems()
        finally:
            lib.case_rows = original
        self.assertTrue(any("clean code" in p for p in found), found)
        self.assertTrue(any("does not belong" in p for p in found), found)


class FetchManifestTests(unittest.TestCase):
    """The fetch script must name every source file the committed pictures were built from, and pin each with a checksum (offline check)."""

    def test_the_fetch_list_covers_every_source_the_builders_use(self):
        sys.path.insert(0, str(ROOT / "tools/data"))
        import re
        import fetch_all
        wanted = fetch_all.wanted()
        amos = {w["scan"] for w in wanted if w["kind"] == "amos"}
        used = set(re.findall(r'vid="(amos_\d+)"', (ROOT / "tools/data/xsec.py").read_text(encoding="utf-8")))      # read as text: xsec.py needs numpy and nibabel
        self.assertTrue(used and used <= amos, used - amos)
        sel = json.loads((ROOT / "tools/data/ultrasound_real_selection.json").read_text(encoding="utf-8"))["images"]
        classes = {int(i["archive"][0]) for i in sel}
        self.assertTrue(classes <= {w["class"] for w in wanted if w["kind"] == "uidatagb"})
        nih = {w["n"] for w in wanted if w["kind"] == "nih"}
        self.assertEqual(nih, set(fetch_all.nih_shards()))

    def test_the_manifest_pins_every_large_file(self):
        sys.path.insert(0, str(ROOT / "tools/data"))
        import fetch_all
        known = json.loads(fetch_all.MANIFEST.read_text(encoding="utf-8"))["files"]
        for w in fetch_all.wanted():
            if w["kind"] == "ptbxl":
                continue
            if w["kind"] == "uidatagb":
                self.assertTrue(any(k.startswith(f"uidatagb/{w['class']}") for k in known), w)
            else:
                self.assertIn(w["path"], known)
        for v in known.values():
            self.assertEqual(len(v["sha256"]), 64)


class RuleReaderTests(unittest.TestCase):
    def test_alternative_words_inherit_the_key(self):
        self.assertEqual(vi.rule_problems("complication=gas|pus|perforation", {"complication": "pus"}), [])
        self.assertTrue(vi.rule_problems("complication=gas|pus", {"complication": "mass"}))

    def test_absent_keys_are_not_asserted(self):
        self.assertEqual(vi.rule_problems("wall_mm<=3", {}), [])

    def test_numeric_limits(self):
        self.assertTrue(vi.rule_problems("duct_mm>6", {"duct_mm": 6.0}))
        self.assertEqual(vi.rule_problems("duct_mm>6", {"duct_mm": 6.5}), [])


ptb_before = 25   # samples before the R peak (extract_ptbxl.BEFORE)


class DatasetResultsTests(unittest.TestCase):
    D = ROOT / "tools/data/derived"

    def test_ecg_templates_have_plausible_rates(self):
        c = json.loads((self.D / "ptbxl_ecg_templates.json").read_text(encoding="utf-8"))["classes"]
        self.assertTrue(55 <= c["sinus_rhythm"]["heart_rate_median"] < 100)
        self.assertGreaterEqual(c["sinus_tachycardia"]["heart_rate_median"], 100)
        self.assertGreater(c["atrial_fibrillation"]["rr_variation_cv_median"], c["sinus_rhythm"]["rr_variation_cv_median"])
        for v in c.values():
            self.assertEqual(len(v["template_r_normalised"]), 75)
            t = v["template_r_normalised"]   # beats are normalised to their own R peak; averaging beats of different shape leaves the peak below 1, but still the tallest point at the R position
            self.assertTrue(0.5 < max(t) <= 1.0)
            self.assertAlmostEqual(t.index(max(t)), ptb_before, delta=2)

    def test_dataset_register_is_honest_about_unread_pages(self):
        with open(ROOT / "docs/research/dataset_register.csv", encoding="utf-8", newline="") as f:
            rows = {r["id"]: r for r in csv.DictReader(f)}
        for r in rows.values():
            if r["contents_checked"].startswith("no"):
                self.assertTrue(r["status"].startswith("NOT"), r["id"])
            if r["status"].startswith("USED"):
                self.assertTrue(r["contents_checked"].startswith("yes"), r["id"])
        self.assertTrue(rows["DS-0013"]["status"].startswith("REJECTED"))

    def test_emr_distractors_include_near_miss_names(self):
        with open(ROOT / "content/emr_distractors.csv", encoding="utf-8", newline="") as f:
            names = [r["d_name"] for r in csv.DictReader(f)]
        self.assertGreaterEqual(sum("Ayesha" in n for n in names), 2)


if __name__ == "__main__":
    unittest.main(verbosity=1)
