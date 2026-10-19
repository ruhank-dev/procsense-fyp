"""The interpretation benchmark, its page and the speech_interpretation gate (p1-ai-language-s14, ADR-0022). No service is called.
Checked: the state given to a row comes from the behaviour windows of the mapping (a window step; every step for an act no behaviour lists; a waiting question or read-back only for the acts that need one); the committed result files say they are synthetic and
not valid for claims; the page is what the result files give; the gate has the proposed suites, reads the INDEPENDENT file and is SKIPPED and OFF, and a synthetic result file cannot turn it on; the numbers of the three splits agree with the rows they were made from."""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "tools" / "eval"))
import ai_gate  # noqa: E402
import interpretation_bench as B  # noqa: E402
import interpretation_report as REPORT  # noqa: E402

R = ROOT / "reports" / "ai"
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))


class Conditions(unittest.TestCase):
    def test_a_rows_state_comes_from_the_mapping(self):
        for act in {a for b in MAPPING["behaviours"] for a in b["ai_acts"]}:
            c = B.conditions({"act": act})["in_window"]
            self.assertEqual(c.steps, B.window_of(act)[:1], act)
            self.assertIn(act, [e.act for e in c.eligible])
            out = B.conditions({"act": act}).get("out_of_window")
            if out is not None:
                self.assertNotIn(act, [e.act for e in out.eligible])
                self.assertFalse(set(out.steps) & set(B.window_of(act)))

    def test_an_act_no_behaviour_lists_has_every_step_running(self):
        for act in ("CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT"):
            self.assertEqual(B.window_of(act), [])
            self.assertEqual(B.conditions({"act": act})["in_window"].steps, B.ALL_STEPS)
            self.assertNotIn("out_of_window", B.conditions({"act": act}))

    def test_only_the_acts_that_need_them_get_a_waiting_question_or_readback(self):
        c = B.conditions({"act": "ANSWER_QUESTION"})
        self.assertTrue(c["in_window"].patient_question_pending and not c["in_window"].readback_pending)
        self.assertFalse(c["no_context"].patient_question_pending)
        c = B.conditions({"act": "CLOSED_LOOP"})
        self.assertTrue(c["in_window"].readback_pending and not c["no_context"].readback_pending)
        c = B.conditions({"act": "ASK_TOPIC"})
        self.assertFalse(c["in_window"].patient_question_pending or c["in_window"].readback_pending)
        self.assertNotIn("no_context", c)


class Results(unittest.TestCase):
    def test_every_result_is_labelled_synthetic_and_not_valid_for_claims(self):
        for s in ("dev", "test", "test2"):
            d = json.loads((R / f"interpretation-bench-{s}.json").read_text(encoding="utf-8"))
            self.assertTrue(d["synthetic"])
            self.assertFalse(d["independent"])
            self.assertFalse(d["gate_inputs"]["valid_for_claims"])
            self.assertEqual((d["counts"]["legitimate"], d["counts"]["adversarial"], d["counts"]["critical"]), (140, 300, 40))

    def test_the_interpretation_never_offers_more_than_the_route_after_the_window_rule_and_never_a_critical_act(self):
        for s in ("dev", "test", "test2"):
            d = json.loads((R / f"interpretation-bench-{s}.json").read_text(encoding="utf-8"))
            self.assertLessEqual(d["interpretation_in_window"]["offers"], d["classifier_route_as_served"]["offers"])
            self.assertEqual(d["critical"]["offered_in_any_condition"], 0)
            self.assertEqual(d["out_of_window"]["interpretation_offered_any_act"], 0)

    def test_the_page_is_what_the_result_files_give(self):
        self.assertEqual((ROOT / "docs" / "research" / "interpretation_benchmark.md").read_text(encoding="utf-8"), REPORT.render())

    def test_the_latency_report_keeps_the_interpretation_beside_the_reply(self):
        d = json.loads((R / "interpretation-latency.json").read_text(encoding="utf-8"))
        self.assertFalse(d["voice_workflow_from_reports"]["interpretation_in_reply_path"])
        self.assertLessEqual(d["warm_while_4b_generates_ms"]["whole"]["p95"], 300)


class Gate(unittest.TestCase):
    thresholds = json.loads((ROOT / "content" / "ai" / "thresholds.json").read_text(encoding="utf-8"))["features"]

    def test_the_gate_has_the_proposed_suites_and_reads_the_independent_file(self):
        ids = {s["id"] for s in self.thresholds["speech_interpretation"]["suites"]}
        for needed in ("critical_false_credit", "out_of_window_false_credit", "non_assertive_false_credit_rate", "credited_precision", "legitimate_recall_not_below_route", "legitimate_recall_floor", "latency_p95_ms", "independent_evidence"):
            self.assertIn("speech_interpretation." + needed, ids)
        for s in self.thresholds["speech_interpretation"]["suites"]:
            if s["file"] != "interpretation-latency.json":
                self.assertEqual(s["file"], "interpretation-bench-independent.json")
            self.assertIn("PENDING", s["ref"] if s["id"] != "speech_interpretation.independent_evidence" else "PENDING")

    def test_the_feature_is_off_and_skipped_today(self):
        f = json.loads((ROOT / "content" / "ai" / "features.json").read_text(encoding="utf-8"))["features"]["speech_interpretation"]
        self.assertFalse(f["enabled"])
        self.assertEqual(f["status"], "SKIPPED")

    def test_a_synthetic_result_cannot_switch_it_on(self):
        import tempfile
        with tempfile.TemporaryDirectory() as t:
            tmp = Path(t)
            for s in ("dev", "test", "test2"):
                (tmp / "interpretation-bench-independent.json").write_text((R / f"interpretation-bench-{s}.json").read_text(encoding="utf-8"), encoding="utf-8")
                (tmp / "interpretation-latency.json").write_text((R / "interpretation-latency.json").read_text(encoding="utf-8"), encoding="utf-8")
                built = ai_gate.build(tmp)["features"]["speech_interpretation"]
                self.assertFalse(built["enabled"], s)
                self.assertEqual({x["id"]: x["status"] for x in built["suites"]}["speech_interpretation.independent_evidence"], "FAIL")


if __name__ == "__main__":
    unittest.main()
