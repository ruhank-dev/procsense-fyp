"""ADR-0019: the Qwen 3.5 9B model is retired. No active runtime, tool default or setup path may name it; only the files that read or describe HISTORICAL 9B results may."""
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
HISTORICAL_READERS = {"tools/ai/model_compare.py", "tools/bench/classifier_report.py", "tools/bench/cross_judge.py", "tools/ci/test_llm.py", "tools/ci/test_no_9b.py", "tools/bench/safety_report.py",
                      "tools/bench/llm_classifier_compare.py", "tools/bench/heads_same_rows.py", "tools/eval/collect_ai.py"}


class NoNineB(unittest.TestCase):
    def test_no_active_file_names_the_9b_model(self):
        files = subprocess.run(["git", "grep", "-l", "-i", "-E", "qwen3[.]5:9b"], cwd=ROOT, capture_output=True, text=True).stdout.split()
        active = [f for f in files if f.endswith((".py", ".yml", ".yaml", ".toml", ".sh", ".cs")) and f not in HISTORICAL_READERS]
        self.assertEqual(active, [], "a code or config file names the retired 9B model")

    def test_every_model_default_is_the_4b(self):
        from llm import client
        self.assertEqual((client.LIVE_MODEL, client.BATCH_MODEL), ("qwen3.5:4b-q4_K_M", "qwen3.5:4b-q4_K_M"))
        sys.path.insert(0, str(ROOT / "tools" / "kb"))
        sys.path.insert(0, str(ROOT / "tools" / "bench"))
        import extract_relations
        import cross_judge
        import judge_relations
        self.assertEqual((extract_relations.DEFAULT_MODEL, judge_relations.MODEL, cross_judge.MODEL), ("qwen3.5:4b-q4_K_M",) * 3)


if __name__ == "__main__":
    unittest.main()
