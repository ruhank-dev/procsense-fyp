"""The P1 variant check against the book (tools/content/variant_facts.py, p1-case-library-extended-s01): the real cases pass, and a changed value or a phrase that is not the book's is caught."""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "content"))
import variant_facts as vf  # noqa: E402


class VariantFacts(unittest.TestCase):
    def test_every_book_fact_of_the_ten_variants_is_in_its_case_file(self):
        rows, problems = vf.run()
        self.assertEqual(problems, [])
        self.assertEqual(sorted({r["case"] for r in rows}), sorted(["VAR-S1-I", "VAR-S1-K", "VAR-S1-L", "VAR-S1-M", "VAR-S2-E", "VAR-S2-H", "VAR-S2-I", "VAR-S2-J", "VAR-S2-K", "VAR-S2-L"]))
        self.assertTrue(all(r["ok"] for r in rows))

    def test_a_case_file_that_no_longer_says_what_the_book_says_is_caught(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            for case_id in vf.FACTS:
                shutil.copy(vf.CASES / f"{case_id}.json", folder / f"{case_id}.json")
            case = json.loads((folder / "VAR-S1-K.json").read_text(encoding="utf-8"))
            next(r for r in case["results"] if r["test"] == "Hb")["value"] = "108"
            (folder / "VAR-S1-K.json").write_text(json.dumps(case), encoding="utf-8")
            old, vf.CASES = vf.CASES, folder
            try:
                _, problems = vf.run()
            finally:
                vf.CASES = old
        self.assertEqual(problems, ["VAR-S1-K: Hb: the book says 'Hb 98 g/L' but the case file has '108'"])

    def test_a_phrase_that_is_not_the_books_words_is_caught(self):
        old = vf.FACTS["VAR-S1-K"]
        vf.FACTS["VAR-S1-K"] = [("Hb 98 grams per litre", "Hb", vf._result("Hb"), "equals", 98)]
        try:
            _, problems = vf.run()
        finally:
            vf.FACTS["VAR-S1-K"] = old
        self.assertEqual(problems, ["VAR-S1-K: 'Hb 98 grams per litre' is not in the book's story of this case (content/case_seeds.csv)"])

    def test_numbers_compare_as_numbers_and_text_ignores_case(self):
        self.assertTrue(vf._matches("14.0", "equals", 14))
        self.assertFalse(vf._matches("14.5", "equals", 14))
        self.assertTrue(vf._matches("Leucocytes 2+, nitrite negative", "contains", "leucocytes 2+"))
        self.assertFalse(vf._matches(None, "contains", "x"))
        self.assertTrue(vf._matches(True, "equals", True) and not vf._matches(1, "equals", True))


if __name__ == "__main__":
    unittest.main(verbosity=1)
