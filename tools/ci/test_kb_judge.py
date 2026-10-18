"""The AI-judged relation precision (tools/kb/judge_relations.py): the arithmetic, and that it never claims to be the hand-labelled gate."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "kb"))
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import judge_relations as J  # noqa: E402

ROWS = [{"id": str(i), "subject": "a", "predicate": "leads_to", "object": "b", "quote": "a leads to b"} for i in range(4)]


class Judge(unittest.TestCase):
    def test_both_either_and_the_split_rows(self):
        s = J.summarise(ROWS, [True, True, False, False], [True, False, True, False])
        self.assertEqual((s["supported_by_both"], s["supported_by_either"], s["precision_both"], s["precision_either"]), (1, 3, 0.25, 0.75))
        self.assertEqual([x["id"] for x in s["split_judgements"]], ["1", "2"])

    def test_kappa(self):
        self.assertEqual(J.kappa([True, False, True, False], [True, False, True, False]), 1.0)
        self.assertEqual(J.kappa([True, True], [True, True]), None)                      # no variation: undefined
        self.assertLess(J.kappa([True, False, True, False], [False, True, False, True]), 0)

    def test_the_prompt_names_the_claim_and_the_quote(self):
        for cfg in ("J1", "J2"):
            p = J.prompt(ROWS[0], cfg)
            self.assertIn("a leads to b", p)
            self.assertIn("quote", p.lower())

    def test_a_report_is_never_the_hand_labelled_gate(self):
        import json
        if J.REPORT.exists():
            r = json.loads(J.REPORT.read_text(encoding="utf-8"))
            self.assertFalse(r["hand_labelled"])
            self.assertFalse(r["gate_0_8_met"])


if __name__ == "__main__":
    unittest.main()
