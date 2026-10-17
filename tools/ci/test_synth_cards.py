"""Synthetic dataset cards (tools/synth/publish.py; p1-evaluation-s01, plan tests T-SD1 and T-SD4): an incomplete card is refused, a card that disagrees with its data is caught, every card in the repository is whole."""
import copy
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "synth"))
import publish  # noqa: E402


class Cards(unittest.TestCase):
    def setUp(self):
        self.cards = publish.load_cards()
        self.good = self.cards[0]

    def test_every_card_in_the_repository_is_whole_and_the_index_is_current(self):
        self.assertGreaterEqual(len(self.cards), 5)
        self.assertEqual([p for c in self.cards for p in publish.problems(c)], [])
        self.assertEqual((publish.INDEX).read_text(encoding="utf-8"), publish.index_text(self.cards))

    def test_a_card_missing_a_required_field_is_refused(self):
        for field in ("purpose", "generator", "validation", "review", "model_use", "use"):
            bad = copy.deepcopy(self.good)
            del bad[field]
            self.assertTrue(publish.problems(bad), field)

    def test_a_card_that_does_not_say_synthetic_is_refused(self):
        bad = copy.deepcopy(self.good)
        bad["synthetic"] = False
        self.assertTrue(publish.problems(bad))

    def test_a_missing_generator_a_missing_file_and_a_wrong_row_count_are_caught(self):
        bad = copy.deepcopy(self.good)
        bad["generator"]["path"] = "tools/nothing_here.py"
        self.assertTrue(any("generator" in p for p in publish.problems(bad)))
        bad = copy.deepcopy(self.good)
        bad["files"] = ["tests/no/such/file.json"]
        self.assertTrue(any("does not exist" in p for p in publish.problems(bad)))
        bad = copy.deepcopy(self.good)
        key = next(iter(bad["rows"]))
        field = next(iter(bad["rows"][key]))
        bad["rows"][key][field] += 1
        self.assertTrue(any("rows" in p for p in publish.problems(bad)))

    def test_two_cards_with_one_id_are_caught(self):
        self.assertEqual(len({c["id"] for c in self.cards}), len(self.cards))


if __name__ == "__main__":
    unittest.main()
