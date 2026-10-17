"""History, the generated debrief and the offline evaluation loop (server/generation/history.py, tools/generation/evaluate.py; p1-live-scenario-generation-s12, stages LG-19 to LG-21)."""
import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import jsonschema

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tools" / "generation"))
from generation import history as H, objectives as ob, package, pipeline, retrieve  # noqa: E402
from composer import blocks as B  # noqa: E402
import evaluate as E  # noqa: E402

CASES = B.load_cases()
TABLES = ob.Tables.load()
CTX = pipeline.Context(retrieve.build_index(), CASES, TABLES)
FIXTURE = ROOT / "tests" / "fixtures" / "generated" / "GEN-S1-0003-001.package.json"
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def cli(*args):
    return subprocess.run(CLI + list(args), cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")


def play_report(tmp: Path) -> dict:
    run = cli("run", "--scenario", str(FIXTURE), "--seed", "42", "--actions", str(ROOT / "tests" / "golden" / "VAR-S1-G_correct.actions.json"), "--out", str(tmp / "o.json"))
    assert run.returncode == 0, run.stdout + run.stderr
    rep = cli("report", "--scenario", str(FIXTURE), "--log", str(tmp / "o.session.jsonl"), "--out", str(tmp / "r.html"), "--json", str(tmp / "r.json"))
    assert rep.returncode == 0, rep.stdout + rep.stderr
    return json.loads((tmp / "r.json").read_text(encoding="utf-8"))


class Debrief(unittest.TestCase):
    def test_a_generated_session_report_says_where_the_case_came_from_and_is_valid(self):
        with tempfile.TemporaryDirectory() as t:
            report = play_report(Path(t))
            schema = json.loads((ROOT / "content" / "schema" / "debrief_facts.schema.json").read_text(encoding="utf-8"))
            jsonschema.validate(report, schema)
            self.assertEqual(report["generated"]["spine"], "VAR-S1-G")
            self.assertEqual(report["generated"]["status"], "GENERATED_PRACTICE_ONLY")
            self.assertEqual(report["generated"]["package_sha256"], package.file_sha256(FIXTURE))
            page = (Path(t) / "r.html").read_text(encoding="utf-8")
            self.assertIn("Generated practice case", page)
            self.assertIn("not used for assessment", page)

    def test_a_library_session_report_has_no_generated_section(self):
        with tempfile.TemporaryDirectory() as t:
            t = Path(t)
            self.assertEqual(cli("run", "--case", "VAR-S1-G", "--seed", "42", "--actions", str(ROOT / "tests" / "golden" / "VAR-S1-G_correct.actions.json"), "--out", str(t / "o.json")).returncode, 0)
            self.assertEqual(cli("report", "--log", str(t / "o.session.jsonl"), "--out", str(t / "r.html"), "--json", str(t / "r.json")).returncode, 0)
            self.assertNotIn("generated", json.loads((t / "r.json").read_text(encoding="utf-8")))

    def test_the_engine_history_keeps_the_tier_of_a_generated_case(self):
        with tempfile.TemporaryDirectory() as t:
            t = Path(t)
            report = play_report(t)
            hist = t / "h" / "trainee-001.json"
            r = cli("record-session", "--history", str(t / "h"), "--trainee", "trainee-001", "--facts", str(t / "r.json"))
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            doc = json.loads(hist.read_text(encoding="utf-8"))
            self.assertEqual(doc["sessions"][0]["case"], report["case"])
            self.assertEqual(doc["sessions"][0]["tier"], "P0")


class History(unittest.TestCase):
    def package(self):
        return json.loads(FIXTURE.read_text(encoding="utf-8"))

    def test_an_entry_from_a_generated_report_holds_the_package_and_no_name(self):
        with tempfile.TemporaryDirectory() as t:
            report = play_report(Path(t))
        pkg = self.package()
        e = H.entry_from_report("trainee-001", "s-1", report, "2026-10-17T10:00:00Z", pkg)
        self.assertEqual((e.story, e.tier, e.package_id, e.fingerprint), ("VAR-S1-G", "P0", "GEN-S1-0003-001", pkg["fingerprint"]))
        self.assertEqual(e.levels, pkg["fingerprint_levels"])
        text = json.dumps(e.as_dict())
        self.assertNotIn(pkg["case"]["identity"]["full_name"], text)
        self.assertRegex(e.ended_at, r"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$")

    def test_the_wrong_package_or_a_name_for_a_pseudonym_is_refused(self):
        with tempfile.TemporaryDirectory() as t:
            report = play_report(Path(t))
        pkg = self.package()
        with self.assertRaises(H.HistoryError):
            H.entry_from_report("Dr A. Person", "s", report, None, pkg)
        with self.assertRaises(H.HistoryError):
            H.entry_from_report("trainee-001", "s", report, None, None)
        with self.assertRaises(H.HistoryError):
            H.entry_from_report("trainee-001", "s", report, None, pkg, package_file_sha256="0" * 64)

    def test_the_store_round_trips_forgets_and_feeds_the_next_request(self):
        pkg = self.package()
        e = H.HistoryEntry("trainee-007", "s1", pkg["case"]["id"], "VAR-S1-G", "P0", {"b01": [1, 3], "b02": [4, 0]}, "2026-10-17T10:00:00Z", pkg["id"], "", pkg["fingerprint"], pkg["fingerprint_levels"])
        with tempfile.TemporaryDirectory() as t:
            s = H.FileHistory(Path(t))
            s.append(e)
            self.assertEqual(s.entries("trainee-007"), [e])
            self.assertEqual(s.entries("trainee-999"), [])
            rec = H.session_records(s.entries("trainee-007"))
            self.assertEqual((rec[0].case, rec[0].tier, rec[0].behaviours["b01"]), ("VAR-S1-G", "P0", (1, 3)))
            self.assertEqual(H.recent_fingerprints(s.entries("trainee-007")), [pkg["fingerprint"]])
            req = {"version": 1, "trainee": "trainee-007", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": 5}
            r = pipeline.generate(req, rec, H.novelty_history(s.entries("trainee-007")), CTX, recent_fingerprints=H.recent_fingerprints(s.entries("trainee-007")))
            self.assertNotEqual(r.package["provenance"]["spine"], "VAR-S1-G")                  # the story just played is not offered again
            self.assertTrue(s.forget("trainee-007"))
            self.assertFalse(s.forget("trainee-007"))
            self.assertEqual(s.entries("trainee-007"), [])

    def test_a_damaged_line_is_reported_and_a_path_trick_is_refused(self):
        with tempfile.TemporaryDirectory() as t:
            (Path(t) / "trainee-001.jsonl").write_text('{"trainee": \n', encoding="utf-8")
            with self.assertRaises(H.HistoryError):
                H.FileHistory(Path(t)).entries("trainee-001")
            with self.assertRaises(H.HistoryError):
                H.FileHistory(Path(t)).path("../trainee-001")


class Evaluation(unittest.TestCase):
    def test_a_batch_makes_a_report_with_a_stable_id_and_proposals_that_cite_numbers(self):
        a = E.make_report(E.aggregate(E.simulate(30, 5)), "test", 51)
        b = E.make_report(E.aggregate(E.simulate(30, 5)), "test", 51)
        self.assertEqual(a["id"], b["id"])
        self.assertRegex(a["id"], r"^EVAL-[0-9a-f]{12}$")
        for p in a["proposals"]:
            self.assertTrue(p["evidence"] and p["why"] and p["status"] == "proposed")
        self.assertIn(a["id"], E.to_markdown(a))

    def test_proposals_follow_the_numbers(self):
        base = {"requests": 100, "served": 100, "fallbacks": 0, "fallback_rate": 0.0, "repaired": 0, "repair_by_strategy": {}, "defect_codes_in_fallbacks": {}, "defect_codes_repaired": {}, "novel": 100,
                "low_novelty": 0, "novelty_rate": 1.0, "mean_nearest": 0.5, "spine_use": {"A": 50, "B": 50}, "metric_mean": {}, "metric_min": {}, "not_measured": {}, "unmet_dimensions": {}}
        self.assertEqual(E.propose(base), [])
        worse = {**base, "fallbacks": 10, "fallback_rate": 0.1, "defect_codes_in_fallbacks": {"Q_X": 10}, "low_novelty": 9, "novelty_rate": 0.91,
                 "repair_by_strategy": {"swap_spine": {"tries": 10, "passed": 0}, "redraw_numbers": {"tries": 10, "passed": 1}}, "spine_use": {"A": 70, "B": 10, "C": 10, "D": 10}, "unmet_dimensions": {"hidden_facts": 30}}
        kinds = [(p["kind"], p["target"]) for p in E.propose(worse)]
        self.assertIn(("block_or_spine", "the defects that end in a fallback"), kinds)
        self.assertIn(("repair_table", "swap_spine"), kinds)
        self.assertIn(("repair_table", "redraw_numbers"), kinds)
        self.assertIn(("block_weight", "A"), kinds)
        self.assertIn(("content", "hidden_facts"), kinds)
        self.assertTrue(any(k[0] == "threshold" for k in kinds))

    def test_releases_cite_reports_that_exist_and_the_packs_match_the_latest_release(self):
        self.assertEqual(E.check_releases(), [])

    def test_changed_packs_without_a_release_are_caught(self):
        real = E.packs_hash
        try:
            E.packs_hash = lambda: "0" * 16
            self.assertTrue(any("packs changed" in p for p in E.check_releases()))
        finally:
            E.packs_hash = real

    def test_nothing_in_the_server_can_run_the_loop_inside_a_session(self):
        for path in (ROOT / "server").rglob("*.py"):
            if "migrations" in path.parts or "__pycache__" in path.parts or ".venv" in path.parts:
                continue
            self.assertIsNone(re.search(r"^\s*(from|import)\s+(tools\.)?generation\.evaluate|import evaluate\b", path.read_text(encoding="utf-8"), re.M), path)


if __name__ == "__main__":
    unittest.main()
