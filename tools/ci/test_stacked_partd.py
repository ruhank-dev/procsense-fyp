"""The stacked-case check against Part D (tools/content/stacked_partd.py, p1-case-library-extended-s02): the eight cases pass, and a rank out of the book's order, a missing decision,
a prompt without the book's numbers and a changed result are each caught."""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "content"))
import stacked_partd as sp  # noqa: E402


class StackedPartD(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.folder = Path(self.tmp.name)
        for case_id in sp.RANKS:
            shutil.copy(sp.CASES / f"{case_id}.json", self.folder / f"{case_id}.json")
        self.real, sp.CASES = sp.CASES, self.folder

    def tearDown(self):
        sp.CASES = self.real
        self.tmp.cleanup()

    def edit(self, case_id, change):
        p = self.folder / f"{case_id}.json"
        case = json.loads(p.read_text(encoding="utf-8"))
        change(case)
        p.write_text(json.dumps(case), encoding="utf-8")
        return sp.check(case_id)[1]

    def test_the_eight_stacked_cases_follow_part_d(self):
        for case_id in sp.RANKS:
            self.assertEqual(sp.check(case_id)[1], [], case_id)

    def test_part_d_is_read_section_by_section(self):
        sec = sp.sections("STK-S1-B")
        self.assertIn("Apixaban active", sec[8])
        self.assertEqual(len(sp.book_decisions(sec[9])), 4)
        self.assertIn("Ferritin 9", sec[12])

    def test_two_problems_swapped_out_of_the_books_order_are_caught(self):
        def swap(c):
            ranks = {p["id"]: p for p in c["problems"]}
            ranks["stkb.dose"]["rank"], ranks["stkb.anaemia"]["rank"] = 2, 1
        self.assertIn("STK-S1-B: stkb.anaemia (rank 1) is out of the book's order (section 8 row 2)", self.edit("STK-S1-B", swap))

    def test_a_missing_decision_and_a_prompt_without_the_books_numbers_are_caught(self):
        found = self.edit("STK-S1-C", lambda c: c["decisions"].pop())
        self.assertIn("STK-S1-C: the book has 4 decisions in section 9, the case has 3", found)

        def reword(c):
            c["decisions"][2]["prompt"] = "The kidney result is back. What now?"
        found = self.edit("STK-S1-C", reword)
        self.assertTrue(any("decision 3" in f and "178" in f for f in found), found)

    def test_a_result_that_differs_from_the_books_table_is_caught(self):
        def wrong(c):
            next(r for r in c["results"] if r["test"] == "Capillary ketones")["value"] = "0.4"
        self.assertEqual(self.edit("STK-S1-A", wrong), ["STK-S1-A: Capillary ketones: the book's results table says 'Ketones 0.9' but the case has '0.4'"])


if __name__ == "__main__":
    unittest.main(verbosity=1)
