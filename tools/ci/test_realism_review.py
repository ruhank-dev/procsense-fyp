"""The clinical realism review (tools/eval/realism_review.py): the checklist is read from the walkthrough's Part I.3 (7 sections, 44 items), the pack and the answer sheet
are current, and the import refuses an unanswered item or a correction with no issue ID. The answers here are made up to exercise the code; they are no review."""
import csv
import io
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "eval"))
import realism_review as R  # noqa: E402


def sheet(ok="yes"):
    _, text = R.pack()
    rows = list(csv.DictReader(io.StringIO(text)))
    for r in rows:
        r["ok"] = ok
    return rows


class Checklist(unittest.TestCase):
    def test_seven_sections_and_44_items_from_the_book(self):
        sections = R.checklist()
        self.assertEqual([s[0] for s in sections], list("ABCDEFG"))
        self.assertEqual([len(s[2]) for s in sections], [7, 7, 6, 5, 6, 7, 6])
        self.assertIn("Monitor behaviour", sections[6][2])
        self.assertTrue(sections[0][2][1].startswith("Disease definitions (biliary colic"), "an item over two lines is joined")

    def test_the_pack_and_sheet_are_current(self):
        self.assertEqual(R.main(["pack", "--check"]), 0)

    def test_every_section_says_where_to_look(self):
        self.assertEqual(set(R.WHERE), set("ABCDEFG"))


class Import(unittest.TestCase):
    def test_a_complete_sheet_is_imported(self):
        page, problems = R.import_answers(sheet())
        self.assertEqual(problems, [])
        self.assertIn("| G. VR realism | 6 | 6 | 0 |", page)
        self.assertIn("## Corrections\n\nNone.", page)

    def test_a_blank_item_is_refused(self):
        rows = sheet()
        rows[3]["ok"] = ""
        self.assertTrue(any("ok must be yes or no" in p for p in R.import_answers(rows)[1]))
        self.assertTrue(any("not answered" in p for p in R.import_answers(rows[:-1])[1]))

    def test_a_correction_needs_an_issue_that_exists(self):
        rows = sheet()
        rows[0].update(ok="no", correction="say gallbladder wall, not wall", issue="")
        self.assertTrue(any("needs an issue ID" in p for p in R.import_answers(rows)[1]))
        rows[0]["issue"] = "ISS-9999"
        self.assertTrue(any("needs an issue ID" in p for p in R.import_answers(rows)[1]))
        rows[0]["issue"] = "ISS-0019"
        page, problems = R.import_answers(rows)
        self.assertEqual(problems, [])
        self.assertIn("(ISS-0019)", page)


if __name__ == "__main__":
    unittest.main(verbosity=1)
