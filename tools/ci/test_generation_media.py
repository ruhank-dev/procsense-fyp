"""Picture and ECG selection (server/generation/media.py, p1-live-scenario-generation-s06, stage LG-08)."""
import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from generation import media  # noqa: E402

PICS = media.pictures()
REPS = media.reports()
CASES = {json.loads(p.read_text(encoding="utf-8"))["id"]: json.loads(p.read_text(encoding="utf-8")) for p in (ROOT / "content" / "case_files").glob("*.json")}


def card_of(spine):
    c = copy.deepcopy(CASES[spine])
    c["id"] = "GEN-S1-0001-001"
    return c


class Media(unittest.TestCase):
    def test_every_slot_of_every_authored_case_gets_a_picture_of_its_finding(self):
        missing = []
        for spine in CASES:
            for seed in range(5):
                m = media.select_media(card_of(spine), spine, seed, PICS, REPS)
                for s in m["slots"]:
                    if s["picture_id"] is None:
                        missing.append((spine, s["test"], s["finding_code"]))
                    else:
                        p = next(x for x in PICS if x["id"] == s["picture_id"])
                        self.assertEqual(p["finding_code"], s["finding_code"])
        # written-only tests have no pictures: those slots say so instead of inventing one
        self.assertTrue(all(t in ("pregnancy_test", "urine_dipstick", "blood_sample_appearance") for _, t, _ in missing), set(missing))

    def test_zero_tag_mismatches_and_a_licence_row_for_every_picked_asset(self):
        for spine in CASES:
            c = card_of(spine)
            m = media.select_media(c, spine, 3, PICS, REPS)
            self.assertEqual(media.check_media(c, m, PICS, REPS), [], spine)
            for s in m["slots"]:
                if s["picture_id"]:
                    self.assertTrue(media.licence_ok(s["licence"]), s)

    def test_the_same_seed_gives_the_same_pictures_and_other_seeds_vary_them(self):
        a = media.select_media(card_of("VAR-S1-A"), "VAR-S1-A", 1, PICS, REPS)
        b = media.select_media(card_of("VAR-S1-A"), "VAR-S1-A", 1, PICS, REPS)
        self.assertEqual(a, b)
        seen = {media.select_media(card_of("VAR-S1-A"), "VAR-S1-A", s, PICS, REPS)["slots"][0]["picture_id"] for s in range(40)}
        self.assertGreater(len(seen), 1)

    def test_real_pictures_come_before_synthetic_ones(self):
        for spine in CASES:
            m = media.select_media(card_of(spine), spine, 2, PICS, REPS)
            for s in m["slots"]:
                if s["picture_id"] is None:
                    continue
                real_available = any(p["finding_code"] == s["finding_code"] and p["real"] == "true" and p["quality_grade"] != "C" and media.licence_ok(p["licence"]) for p in PICS)
                if real_available:
                    self.assertTrue(s["real"], (spine, s["test"]))

    def test_a_forbidden_licence_or_a_low_status_is_dropped(self):
        pics = copy.deepcopy(PICS)
        for p in pics:
            if p["finding_code"] == "US-GB-01":
                p["licence"] = "CC BY-NC 4.0"
        m = media.select_media(card_of("VAR-S1-A"), "VAR-S1-A", 1, pics, REPS)
        self.assertIsNone(m["slots"][0]["picture_id"])
        self.assertEqual(m["log"][0]["action"], "no_picture")
        self.assertFalse(media.licence_ok("Research use only"))
        self.assertTrue(media.licence_ok("CC BY 4.0"))
        pics = copy.deepcopy(PICS)
        for p in pics:
            if p["finding_code"] == "US-GB-01":
                p["status"] = "DRAFT"
        self.assertIsNone(media.select_media(card_of("VAR-S1-A"), "VAR-S1-A", 1, pics, REPS)["slots"][0]["picture_id"])

    def test_a_wrong_finding_or_a_changed_report_is_caught(self):
        c = card_of("VAR-S1-A")
        m = media.select_media(c, "VAR-S1-A", 1, PICS, REPS)
        bad = copy.deepcopy(m)
        bad["slots"][0]["finding_code"] = "US-GB-02"
        self.assertTrue(any("needs US-GB-02" in d for d in media.check_media(c, bad, PICS, REPS)))
        bad = copy.deepcopy(m)
        bad["slots"][0]["picture_id"] = "no-such-picture"
        self.assertTrue(any("not in the library" in d for d in media.check_media(c, bad, PICS, REPS)))

    def test_an_ecg_strip_and_the_pulse_agree_within_5_when_the_pulse_is_free_to_move(self):
        slot = [{"test": "ecg", "finding_code": "ECG-01", "basis": "book", "ref": "x"}]          # no authored case has a normal-rhythm ECG slot with a free pulse: this is the situation made up
        moved = 0
        for seed in range(60):
            c = card_of("VAR-S1-A")
            c["observations"][0]["heart_rate"] = 51          # the four ECG-01 strips measure 64 or more, so the pulse has to move
            self.assertTrue(media._pulse_free(c))
            m = media.select_media(c, "VAR-S1-A", seed, PICS, REPS, slot)
            strip = float(next(s for s in m["slots"] if s["test"] == "ecg")["measurements"]["rate_bpm"])
            if media.PULSE_RANGE[0] <= strip <= media.PULSE_RANGE[1]:          # a strip outside the range that scores 0 cannot be matched by a normal pulse
                self.assertLessEqual(abs(strip - c["observations"][0]["heart_rate"]), media.ECG_TOLERANCE, seed)
                self.assertEqual(media.check_media(c, m, PICS, REPS), [])
            moved += any(e["action"] == "pulse_moved_to_the_strip" for e in m["log"])
        self.assertGreater(moved, 0)

    def test_authored_cases_with_an_ecg_never_have_a_pulse_changed_against_the_story(self):
        for spine in [s for s in CASES if any(x["test"] == "ecg" for x in media.slots_of(s))]:
            for seed in range(10):
                c = card_of(spine)
                before = c["observations"][0]["heart_rate"]
                free = media._pulse_free(c)
                m = media.select_media(c, spine, seed, PICS, REPS)
                if not free:
                    self.assertEqual(c["observations"][0]["heart_rate"], before, spine)
                self.assertEqual(media.check_media(c, m, PICS, REPS), [], spine)

    def test_a_pulse_named_in_the_story_is_not_moved(self):
        c = card_of("VAR-S1-A")
        c["presentation"]["summary"] += f" Her pulse is {c['observations'][0]['heart_rate']}."
        before = c["observations"][0]["heart_rate"]
        media.select_media(c, "VAR-S1-A", 1, PICS, REPS)
        self.assertEqual(c["observations"][0]["heart_rate"], before)


if __name__ == "__main__":
    unittest.main()
