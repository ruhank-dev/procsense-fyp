"""Candidate model rules: a gated model never reaches `ready` without recorded terms, and no switch happens without measured parity and a blind rating."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ai"))
import model_candidate as mc  # noqa: E402

REG = mc.registry()
RULE = REG["switch_rule"]
BASE = {"valid_rate": 1.0, "earned_pass": 0.98, "withheld_pass": 1.0, "p50_s": 1.0, "gpu_share": 1.0}


class Gate(unittest.TestCase):
    def test_medgemma_is_gated_and_a_tool_never_writes_the_acceptance(self):
        c = json.loads(json.dumps(REG["candidates"][0]))
        self.assertTrue(c["gated"])
        t = c["terms"]
        if t["accepted_by"]:                                            # a person's statement, recorded as such and as unverified
            self.assertTrue(t["accepted_on"] and t["account_holder_eligibility_confirmed"])
            self.assertIn("unverified", t["note"].lower().replace("not independently verified", "unverified"))
        c["terms"].update(accepted_by=None, accepted_on=None, account_holder_eligibility_confirmed=False)
        state, step = mc.candidate_state(c, {"medgemma-4b-it-q4"}, 6144)
        self.assertEqual(state, "terms_not_recorded")                    # installed or not, it is not ready
        self.assertIn("accept", step)

    def test_recorded_terms_then_install_then_fit(self):
        c = json.loads(json.dumps(REG["candidates"][0]))
        c["terms"].update(accepted_by="A. Holder", accepted_on="2026-11-01", account_holder_eligibility_confirmed=True)
        self.assertEqual(mc.candidate_state(c, set(), 6144)[0], "not_installed")
        self.assertEqual(mc.candidate_state(c, {c["model"]}, 2000)[0], "too_large")
        self.assertEqual(mc.candidate_state(c, {c["model"]}, 6144)[0], "ready")

    def test_half_recorded_terms_are_not_enough(self):
        c = json.loads(json.dumps(REG["candidates"][0]))
        c["terms"].update(accepted_by="A. Holder", accepted_on="2026-11-01", account_holder_eligibility_confirmed=False)          # eligibility not confirmed
        self.assertEqual(mc.candidate_state(c, {c["model"]}, 6144)[0], "terms_not_recorded")


class Decision(unittest.TestCase):
    def test_equal_figures_without_a_rating_are_inconclusive_never_a_switch(self):
        self.assertEqual(mc.decide(BASE, dict(BASE), RULE, None)[0], "INCONCLUSIVE")

    def test_a_leak_or_slowness_blocks_the_switch(self):
        self.assertEqual(mc.decide(BASE, {**BASE, "withheld_pass": 0.95}, RULE, None)[0], "NO_SWITCH")
        self.assertEqual(mc.decide(BASE, {**BASE, "p50_s": 2.0}, RULE, None)[0], "NO_SWITCH")
        self.assertEqual(mc.decide(BASE, {**BASE, "gpu_share": 0.6}, RULE, None)[0], "NO_SWITCH")

    def test_switch_needs_enough_blind_samples_and_a_win(self):
        self.assertEqual(mc.decide(BASE, dict(BASE), RULE, {"samples": 10, "candidate_better": 9, "baseline_better": 1})[0], "INCONCLUSIVE")
        self.assertEqual(mc.decide(BASE, dict(BASE), RULE, {"samples": 50, "candidate_better": 20, "baseline_better": 30})[0], "NO_SWITCH")
        self.assertEqual(mc.decide(BASE, dict(BASE), RULE, {"samples": 50, "candidate_better": 30, "baseline_better": 20})[0], "SWITCH")


class Summary(unittest.TestCase):
    def test_summarise_reads_runs_and_reports_none_when_a_run_is_missing(self):
        with tempfile.TemporaryDirectory() as t:
            art = Path(t)
            self.assertIsNone(mc.summarise("m:1", art))

            def w(test, rows):
                (art / f"20260101T000000Z-{test}-m_1.jsonl").write_text("".join(json.dumps(r) + "\n" for r in rows), encoding="utf-8")
            w("b0", [{"record": "call", "cold": True, "wall_s": 5}, {"record": "call", "cold": False, "wall_s": 1.0}, {"record": "residency", "gpu_share": 1.0}])
            w("b1", [{"record": "call", "valid": True}, {"record": "call", "valid": False}])
            w("b2", [{"record": "call", "condition": "earned", "ok": True}, {"record": "call", "condition": "withheld", "ok": True}])
            s = mc.summarise("m:1", art)
            self.assertEqual((s["valid_rate"], s["earned_pass"], s["withheld_pass"], s["p50_s"], s["gpu_share"]), (0.5, 1.0, 1.0, 1.0, 1.0))


if __name__ == "__main__":
    unittest.main()
