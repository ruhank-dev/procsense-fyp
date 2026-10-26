"""The AI-assistant-authored spoken-order sets (B23, C32, EX-18; ADR-0028): well-formed, labelled as AI-authored synthetic data, separate, and the held-out result belongs to the frozen files."""
import hashlib
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SETS = ROOT / "tests" / "orders" / "ai_authored"
REPORTS = ROOT / "reports" / "ai"
sys.path.insert(0, str(ROOT))
from server.orders.grammar import vocabulary  # noqa: E402
from server.orders.interpreter import catalogue  # noqa: E402

DEV = json.loads((SETS / "dev_v1.json").read_text(encoding="utf-8"))
HELD = json.loads((SETS / "heldout_v1.json").read_text(encoding="utf-8"))
IDS = set(catalogue(vocabulary())[1])


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


class AiAuthoredSets(unittest.TestCase):
    def test_rows_are_well_formed_and_every_target_is_in_the_catalogue(self):
        for doc in (DEV, HELD):
            self.assertIn("NOT human-authored", doc["authorship"])
            seen = set()
            for r in doc["rows"]:
                self.assertNotIn(r["id"], seen)
                seen.add(r["id"])
                self.assertIn(r["context_id"], doc["contexts"])
                e = r["expect"]
                self.assertIn(e["behaviour"], ("propose", "no_order", "clarify", "unsupported"))
                self.assertEqual(e["behaviour"] == "propose", bool(e["target"]), r["id"])
                for t in [e["target"]] + e["accept"]:
                    self.assertTrue(t is None or t in IDS, (r["id"], t))
                self.assertTrue(r["rationale"], r["id"])
            self.assertEqual(len(doc["counts"]), 18)

    def test_development_and_held_out_sets_share_no_sentence_and_are_large_enough(self):
        self.assertFalse({r["text"].lower() for r in DEV["rows"]} & {r["text"].lower() for r in HELD["rows"]})
        self.assertGreaterEqual(len(DEV["rows"]), 150)
        self.assertGreaterEqual(len(HELD["rows"]), 150)
        self.assertEqual((DEV["role"], HELD["role"]), ("development", "heldout"))

    def test_the_held_out_result_was_scored_once_on_these_frozen_files(self):
        rep = json.loads((REPORTS / "orders-ai-heldout_v1.json").read_text(encoding="utf-8"))
        self.assertEqual(rep["set_sha256"], sha(SETS / "heldout_v1.json"), "the held-out labels changed after the run")
        self.assertEqual(rep["rows_sha256"], hashlib.sha256(json.dumps(HELD["rows"], sort_keys=True, ensure_ascii=False).encode()).hexdigest(), "a held-out row changed after the run")
        self.assertEqual(rep["prompt_sha256"], sha(ROOT / "prompts" / "orders" / "interpret_v1.txt"), "the prompt changed after the held-out run")
        self.assertEqual(rep["interpreter_sha256"], sha(ROOT / "server" / "orders" / "interpreter.py"), "the interpreter changed after the held-out run")
        self.assertFalse(rep["independent_human"])
        self.assertFalse(rep["second_look"])
        self.assertEqual(rep["model"], "gemma4:31b-cloud")
        self.assertEqual(rep["confirmation_bypasses"], 0)
        self.assertGreaterEqual(rep["accuracy"], 0.95)


if __name__ == "__main__":
    unittest.main()
