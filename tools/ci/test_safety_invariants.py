"""The safety principles of the AI work, each held by a check in more than one layer (owner, 18 October 2026): deterministic scoring is authoritative; no model, classifier, critic or synthetic evaluator can grant critical-action
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
from classify.classifier import ActClassifier  # noqa: E402


def j(p):
    return json.loads((ROOT / p).read_text(encoding="utf-8"))


class CriticalCredit(unittest.TestCase):
    def test_the_taxonomy_and_the_scoring_mapping_agree_on_which_acts_are_critical(self):
        tax = {a["id"] for a in j("content/assessment/taxonomy.json")["acts"] if a["critical"]}
        self.assertEqual(tax, set(j("content/assessment/mapping.json")["critical_acts"]))

    def test_the_core_tests_that_hold_it_still_exist(self):
        text = (ROOT / "tests" / "ProcSense.Core.Tests" / "ScoringTests.cs").read_text(encoding="utf-8")
        for name in ("T_EM1_a_critical_act_from_speech_is_recorded_but_not_credited", "T_EM1b_a_confident_sbar_element", "T_EM1c_no_critical_act_is_credited_from_speech_at_any_confidence"):
            self.assertIn(name, text)

    def test_the_classifier_service_never_credits_a_critical_act_at_any_confidence(self):
        clf = ActClassifier(None, thresholds={"per_act": {a["id"]: 0.0 for a in j("content/assessment/taxonomy.json")["acts"]}})
        for a in j("content/assessment/taxonomy.json")["acts"]:
            if a["critical"] or a["id"] == "NONE":
                self.assertFalse(clf.credit_allowed(a["id"], 1.0), a["id"])

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
            self.assertEqual(f["enabled"], all(s["status"] == "PASS" for s in f["suites"]) and bool(f["suites"]), name)

    def test_the_classifier_features_are_off_and_synthetic_evidence_cannot_switch_anything_on(self):
        for name, f in self.FEATURES.items():
            if name.startswith("classifier"):
                self.assertFalse(f["enabled"], name)
        for name in ("classifier", "classifier_synthetic", "classifier_synthetic2"):
            ids = {s["id"] for s in self.FEATURES[name]["suites"]}
            self.assertTrue(any("independent" in i for i in ids) or name == "classifier_synthetic", name)

    def test_every_feature_label_that_says_not_independent_is_off(self):
        for name, f in self.FEATURES.items():
            if "NOT independent" in f["label"]:
                self.assertFalse(f["enabled"], name)

    def test_a_gated_model_candidate_has_no_recorded_acceptance(self):
        for c in j("content/ai/model_candidates.json")["candidates"]:
            if c["gated"]:
                self.assertIsNone(c["terms"]["accepted_by"], c["id"])         # only a person with the right to accept writes this

    def test_live_drug_availability_is_off_and_no_source_is_permitted(self):
        self.assertFalse(j("content/availability/config.json")["live_enabled"])
        self.assertEqual(j("content/availability/permitted_sources.json")["sources"], [])


if __name__ == "__main__":
    unittest.main()
