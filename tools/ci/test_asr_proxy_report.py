"""The on-device recogniser proxy report says what it is: a desktop-processor figure on the same clips as the server's, never a headset result."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import asr_wer as W  # noqa: E402

REPORT = json.loads((ROOT / "reports" / "ai" / "asr-ondevice-proxy.json").read_text(encoding="utf-8"))
SERVER = json.loads((ROOT / "reports" / "ai" / "asr-wer.json").read_text(encoding="utf-8"))


class Proxy(unittest.TestCase):
    def test_same_clips_as_the_server_figure_and_not_the_headset(self):
        self.assertTrue(REPORT)
        for name, r in REPORT.items():
            self.assertEqual(r["clips"], SERVER["clips"], name)
            self.assertIn("NOT the Quest", r["platform"], name)
            self.assertIn("not measured", r["limit"], name)

    def test_the_decision_follows_the_gate(self):
        gate = 0.25
        passing = sorted(n for n, r in REPORT.items() if r["wer"] <= gate)
        self.assertEqual(passing, ["sherpa-onnx-whisper-tiny.en"])           # ADR-0014 names only the model that passes

    def test_the_error_rate_code_is_the_servers(self):
        self.assertEqual(W.wer(W.norm("No illnesses."), W.norm("no little nesses")), (2, 2))


if __name__ == "__main__":
    unittest.main()
