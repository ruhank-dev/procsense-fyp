"""The safety principles of the AI work, each held by a check in more than one layer (owner, 18 October 2026): deterministic scoring is authoritative; no model, critic or synthetic evaluator can grant critical-action
credit; generated scenarios pass the deterministic checks; a feature stays off while its acceptance, permission or validation is unmet. Where a principle is tested in depth elsewhere this file names the test and fails
if that test disappears, and adds the cross-file facts no single test sees."""
import json
import re
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "server"))


def j(p):
    return json.loads((ROOT / p).read_text(encoding="utf-8"))


class CriticalCredit(unittest.TestCase):
    def test_the_taxonomy_and_the_scoring_mapping_agree_on_which_acts_are_critical(self):
        tax = {a["id"] for a in j("content/assessment/taxonomy.json")["acts"] if a["critical"]}
        self.assertEqual(tax, set(j("content/assessment/mapping.json")["critical_acts"]))

    def test_the_core_tests_that_hold_it_still_exist(self):
        text = (ROOT / "tests" / "ProcSense.Core.Tests" / "ScoringTests.cs").read_text(encoding="utf-8")
        for name in ("T_EM1_a_critical_act_from_speech_is_recorded_and_graded_but_not_credited", "T_EM1b_a_perfect_sbar_element", "T_EM1c_no_critical_act_is_accepted_as_evidence_from_speech"):
            self.assertIn(name, text)

    def test_the_evaluator_route_returns_a_judgement_and_never_a_score_or_a_credit(self):
        out = (ROOT / "server" / "schemas" / "evaluate.py").read_text(encoding="utf-8")
        for field in ("score", "credit", "points", "confidence", "grade"):
            self.assertNotRegex(out.split("class JudgedActOut")[1], rf"^\s+{field}", field)
        for f in (ROOT / "server" / "evaluate").glob("*.py"):
            self.assertNotRegex(f.read_text(encoding="utf-8"), r"^\s*(from|import)\s+(server\.)?(classify|interpret)", f.name)

    def test_a_critical_act_named_by_the_evaluator_is_never_accepted_by_the_engine(self):
        text = (ROOT / "src" / "ProcSense.Core" / "Scoring" / "Evidence.cs").read_text(encoding="utf-8")
        self.assertIn("CriticalNeedsAction", text)
        self.assertIn("critical_acts", (ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
        rub = j("content/assessment/mapping.json")
        self.assertTrue(set(rub["critical_act_evidence"]) <= set(rub["critical_acts"]))                 # the physical part of a critical act comes only from the actions named here

    def test_the_core_has_no_dependency_on_the_server_or_a_model(self):
        for f in (ROOT / "src" / "ProcSense.Core").rglob("*.cs"):
            text = f.read_text(encoding="utf-8")
            self.assertNotRegex(text, r"using\s+(Ollama|OpenAI|System\.Net\.Http)\b", f.name)
            self.assertNotRegex(text, r"\b(HttpClient|WebRequest)\b", f.name)


class Generation(unittest.TestCase):
    def test_the_deterministic_checks_run_without_a_model_and_a_silent_critic_cannot_let_a_contradiction_through(self):
        text = (ROOT / "tools" / "ci" / "test_generation_consistency.py").read_text(encoding="utf-8")
        self.assertIn("Silent", text)                                                  # the critic that says nothing
        self.assertIn("consistency.check", (ROOT / "server" / "generation" / "pipeline.py").read_text(encoding="utf-8"))
        # the always-on rules are called before, and independently of, the model critic
        src = (ROOT / "server" / "generation" / "pipeline.py").read_text(encoding="utf-8")
        self.assertLess(src.index("consistency.check"), src.index("ctx.critic.review"))


class Gate(unittest.TestCase):
    FEATURES = j("content/ai/features.json")["features"]

    def test_a_feature_is_on_only_when_every_suite_passed(self):
        for name, f in self.FEATURES.items():
            self.assertEqual(f["enabled"], all(s["status"] == "PASS" for s in f["suites"]) and bool(f["suites"]) and not f.get("informational"), name)      # an informational feature is recorded and never switched on

    def test_the_speech_evaluation_feature_is_on_only_through_its_acceptance_record_and_its_safety_suites_are_absolute(self):
        f = self.FEATURES["speech_evaluation"]
        self.assertEqual(f["enabled"], all(s["status"] == "PASS" for s in f["suites"]) and bool(f["suites"]))
        suites = {s["id"].split(".", 1)[1]: s for s in f["suites"]}
        self.assertEqual((suites["critical_physical_completions"]["needs"], suites["window_state_enforcement"]["needs"]), ("== 0.0", "== 0.0"))      # the two blocking criteria never become a rate
        acc = j("reports/ai/speech-evaluation-acceptance.json")
        self.assertEqual((acc["final_verdict"], acc["same_prompt_in_both_runs"]), ("APPROVED", True))
        for name in self.FEATURES:
            self.assertFalse(name.startswith("classifier") or name == "speech_interpretation", name)             # the retired classifier and interpreter features are gone from the gate

    def test_every_feature_label_that_says_not_independent_is_off(self):
        for name, f in self.FEATURES.items():
            if "NOT independent" in f["label"]:
                self.assertFalse(f["enabled"], name)

    def test_a_gated_model_candidate_acceptance_is_a_persons_statement_recorded_as_such(self):
        for c in j("content/ai/model_candidates.json")["candidates"]:
            t = c["terms"]
            if c["gated"] and t["accepted_by"]:                               # only a person with the right to accept writes this, with date, eligibility and what was not checked
                self.assertTrue(t["accepted_on"] and t["account_holder_eligibility_confirmed"], c["id"])
                self.assertIn("not independently verified", t["note"], c["id"])

    def test_live_drug_availability_is_off_and_no_source_is_permitted(self):
        self.assertFalse(j("content/availability/config.json")["live_enabled"])
        self.assertEqual(j("content/availability/permitted_sources.json")["sources"], [])


if __name__ == "__main__":
    unittest.main()
