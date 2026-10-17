"""The terminology code tables (tools/content/terminology_codes.py): the real files pass, and each kind of mistake is caught."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "content"))
import terminology_codes as T  # noqa: E402


class Codes(unittest.TestCase):
    def test_the_real_tables_cover_every_analyte_and_condition_word(self):
        loinc, icd = T.read("loinc.csv"), T.read("icd11.csv")
        self.assertEqual(T.problems(loinc, icd, T.analytes(), T.condition_words()), [])
        self.assertEqual(len(loinc), 23)
        self.assertEqual(sum(1 for r in icd if r["icd11"]), 30)

    def test_the_loinc_check_digit(self):
        for code in ("2160-0", "2823-3", "718-7", "1988-5", "14682-9", "62238-1"):
            self.assertTrue(T.loinc_check_digit_ok(code), code)
        for code in ("2160-1", "2823-4", "718-8"):
            self.assertFalse(T.loinc_check_digit_ok(code), code)

    def test_each_kind_of_mistake_is_reported(self):
        loinc, icd = T.read("loinc.csv"), T.read("icd11.csv")
        a, w = T.analytes(), T.condition_words()
        bad = [dict(r) for r in loinc]
        bad[0]["loinc"] = "718-8"
        self.assertTrue(any("invalid LOINC" in p for p in T.problems(bad, icd, a, w)))
        self.assertTrue(any("no row for analyte" in p for p in T.problems(loinc[1:], icd, a, w)))
        bad = [dict(r) for r in icd]
        bad[0]["icd11"] = "not a code"
        self.assertTrue(any("invalid ICD-11" in p for p in T.problems(loinc, bad, a, w)))
        bad = [dict(r) for r in icd]
        none_row = next(r for r in bad if not r["icd11"])
        none_row["note"] = "forgot"
        self.assertTrue(any("does not say why" in p for p in T.problems(loinc, bad, a, w)))
        self.assertTrue(any("no row for condition word" in p for p in T.problems(loinc, icd[1:], a, w)))
        bad = [dict(r) for r in icd]
        bad[1]["checked"] = ""
        self.assertTrue(any("check date" in p for p in T.problems(loinc, bad, a, w)))


if __name__ == "__main__":
    unittest.main()
