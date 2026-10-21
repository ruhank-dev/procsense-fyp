"""The AI feature gate (tools/eval/ai_gate.py, server/ai_features.py; p1-ai-language-s09, plan tests T-EV1 to T-EV3): a missing result is SKIPPED and never passes; a result below its threshold switches the feature off."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "eval"))
sys.path.insert(0, str(ROOT))
import ai_gate  # noqa: E402


class Gate(unittest.TestCase):
    def with_results(self, files: dict):
        d = tempfile.TemporaryDirectory()
        for name, doc in files.items():
            (Path(d.name) / name).write_text(json.dumps(doc), encoding="utf-8")
        self.addCleanup(d.cleanup)
        return Path(d.name)

    def test_a_suite_with_no_result_file_is_skipped_and_skipped_never_passes(self):
        d = self.with_results({})
        out = ai_gate.build(d)
        for name, f in out["features"].items():
            self.assertEqual((f["status"], f["enabled"]), ("SKIPPED", False), name)
            self.assertTrue(all(s["status"] == "SKIPPED" for s in f["suites"]))

    def test_a_result_that_passes_every_suite_switches_the_feature_on_and_one_below_the_line_off(self):
        good = {"first_try_schema_valid_rate": 0.97, "accept_rate": 0.8}
        d = self.with_results({"narrative-4b.json": good})
        self.assertTrue(ai_gate.build(d)["features"]["narrative"]["enabled"])
        d = self.with_results({"narrative-4b.json": {**good, "first_try_schema_valid_rate": 0.9}})
        f = ai_gate.build(d)["features"]["narrative"]
        self.assertEqual((f["status"], f["enabled"]), ("FAIL", False))
        self.assertEqual([s["status"] for s in f["suites"]], ["FAIL", "PASS"])

    def test_a_comparison_with_another_number_and_a_margin(self):
        first = {"recall": {"baseline": 0.9, "union": 0.95, "critic": 0.5}, "critic_false_alarm_rate": 0.05}
        held = {"recall": {"baseline": 0.1, "union": 0.7, "critic": 0.65}}
        ok = ai_gate.build(self.with_results({"critic-eval.json": first, "critic-eval-v2.json": held}))["features"]["critic"]
        self.assertTrue(ok["enabled"])
        worse = {"recall": {"baseline": 0.67, "union": 0.8, "critic": 0.5}}                       # only 13 points above the rules on the held-out set
        f = ai_gate.build(self.with_results({"critic-eval.json": first, "critic-eval-v2.json": worse}))["features"]["critic"]
        self.assertEqual((f["enabled"], f["suites"][0]["status"]), (False, "FAIL"))
        self.assertTrue(f["suites"][0]["needs"].startswith(">= 0.87"))
        lost = {**first, "recall": {"baseline": 0.9, "union": 0.85, "critic": 0.5}}               # the critic must not lose what the rules find
        f = ai_gate.build(self.with_results({"critic-eval.json": lost, "critic-eval-v2.json": held}))["features"]["critic"]
        self.assertEqual((f["enabled"], [s["status"] for s in f["suites"]]), (False, ["PASS", "PASS", "FAIL"]))

    def test_a_result_file_missing_the_field_is_skipped_not_passed(self):
        f = ai_gate.build(self.with_results({"narrative-4b.json": {"accept_rate": 0.9}}))["features"]["narrative"]
        self.assertEqual([s["status"] for s in f["suites"]], ["SKIPPED", "PASS"])
        self.assertFalse(f["enabled"])

    def test_the_committed_features_file_is_what_the_committed_results_give(self):
        self.assertEqual(json.loads((ROOT / "content" / "ai" / "features.json").read_text(encoding="utf-8")), ai_gate.build())

    def test_every_feature_of_the_gate_has_a_route_or_a_stage_that_honours_it(self):
        names = set(json.loads(ai_gate.THRESHOLDS.read_text(encoding="utf-8"))["features"])
        text = "".join(p.read_text(encoding="utf-8") for p in (ROOT / "server" / "routers").glob("*.py")) + (ROOT / "server" / "generation" / "pipeline.py").read_text(encoding="utf-8")
        routed = {n for n in names if f'enabled("{n}")' in text or f'"{n}"' in text}
        self.assertEqual(sorted(names - routed), sorted({"plan_composer", "interpreter", "multi_agent", "plan_extras", "trap_writer"}))        # offline or library-level features; (the pipeline is given a planner or a critic by its caller): listed, so a new unhonoured feature is noticed


if __name__ == "__main__":
    unittest.main()
