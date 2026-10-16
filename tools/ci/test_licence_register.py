"""The licence register check (tools/data/licence_register_check.py, p1-dataset-analysis-s01): the real register passes, and each kind of mistake is caught."""
import csv
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "data"))
import licence_register_check as check  # noqa: E402

TABLE = """| ID | Item | Licence | Decision | Held | Notes | Checked |
|---|---|---|---|---|---|---|
{rows}
"""
PIPELINE = {"datasets": [{"id": "DS-0001"}, {"id": "DS-0002"}], "model_catalog": [{"id": "MC-01"}], "external_dependencies": [{"id": "EXT-OLLAMA"}]}
DATASETS = [{"id": "DS-0001", "status": "USED (aggregates only)"}, {"id": "DS-0002", "status": "NOT USED"}]
GOOD = ["| DS-0001 | A | CC BY 4.0 | ALLOWED WITH CONDITIONS | x | y | 2026-10-16 (R-1) |",
        "| DS-0002 | B | CC BY-NC 4.0 | REJECTED | x | y | 2026-10-16 |",
        "| MC-01, MC-02 | C | Apache-2.0 | ALLOWED WITH CONDITIONS (deferred) | x | y | 2026-10-16 |",
        "| EXT-OLLAMA | D | MIT | ALLOWED | x | y | 2026-10-16 |"]


def text(rows):
    return TABLE.format(rows="\n".join(rows))


class LicenceRegister(unittest.TestCase):
    def test_the_real_register_covers_every_id_and_passes(self):
        with (ROOT / "docs/research/dataset_register.csv").open(encoding="utf-8", newline="") as f:
            datasets = list(csv.DictReader(f))
        pipeline = json.loads((ROOT / "docs/architecture/procsense_full_pipeline.json").read_text(encoding="utf-8"))
        register = (ROOT / "docs/research/licence_provenance_register.md").read_text(encoding="utf-8")
        self.assertEqual(check.problems(register, pipeline, datasets), [])
        self.assertGreaterEqual(len(check.required_ids(pipeline, datasets)), 39)

    def test_a_good_table_passes_and_a_row_can_name_several_ids(self):
        self.assertEqual(check.problems(text(GOOD), PIPELINE, DATASETS), [])

    def test_a_missing_id_is_reported(self):
        self.assertEqual(check.problems(text(GOOD[:3]), PIPELINE, DATASETS), ["EXT-OLLAMA: no row in the licence register"])

    def test_an_unknown_decision_word_and_a_missing_date_are_reported(self):
        rows = GOOD[:3] + ["| EXT-OLLAMA | D | MIT | FINE | x | y | 2026-10-16 |"]
        self.assertIn("not one of", check.problems(text(rows), PIPELINE, DATASETS)[0])
        rows = GOOD[:3] + ["| EXT-OLLAMA | D | MIT | ALLOWED | x | y | VERIFY (general knowledge) |"]
        self.assertEqual(check.problems(text(rows), PIPELINE, DATASETS), ["EXT-OLLAMA: decision ALLOWED but no date in the Checked column"])
        rows = GOOD[:3] + ["| EXT-OLLAMA | D | not read | VERIFY | x | y | — |"]          # VERIFY means "not read yet", so it needs no date
        self.assertEqual(check.problems(text(rows), PIPELINE, DATASETS), [])

    def test_the_two_registers_must_agree_on_what_is_used(self):
        rows = ["| DS-0001 | A | CC BY-NC 4.0 | REJECTED | x | y | 2026-10-16 |"] + GOOD[1:]
        found = check.problems(text(rows), PIPELINE, DATASETS)
        self.assertIn("DS-0001: the dataset register uses it but the licence register says REJECTED", found)
        self.assertIn("DS-0001: rejected in the licence register but the dataset register says USED (aggregates only)", found)

    def test_a_table_without_a_decision_column_is_not_read(self):
        extra = "\n| ID | Attribution text |\n|---|---|\n| DS-9999 | Some credit |\n"
        self.assertEqual(check.problems(text(GOOD) + extra, PIPELINE, DATASETS), [])


if __name__ == "__main__":
    unittest.main(verbosity=1)
