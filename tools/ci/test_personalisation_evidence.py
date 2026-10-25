"""Session evidence (server/personalisation/evidence.py, p1-personalisation-s01): every golden session gives a valid record, the same every time, with no free text and no name."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import jsonschema

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from personalisation import evidence  # noqa: E402

CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]
SCHEMA = json.loads((ROOT / "server" / "schemas" / "session_evidence.json").read_text(encoding="utf-8"))


def cli(*args):
    r = subprocess.run(CLI + list(args), cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    assert r.returncode == 0, r.stdout + r.stderr
    return r


def report_of(case: str, script: Path, tmp: Path, scenario=None) -> dict:
    target = ["--scenario", str(scenario)] if scenario else ["--case", case]
    cli("run", *target, "--seed", "42", "--actions", str(script), "--out", str(tmp / "o.json"), "--allow-draft")
    cli("report", *target, "--log", str(tmp / "o.session.jsonl"), "--out", str(tmp / "r.html"), "--json", str(tmp / "r.json"))
    return json.loads((tmp / "r.json").read_text(encoding="utf-8"))


class Evidence(unittest.TestCase):
    def test_every_p0_correct_session_gives_a_valid_deterministic_record_without_free_text(self):
        scripts = [p for p in sorted((ROOT / "tests" / "golden").glob("*_correct.actions.json")) if (ROOT / "content" / "case_files" / (p.name.removesuffix("_correct.actions.json") + ".json")).exists()][:6]
        self.assertEqual(len(scripts), 6)                                       # the first six P0 correct paths: enough to cover both scenarios without making the check slow
        with tempfile.TemporaryDirectory() as t:
            for script in scripts:
                case = script.name.removesuffix("_correct.actions.json")
                report = report_of(case, script, Path(t))
                a = evidence.extract(report, "trainee-001", "s1", "2026-10-17T10:00:00Z")
                b = evidence.extract(report, "trainee-001", "s1", "2026-10-17T10:00:00Z")
                jsonschema.validate(a, SCHEMA)
                self.assertEqual(a, b, case)
                text = json.dumps(a)
                self.assertNotIn("Rule RUBRIC", text)                     # the sentences are not carried over
                self.assertEqual(a["story"], case)
                self.assertTrue(a["steps"] or a["errors"], case)

    def test_the_evidence_holds_every_accepted_action_not_only_the_scored_ones(self):
        """DT-209: a verb no behaviour names (a bedside examination, for one) is in the evidence, in the order it happened."""
        script = ROOT / "tests" / "golden" / "VAR-S1-A_correct.actions.json"
        with tempfile.TemporaryDirectory() as t:
            report = report_of("VAR-S1-A", script, Path(t))
        ev = evidence.extract(report, "trainee-001", "s1", "2026-10-26T10:00:00Z")
        scored = {x["text"] for x in report["timeline"] if x["kind"] == "action"}
        everything = [a["verb"] for a in ev["actions"]]
        self.assertGreater(len(set(everything) - scored), 0, "at least one accepted verb is not a scored one")
        self.assertEqual(everything, [a["verb"] for a in report["accepted_actions"]][:len(everything)])
        self.assertEqual([a["seq"] for a in ev["actions"]], sorted(a["seq"] for a in ev["actions"]))

    def test_a_generated_session_carries_its_story_and_tier(self):
        pkg = ROOT / "tests" / "fixtures" / "generated" / "GEN-S1-0003-001.package.json"
        with tempfile.TemporaryDirectory() as t:
            report = report_of("", ROOT / "tests" / "golden" / "VAR-S1-G_correct.actions.json", Path(t), scenario=pkg)
        ev = evidence.extract(report, "trainee-007", "s9", "2026-10-17T11:00:00Z")
        jsonschema.validate(ev, SCHEMA)
        self.assertEqual((ev["generated"], ev["story"], ev["tier"], ev["case"]), (True, "VAR-S1-G", "P0", "GEN-S1-0003-001"))
        self.assertNotIn("Helena", json.dumps(ev))

    def test_what_the_engine_does_not_record_is_said_and_a_name_is_refused(self):
        report = {"case": "X", "mode": "practice", "end_state": "READY_SAFE", "result": "SUCCESS", "items": [], "competencies": [], "timeline": [], "lateness": [], "events": ["E-LOW-BP"]}
        ev = evidence.extract(report, "trainee-001", "s", "2026-10-17T10:00:00Z")
        self.assertIn("hints", ev["unrecorded"])
        self.assertEqual(ev["events"], [{"event": "E-LOW-BP", "response_latency_s": None}])
        with self.assertRaises(ValueError):
            evidence.extract(report, "Dr A Person", "s", "2026-10-17T10:00:00Z")

    def test_missed_steps_errors_and_lateness_are_separated(self):
        report = {"case": "X", "mode": "practice", "end_state": "SAFE_BUT_DELAYED", "result": "SUCCESS_WITH_DELAY", "competencies": [{"behaviour": "b14", "label": "x", "credits": 1, "penalties": 3, "net": -2, "weight": 1, "items": 2}],
                  "items": [{"id": "credit.a", "behaviour": "b14", "severity": "credit", "evidence": [1]}, {"id": "missed.b", "behaviour": "b03", "severity": "missed", "evidence": []},
                            {"id": "major.c", "behaviour": "b14", "severity": "major", "evidence": [2]}],
                  "timeline": [{"seq": 1, "time": "08:00:04", "kind": "item", "text": "credit.a", "step": 1}, {"seq": 2, "time": "09:00:49", "kind": "item", "text": "major.c", "step": 2},
                               {"seq": 2, "time": "09:00:49", "kind": "action", "text": "postpone", "step": 2}], "lateness": [{"what": "credit.a", "minutes": 4.5, "source": "SR-0813"}], "events": []}
        ev = evidence.extract(report, "trainee-001", "s", "2026-10-17T10:00:00Z")
        self.assertEqual([(s["item"], s["state"]) for s in ev["steps"]], [("credit.a", "completed"), ("missed.b", "missed")])
        self.assertEqual(ev["steps"][0]["late_minutes"], 4.5)
        self.assertEqual([(e["item"], e["severity"]) for e in ev["errors"]], [("major.c", "major")])
        self.assertEqual(ev["actions"], [{"seq": 2, "at": "09:00:49", "verb": "postpone"}])


if __name__ == "__main__":
    unittest.main()
