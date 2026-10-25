"""The "[x] " prefix of feature plan files (tools/plans/plan_files.py): derived from the feature matrix, one file per feature, links resolve."""
import json
import re
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "plans"))
import plan_files as PF  # noqa: E402

PLANS = ROOT / "docs" / "plans" / "features"


class PlanFiles(unittest.TestCase):
    def test_names(self):
        self.assertEqual(PF.plan_name("A1", "Complete"), "[x] A1.md")
        self.assertEqual(PF.plan_name("A1", "Engineering complete"), "[x] A1.md")
        self.assertEqual(PF.plan_name("A1", "Engineering complete, gated off"), "[x] A1.md")
        for r in ("Built, gated off", "Partly built", "Owned by others", "Research: trigger not met"):
            self.assertEqual(PF.plan_name("A1", r), "A1.md")
        self.assertEqual(PF.feature_id("[x] A12.md"), "A12")
        self.assertEqual(PF.plan_link("A1", "Complete", "x"), "x/%5Bx%5D%20A1.md")

    def test_files_match_the_matrix(self):
        ids = {n["id"] for n in json.loads((ROOT / "docs/architecture/procsense_full_pipeline.json").read_text(encoding="utf-8"))["nodes"] if n["type"] == "feature"}
        names = [p.name for p in PLANS.glob("*.md")]
        self.assertEqual(sorted(PF.feature_id(n) for n in names), sorted(ids), "one file per feature, no duplicates")
        matrix = (ROOT / "docs/architecture/feature_implementation_matrix.md").read_text(encoding="utf-8")
        for fid in ids:
            row = re.search(rf"^\| \[{fid}\]\(([^)]+)\)[^\n]*\*\*(Complete|Engineering complete, gated off|Engineering complete|Built, gated off|Partly built|Owned by others|Research: trigger not met)\*\*", matrix, re.M)
            self.assertIsNotNone(row, fid)
            self.assertTrue((ROOT / "docs/architecture" / row.group(1).replace("%5B", "[").replace("%5D", "]").replace("%20", " ")).exists(), row.group(1))
            self.assertEqual(any(n == f"[x] {fid}.md" for n in names), PF.ticked(row.group(2)), fid)


if __name__ == "__main__":
    unittest.main()
