"""The Quest sweep converter (tools/unity/perf_sweep.py, P3-PRF-17): deltas are computed against B0 of the same sweep and view only, "n/a" stays "n/a", a file that is not a sweep CSV is refused, and --write replaces only the measured block."""
import importlib.util
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("perf_sweep", ROOT / "tools" / "unity" / "perf_sweep.py")
ps = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ps)

HEADER = "# ProcSense diagnostic build test\nutc,config,label,changes_look,view,room,frames,avg_fps,one_percent_low_fps,avg_frame_ms,max_frame_ms,avg_cpu_ms,avg_gpu_ms\n"


class PerfSweepTests(unittest.TestCase):
    def sweep(self, d: Path, name: str, body: str) -> Path:
        p = d / name
        p.write_text(HEADER + body, encoding="utf-8")
        return p

    def test_delta_against_b0_of_the_same_view(self):
        with tempfile.TemporaryDirectory() as t:
            p = self.sweep(Path(t), "sweep_a.csv",
                           "2026-11-10T10:00:00Z,B0,\"Production\",no,V2,PatientBay,700,70.00,60.00,14.3,20.0,9.00,12.00\n"
                           "2026-11-10T10:00:13Z,B3,\"Shafts off\",yes,V2,PatientBay,720,72.00,64.00,13.9,18.0,9.00,11.00\n"
                           "2026-11-10T10:01:00Z,B5,\"Foveation high\",yes,V4,Corridor,600,60.00,50.00,16.6,25.0,9.50,n/a\n")
            out = ps.table(ps.read([p]))
            self.assertIn("| V2 | B3 Shafts off | yes | 720 | 72.00 | 64.00 | 9.00 | 11.00 | +2.00 | -1.00 |", out)
            self.assertIn("| V4 | B5 Foveation high | yes | 600 | 60.00 | 50.00 | 9.50 | n/a |  |  |", out, "no B0 in V4: no delta is invented")

    def test_a_file_that_is_not_a_sweep_is_refused(self):
        with tempfile.TemporaryDirectory() as t:
            p = Path(t) / "ovr.csv"
            p.write_text("time,fps\n1,72\n", encoding="utf-8")
            with self.assertRaises(SystemExit):
                ps.read([p])

    def test_write_replaces_only_the_measured_block(self):
        with tempfile.TemporaryDirectory() as t:
            res = Path(t) / "results.md"
            res.write_text("before\n" + ps.BEGIN + "\nold\n" + ps.END + "\nafter\n", encoding="utf-8")
            old = ps.RESULTS
            ps.RESULTS = res
            try:
                ps.write("| new |")
            finally:
                ps.RESULTS = old
            self.assertEqual(res.read_text(encoding="utf-8"), "before\n" + ps.BEGIN + "\n| new |\n" + ps.END + "\nafter\n")

    def test_the_results_file_has_the_measured_block(self):
        text = ps.RESULTS.read_text(encoding="utf-8")
        self.assertIn(ps.BEGIN, text)
        self.assertIn(ps.END, text)


if __name__ == "__main__":
    unittest.main()
