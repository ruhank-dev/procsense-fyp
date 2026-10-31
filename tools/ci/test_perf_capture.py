"""The weekly profiling summary (tools/unity/perf_capture.py): the gate, the 1 % low, the estimate when there is no frame-time column, the 5 % regression rule and
a CSV without the columns refused. The capture here is made up to exercise the code; it is no measurement."""
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "unity"))
import perf_capture as P  # noqa: E402


def capture(folder, name, fps, mem_mb, frame_ms=None):
    path = Path(folder) / name
    head = "Time Stamp,average_frame_rate,app_pss_MB" + (",app_frame_time_ms" if frame_ms is not None else "")
    rows = [f"{i},{f},{mem_mb}" + (f",{frame_ms[i]}" if frame_ms is not None else "") for i, f in enumerate(fps)]
    path.write_text("\n".join([head] + rows) + "\n", encoding="utf-8")
    return path


class Summary(unittest.TestCase):
    def test_a_good_capture_passes(self):
        with tempfile.TemporaryDirectory() as tmp:
            s = P.summarise(capture(tmp, "2026-11-02-demo-abc1234.csv", [72, 73, 74], 1800, [13.9, 14.1, 20.0]))
            self.assertTrue(s["pass"])
            self.assertAlmostEqual(s["avg_fps"], 73.0)
            self.assertEqual(s["worst_ms"], 20.0)
            self.assertAlmostEqual(s["peak_gb"], 1800 / 1024)

    def test_a_hitch_or_memory_fails_and_a_missing_frame_column_is_estimated(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertFalse(P.summarise(capture(tmp, "a.csv", [80, 80], 1800, [14, 60]))["pass"])
            self.assertFalse(P.summarise(capture(tmp, "b.csv", [80, 80], 2700))["pass"])
            s = P.summarise(capture(tmp, "c.csv", [80, 50], 1800))
            self.assertFalse(s["frames_known"])
            self.assertAlmostEqual(s["worst_ms"], 20.0)

    def test_a_csv_without_the_columns_is_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "x.csv"
            path.write_text("a,b\n1,2\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                P.summarise(path)

    def test_regressions_over_five_percent(self):
        before = {"avg_fps": 80.0, "worst_ms": 30.0, "peak_gb": 2.0}
        self.assertEqual(P.regressions({"avg_fps": 77.0, "worst_ms": 31.0, "peak_gb": 2.05}, before), [])
        self.assertEqual(len(P.regressions({"avg_fps": 75.0, "worst_ms": 32.0, "peak_gb": 2.2}, before)), 3)
        self.assertEqual(P.regressions({"avg_fps": 1.0, "worst_ms": 99.0, "peak_gb": 9.0}, None), [])

    def test_the_row_needs_the_file_name_convention(self):
        with tempfile.TemporaryDirectory() as tmp:
            good = capture(tmp, "2026-11-02-demo-abc1234.csv", [72], 1000, [14])
            self.assertTrue(P.row(good, P.summarise(good), "Moiz").startswith("| 2026-11-02 | demo | abc1234 | 72.0 |"))
            with self.assertRaises(ValueError):
                P.row(Path(tmp) / "capture.csv", P.summarise(good), "")


if __name__ == "__main__":
    unittest.main(verbosity=1)
