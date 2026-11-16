"""The degraded demonstration check script (P3-VOX-11): the tests it runs exist, and the live report says what the headset will do."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools" / "setup"))
import demo_degraded_check as d  # noqa: E402


class DemoDegradedCheck(unittest.TestCase):
    def test_the_tests_it_runs_exist(self):
        for t in d.TESTS:
            path, _, name = t.partition("::")
            text = (d.ROOT / "server" / path).read_text(encoding="utf-8")
            if name:
                self.assertIn(f"def {name}(", text)

    def test_the_live_report_names_the_fallbacks(self):
        original = d.reachable
        try:
            d.reachable = lambda url, timeout=3.0: False
            report = " ".join(d.live_report())
            self.assertIn("local fallbacks", report)
            self.assertIn("menu mode", report)
            d.reachable = lambda url, timeout=3.0: True
            self.assertEqual([x.split(": ")[1] for x in d.live_report()], ["reachable", "up"])
        finally:
            d.reachable = original


if __name__ == "__main__":
    unittest.main()
