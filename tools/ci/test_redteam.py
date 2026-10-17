"""The red team of the grounding validator (tools/redteam/run.py; p2-ai-advanced-simulation-s01): the critical classes have zero escapes and the committed report is what the run gives."""
import json
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class RedTeam(unittest.TestCase):
    def test_critical_classes_have_zero_escapes_and_the_report_is_current(self):
        p = subprocess.run([sys.executable, str(ROOT / "tools" / "redteam" / "run.py")], capture_output=True, text=True, cwd=ROOT)
        self.assertEqual(p.returncode, 0, p.stderr[-400:])
        out = json.loads(p.stdout)
        self.assertEqual(out["critical_escapes"], 0)
        committed = json.loads((ROOT / "reports" / "ai" / "redteam.json").read_text(encoding="utf-8"))
        for cls, row in out.items():
            if cls != "critical_escapes":
                self.assertEqual((row["attacks"], row["escapes"]), (committed["classes"][cls]["attacks"], committed["classes"][cls]["escapes"]), cls)

    def test_the_known_gaps_are_recorded_as_gaps(self):
        committed = json.loads((ROOT / "reports" / "ai" / "redteam.json").read_text(encoding="utf-8"))["classes"]
        self.assertEqual(committed["denial"]["escape_rate"], 1.0)             # DT-190: a made-up denial is invisible to a lexical check
        self.assertFalse(committed["denial"]["critical"])


if __name__ == "__main__":
    unittest.main()
