"""Drug availability (server/availability): registration is never stock, synthetic is never live, stale is unknown, and a source that does not permit the use is refused."""
import csv
import sys
import tempfile
import unittest
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from server.availability import service  # noqa: E402
from server.availability.provider import Availability, unknown  # noqa: E402
from server.availability.sources import FileProvider, SyntheticProvider, load_config, load_permitted  # noqa: E402

NOW = datetime(2026, 10, 18, 12, 0, tzinfo=timezone.utc)
SRC = {"id": "pharm-1", "kind": "pharmacist_supplied", "allowed_uses": ["teaching_display"], "attribution": "Supplied by the hospital pharmacist"}


def write_csv(rows, header=("medicine_id", "status", "observed_on", "source_id")):
    d = tempfile.mkdtemp()
    p = Path(d) / "a.csv"
    with open(p, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(header)
        w.writerows(rows)
    return p


class Shipped(unittest.TestCase):
    def test_live_is_off_and_no_source_is_permitted(self):
        self.assertFalse(load_config()["live_enabled"])
        self.assertEqual(load_permitted(), {})                         # DRAP's registry is on the excluded list, not the permitted one

    def test_the_synthetic_file_says_so_and_nothing_resolves_without_demo_mode(self):
        s = SyntheticProvider()
        a = service.resolve("dabigatran", [s], load_config(), NOW)
        self.assertEqual((a.status, a.source_kind), ("unknown", "none"))
        d = service.resolve("dabigatran", [s], load_config(), NOW, demo=True)
        self.assertTrue(d.synthetic and not d.live)
        self.assertIn("SYNTHETIC", service.note(d, load_config()))


class Rules(unittest.TestCase):
    def test_a_registration_register_cannot_hold_a_stock_status(self):
        with self.assertRaises(ValueError):
            Availability("x", "available", "dra", "registration_register", NOW, 30)
        self.assertEqual(Availability("x", "unknown", "dra", "registration_register", NOW, 30).status, "unknown")

    def test_a_row_from_a_register_source_is_refused(self):
        reg = {"dra": {"id": "dra", "kind": "registration_register", "allowed_uses": ["teaching_display"]}}
        p = FileProvider(write_csv([("warfarin", "available", "2026-10-17", "dra")]), reg, "dra", 30, NOW)
        self.assertIsNone(p.lookup("warfarin"))
        self.assertTrue(any("registration register" in why for _, why in p.rejected))

    def test_unlisted_or_disallowed_sources_are_refused(self):
        self.assertTrue(FileProvider(write_csv([]), {}, "ghost", 30, NOW).rejected)
        no_use = {"s": {**SRC, "id": "s", "allowed_uses": ["research"]}}
        self.assertTrue(any("teaching display" in why for _, why in FileProvider(write_csv([]), no_use, "s", 30, NOW).rejected))

    def test_bad_rows_are_rejected_with_a_reason_and_good_rows_kept(self):
        p = FileProvider(write_csv([("a", "available", "2026-10-17", "pharm-1"), ("b", "maybe", "2026-10-17", "pharm-1"),
                                    ("c", "available", "17/10/2026", "pharm-1"), ("d", "available", "2026-12-01", "pharm-1"),
                                    ("e", "available", "2026-10-17", "other")]), {"pharm-1": SRC}, "pharm-1", 30, NOW)
        self.assertEqual(p.lookup("a").status, "available")
        self.assertEqual([n for n, _ in p.rejected], [3, 4, 5, 6])

    def test_live_answers_need_the_switch_and_a_fresh_observation(self):
        p = FileProvider(write_csv([("a", "unavailable", "2026-10-17", "pharm-1"), ("old", "available", "2026-08-01", "pharm-1")]), {"pharm-1": SRC}, "pharm-1", 30, NOW)
        off, on = {"live_enabled": False}, {"live_enabled": True}
        self.assertEqual(service.resolve("a", [p], off, NOW).status, "unknown")           # switched off: a real row is ignored
        got = service.resolve("a", [p], on, NOW)
        self.assertTrue(got.live and got.status == "unavailable")
        self.assertIn("not available locally", service.note(got, on))
        stale = service.resolve("old", [p], on, NOW)
        self.assertEqual(stale.status, "unknown")
        self.assertIn("older than 30 days", stale.note)
        self.assertEqual(service.resolve("missing", [p], on, NOW).status, "unknown")

    def test_freshness_boundary(self):
        a = Availability("a", "available", "pharm-1", "pharmacist_supplied", NOW - timedelta(days=30), 30)
        self.assertTrue(service.fresh(a, NOW))
        self.assertFalse(service.fresh(a, NOW + timedelta(seconds=1)))

    def test_unknown_has_no_note_and_an_available_note_is_silent(self):
        on = {"live_enabled": True}
        self.assertEqual(service.note(unknown("a", "x"), on), "")
        ok = Availability("a", "available", "pharm-1", "pharmacist_supplied", NOW, 30)
        self.assertEqual(service.note(ok, on), "")

    def test_synthetic_can_never_be_live(self):
        s = Availability("a", "available", "synthetic-demo", "synthetic", NOW, 30)
        self.assertFalse(s.live)


if __name__ == "__main__":
    unittest.main()
