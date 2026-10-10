"""Tests for the equipment reference sheets (docs/research/equipment) and their generator.

    uv run --project tools tools/content/test_equipment.py
CI runs it as "equipment sheet tests".
"""
import copy
import re
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import equipment_data as data  # noqa: E402
import equipment_sheets as es  # noqa: E402

SHEETS = sorted((ROOT / "docs/research/equipment").glob("[0-9][0-9]-*.md"))


class Sheets(unittest.TestCase):
    def test_all_53_items_have_a_sheet_and_the_generator_finds_no_problem(self):
        self.assertEqual(len(SHEETS), 53)
        self.assertEqual([int(p.name[:2]) for p in SHEETS], list(range(1, 54)))
        self.assertEqual(es.problems(), [])

    def test_every_sheet_has_the_required_sections(self):
        for p in SHEETS:
            t = p.read_text(encoding="utf-8")
            for section in ("## Dimensions", "## Components", "## Materials and colours", "## States", "## Interaction", "## Triangle-budget class", "## Open points"):
                self.assertIn(section, t, p.name)
            self.assertIn("Representative device", t, p.name)

    def test_no_dimension_row_without_a_source(self):
        for p in SHEETS:
            for line in p.read_text(encoding="utf-8").split("\n"):
                if re.search(r"\| (datasheet|listing) \|", line):
                    self.assertRegex(line, r"\]\(https?://", f"{p.name}: {line[:80]}")

    def test_the_figures_that_were_opened_in_a_data_sheet_are_marked_as_such(self):
        mon = (ROOT / "docs/research/equipment/09-vital-sign-monitor.md").read_text(encoding="utf-8")
        self.assertIn("288 x 327 x 182 mm", mon)
        self.assertIn("datasheet", mon)
        pump = (ROOT / "docs/research/equipment/27-infusion-pump.md").read_text(encoding="utf-8")
        self.assertIn("214 x 68 x 124 mm", pump)

    def test_open_values_are_listed_as_open_points_not_guessed(self):
        for n, it in data.ITEMS.items():
            text = (ROOT / "docs/research/equipment" / f"{n:02d}-{it['slug']}.md").read_text(encoding="utf-8")
            opens = [d for d in it["dims"] if d["level"] == "open"]
            if opens:
                self.assertNotIn("- None.", text.split("## Open points")[1], n)

    def test_pictures_of_bedside_results_are_not_modelled(self):
        t = (ROOT / "docs/research/equipment/19-urine-pot-dipstick-pregnancy-test.md").read_text(encoding="utf-8")
        self.assertIn("written results only", t)


class BreakingSomethingIsCaught(unittest.TestCase):
    def with_data(self, change):
        saved = copy.deepcopy(data.ITEMS)
        try:
            change(data.ITEMS)
            return es.problems()
        finally:
            data.ITEMS.clear()
            data.ITEMS.update(saved)

    def test_a_listing_without_a_url(self):
        found = self.with_data(lambda items: items[9]["dims"][0].update(url=""))
        self.assertTrue(any("no URL" in p for p in found), found)

    def test_an_unknown_level(self):
        found = self.with_data(lambda items: items[9]["dims"][0].update(level="guess"))
        self.assertTrue(any("unknown level" in p for p in found), found)

    def test_a_missing_item(self):
        found = self.with_data(lambda items: items.pop(30))
        self.assertTrue(any("1 to 53" in p for p in found), found)

    def test_an_open_dimension_claimed_as_exact(self):
        def claim(items):
            dim = next(d for d in items[18]["dims"] if d["level"] == "open")     # the blood-culture bottle diameter has no source yet
            dim.update(value="28 mm", source="a page", exact="exact")
        found = self.with_data(claim)
        self.assertTrue(any("states a value as exact" in p for p in found), found)


if __name__ == "__main__":
    unittest.main(verbosity=1)
