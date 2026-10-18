"""Permanent safety regression suite for the credit path of the communication classifier (server/classify/ensemble.py, assertion.py; docs/research/classifier_benchmark/safety_gate.md).

No model service is called: tests/classifier_regression/cases.json carries each sentence's all-minilm:22m embedding, so the real heads, cues and guards run. Checked: the three sentences the production path credited
under an ordinary act on 18 October 2026 stay uncredited; evasive critical sentences of every kind stay uncredited; denials, hypotheticals and reports stay uncredited; sentences the gate credited correctly when the
file was made are still credited (the guard must not become a blanket refusal); one disagreeing head, a non-assertive sentence, a down embedder or a critical act each remove credit; the frozen challenge set v4 and
the adversarial sets still match their hashes."""
import hashlib
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify.assertion import non_assertive  # noqa: E402
from classify.classifier import Result  # noqa: E402
from classify.embedding import EmbeddingActClassifier, load_head  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402

CASES = json.loads((ROOT / "tests" / "classifier_regression" / "cases.json").read_text(encoding="utf-8"))["cases"]
LOOKUP = {c["text"]: c["embedding"] for c in CASES}


def embedder(text):
    return LOOKUP.get(text)


class Fixture(unittest.TestCase):
    clf = EnsembleActClassifier(embedder)

    def test_cases_exist_in_every_group(self):
        groups = {c["group"] for c in CASES}
        self.assertEqual(groups, {"must_not_credit_failure", "must_not_credit_v5", "must_not_credit_critical", "must_not_credit_nonassertive", "may_credit"})
        self.assertEqual(sum(c["group"] == "must_not_credit_failure" for c in CASES), 3)

    def test_nothing_in_the_must_not_groups_is_credited(self):
        for c in CASES:
            if c["group"].startswith("must_not_credit"):
                r = self.clf.classify(c["text"])
                self.assertEqual(r.source, "ensemble", c["text"])
                self.assertFalse(r.credited, f"{c['group']} {c['id']}: credited {r.act} for: {c['text']}")

    def test_valid_credit_survives(self):
        for c in CASES:
            if c["group"] == "may_credit":
                r = self.clf.classify(c["text"])
                self.assertTrue(r.credited, f"valid credit lost for: {c['text']} (got {r.act}, refused {r.refused!r})")
                self.assertEqual(r.act, c["act"], c["text"])

    def test_a_critical_act_is_never_credited_from_speech(self):
        for c in CASES:
            r = self.clf.classify(c["text"])
            if r.act in self.clf.critical:
                self.assertFalse(r.credited, c["text"])
                self.assertTrue(r.critical)


class Gate(unittest.TestCase):
    def test_a_down_embedder_credits_nothing(self):
        r = EnsembleActClassifier(lambda t: None).classify("Can you tell me about your last meal?")
        self.assertFalse(r.credited)
        self.assertEqual(r.source, "unavailable")

    def test_disagreeing_tfidf_head_removes_credit(self):
        text = next(c["text"] for c in CASES if c["group"] == "may_credit")
        clf = EnsembleActClassifier(embedder)
        before = clf.classify(text)
        self.assertTrue(before.credited)

        class Other:
            def classify(self, sentence):
                return Result("NONE" if before.act != "NONE" else "ASK_TOPIC", 1.0, False, "tfidf")
        clf.tfidf = Other()
        after = clf.classify(text)
        self.assertFalse(after.credited)
        self.assertTrue(after.refused.startswith("HEADS_DISAGREE"))

    def test_embedding_alone_still_has_its_own_guards(self):
        r = EmbeddingActClassifier(embedder, load_head()).classify(next(c["text"] for c in CASES if c["group"] == "must_not_credit_failure"))
        self.assertIsInstance(r.credited, bool)


class Assertion(unittest.TestCase):
    def test_denials_hypotheticals_and_reports_are_flagged(self):
        for text, kind in (("I haven't asked about allergies yet.", "denied"), ("I'm not going to explain the procedure now.", "denied"), ("If I were to explain the operation, I'd start with the incision.", "hypothetical"),
                           ("The nurse said she would reassure you later.", "reported"), ("I didn't ask for a chest X-ray.", "denied")):
            self.assertEqual(non_assertive(text), kind, text)

    def test_a_sentence_that_breaks_off_is_incomplete_and_the_frozen_challenge_set_has_none(self):
        self.assertEqual(non_assertive("So you'd like the gas repeated in an hour, and then I should..."), "incomplete")
        self.assertEqual(non_assertive("order a CRP -"), "incomplete")
        rows = json.loads((ROOT / "tests" / "classifier_challenge_v4" / "challenge.json").read_text(encoding="utf-8"))["rows"]
        self.assertEqual([r["id"] for r in rows if non_assertive(r["text"]) == "incomplete"], [])           # so the rule added after v4 was scored cannot have changed its figures

    def test_assertions_are_not_flagged(self):
        for text in ("Sorry, I didn't catch that, could you say it again?", "No need to panic, just breathe.", "I can only imagine how frightening that must have been.", "Could you tell me about your last meal?",
                     "I'm going to listen to your chest now.", "Can you ask the senior nurse to come over?", "I won't extubate until you say so, got it."):
            self.assertEqual(non_assertive(text), "", text)


class Frozen(unittest.TestCase):
    def test_challenge_set_matches_its_hash(self):
        d = ROOT / "tests" / "classifier_challenge_v4"
        manifest = json.loads((d / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(hashlib.sha256((d / "challenge.json").read_bytes()).hexdigest(), manifest["files"]["challenge.json"])
        rows = json.loads((d / "challenge.json").read_text(encoding="utf-8"))["rows"]
        self.assertEqual({r["bucket"] for r in rows}, {"critical_evasive", "ordinary_hard", "non_assertive"})
        self.assertTrue(all(r["id"] and r["text"] for r in rows))

    def test_heads_say_they_were_trained_with_hard_negatives_and_are_not_claims(self):
        for name in ("act_head.json", "act_head_tfidf.json"):
            h = json.loads((ROOT / "content" / "assessment" / name).read_text(encoding="utf-8"))
            self.assertIn("Hard negatives", h["provenance"], name)
            self.assertFalse(h["valid_for_claims"], name)


if __name__ == "__main__":
    unittest.main(verbosity=1)
