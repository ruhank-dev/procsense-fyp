"""Regression suite of the unsafe-speech hardening (p1-ai-language-s16, ADR-0023). No service is called.
  * NonPerformed: every must-not-credit sentence that the UNCHANGED 4a2ed00 implementation credited through the engine (190 on the development and challenge sets: tests/safety_challenge/regression_cases.json) is now named as not performed by the assertion layer itself,
    with its kind, so a refusal by luck of the classifier cannot hide a hole; the kinds each pattern group is for are pinned with minimal pairs, including the legitimate neighbour that must keep its credit;
  * the legitimate neighbours: plain and natural performed rows of the development and challenge sets are flagged only where the known false-rejection causes say (3 narrations 'I'll examine your tummy in a moment');
  * ReasonCodes: every reason the interpretation path can give becomes an audit-safe code (A-Z, 0-9, _; at most 40 characters) and none holds a word of the utterance;
  * SafeInterpret: a failing classifier is the menu with no evidence and a cancellation is not swallowed;
  * Content: speech_evidence is an action of no behaviour, only the five critical acts are critical, the engine's evidence store lists the guards by name, the shipped gate is OFF;
  * Sets: the committed sets are what the generator makes, no seed family or template crosses a split, every category has a taxonomy entry and every taxonomy entry a safeguard or a stated residual risk, labels are marked provisional, the final split holds no regression case and no result file for it is committed unless it is locked."""
import json
import re
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.assertion import contentless, non_assertive  # noqa: E402
from interpret.decide import Interpreter, reason_codes, safe_interpret  # noqa: E402
from interpret.status import status_of  # noqa: E402

SETS = ROOT / "tests" / "safety_challenge"
CODE = re.compile(r"^[A-Z][A-Z0-9_]{0,39}$")                      # server/middleware/audit.py


def rows(split):
    return [json.loads(x) for x in (SETS / f"{split}.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]


# minimal pairs: (not performed, kind(s) allowed, the legitimate neighbour that keeps its credit)
PAIRS = [
    ("Should I ask about your appetite?", {"meta_question"}, "Could I ask about your appetite?"),
    ("Do you want me to explain the operation?", {"meta_question"}, "Do you want to know about the operation?"),
    ("Is it true that the operation takes an hour?", {"meta_question"}, "The operation takes about an hour."),
    ("Maybe I noted where you lived.", {"hedged"}, "I think you told me earlier you live alone."),
    ("I'm pretty sure someone asked about your bowels.", {"hedged", "other_actor"}, "I'm sure you will do very well."),
    ("The nurse asked what you meant by strange.", {"other_actor"}, "Can you explain back to me what the doctor told you?"),
    ("The doctor before me explained the urine test.", {"other_actor"}, "I want the on-call anaesthetist bleeped."),
    ("The tutor said, \"Does that make sense?\"", {"quoted"}, "Does that make sense?"),
    ("I asked about your appetite, no wait, I didn't.", {"retracted"}, "Sorry, I didn't catch that, could you say it again?"),
    ("Wait, that was the wrong patient.", {"retracted"}, "Please wait a moment."),
    ("I wanted to ask about it, but then I got distracted.", {"denied"}, "Can I ask you about it, please?"),
    ("I forgot to tell you that your family can wait.", {"denied"}, "I will tell you that your family can wait."),
    ("I might ask you to repeat the eating instructions later if you want me to.", {"intended"}, "Can you repeat the eating instructions?"),
    ("Someone ought to note that you stopped smoking.", {"intended"}, "Most people do very well after this."),
    ("Let's ask about your appetite tomorrow.", {"intended"}, "We'll look after you."),
    ("Next I plan to explain the urine test.", {"intended"}, "I plan to be with you the whole time."),
    ("I would tell you that your family can wait if there were time.", {"hypothetical"}, "Here is what will happen in the operation."),
    ("I heard someone say, I know the waiting has been difficult for you.", {"reported"}, "I know the waiting has been difficult for you."),
    ("I won't take your pulse now until later, if at all.", {"denied"}, "Sorry, I didn't take that in, can you repeat?"),
    ("Thank you.", {"contentless"}, "Thank you, so you live on the second floor."),
]


class NonPerformed(unittest.TestCase):
    def test_every_sentence_the_unchanged_implementation_credited_is_now_named_as_not_performed(self):
        cases = json.loads((SETS / "regression_cases.json").read_text(encoding="utf-8"))["rows"]
        self.assertGreaterEqual(len(cases), 190)
        bad = [(c["text"], status_of(c["text"])) for c in cases if status_of(c["text"]) in ("actual", "question")]
        self.assertEqual(bad, [])

    def test_minimal_pairs(self):
        for bad, kinds, ok in PAIRS:
            self.assertIn(non_assertive(bad), kinds, bad)
            self.assertEqual(non_assertive(ok), "", f"the legitimate neighbour lost its credit: {ok}")

    def test_contentless_is_a_closed_list_of_discourse_particles(self):
        for s in ("Yes.", "Okay, okay.", "Er, um, yes.", "Mm hm.", "Thank you.", "I see."):
            self.assertTrue(contentless(s.lower()), s)
        for s in ("Any pain?", "Any questions?", "How's the pain?", "Thank you for waiting."):
            self.assertFalse(contentless(s.lower()), s)

    def test_a_question_that_is_the_act_keeps_its_credit(self):
        for s in ("How have you been sleeping lately?", "Could I ask about your sleep?", "Can you say that again?", "Is it the arm with the drip you are talking about?", "Did you say it hurts when you breathe in?"):
            self.assertIn(status_of(s), ("question", "actual"), s)

    def test_legitimate_development_and_challenge_rows_are_flagged_only_for_the_known_cause(self):
        for split, allowed in (("dev", {"I'll examine your tummy in a moment, tell me if it hurts."}), ("challenge", set())):
            flagged = {r["text"] for r in rows(split) if r["expected"] == "credit_allowed" and r["category"] in ("plain", "natural") and non_assertive(r["text"])}
            extra = {t for t in flagged if not any(a.lower().rstrip(".") in t.lower() for a in allowed)}
            self.assertEqual(extra, set(), split)

    def test_the_offer_to_repeat_keeps_its_credit(self):
        for s in ("Shall I go over that once more?", "Should I explain that again?"):
            self.assertEqual(non_assertive(s), "", s)


class ReasonCodes(unittest.TestCase):
    def test_every_reason_becomes_an_audit_safe_code(self):
        reasons = ["NOT_PERFORMED:denied", "HEADS_DISAGREE:ASK_TOPIC", "NON_ASSERTIVE:meta_question", "CRITICAL_MASS", "NO_ELIGIBLE_OBJECTIVE", "NOT_ELIGIBLE", "GATE_NOT_CREDITED", "CRITICAL_TOPIC", "AMBIGUOUS", "CLARIFICATION_LIMIT", "QUESTION_NOT_PERFORMING_ACT", "9bad", "", "a b c"]
        codes = reason_codes(reasons)
        self.assertTrue(all(CODE.match(c) for c in codes), codes)
        self.assertIn("NOT_PERFORMED_DENIED", codes)
        self.assertEqual(reason_codes([], "OFFER_EVIDENCE"), ["OFFER_EVIDENCE"])

    def test_no_word_of_an_utterance_reaches_a_code(self):
        class Gate:
            model = "g"

            def classify_with_probs(self, s):
                from classify.classifier import Result
                return Result("ASK_TOPIC", 0.9, True, "ensemble", False, "", ""), {"ASK_TOPIC": 0.9}

            class embedding:
                head = {"critical_mass_max": 0.05}

                @staticmethod
                def critical_mass(p):
                    return 0.0

        from dataclasses import dataclass, field

        @dataclass
        class E:
            act: str
            behaviour: str
            critical: bool = False

        @dataclass
        class C:
            steps: list = field(default_factory=lambda: [2])
            eligible: list = field(default_factory=lambda: [E("ASK_TOPIC", "B")])
            accepted: list = field(default_factory=list)
            patient_question_pending: bool = False
            readback_pending: bool = False
        secret = "Mr Adeyemi in bed four has diabetes"
        out = Interpreter(Gate()).interpret(C(), "I did not ask " + secret, "u", 0)
        codes = reason_codes(out["reasons"], out["decision"])
        self.assertTrue(all(CODE.match(c) for c in codes), codes)
        self.assertFalse(any(w.upper() in " ".join(codes) for w in secret.split() if len(w) > 3))


class SafeInterpret(unittest.TestCase):
    def test_a_failing_classifier_is_the_menu_and_a_cancellation_is_not_swallowed(self):
        class Boom:
            def classify_with_probs(self, s):
                raise RuntimeError("down")

        class Cancel:
            def classify_with_probs(self, s):
                raise KeyboardInterrupt()

        class C:
            steps, eligible, accepted, patient_question_pending, readback_pending = [2], [], [], False, False
        out, failed = safe_interpret(Interpreter(Boom()), C(), "x", "u")
        self.assertEqual((out["decision"], out["evidence"], failed), ("MENU", None, True))
        with self.assertRaises(KeyboardInterrupt):
            safe_interpret(Interpreter(Cancel()), C(), "x", "u")


class Content(unittest.TestCase):
    def test_speech_evidence_is_the_action_of_no_behaviour_and_only_five_acts_are_critical(self):
        m = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
        self.assertEqual(sorted(m["critical_acts"]), ["CONFIRM_IDENTITY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT", "SBAR_ELEMENT", "STATE_ALLERGY"])
        for b in m["behaviours"]:
            self.assertNotIn("speech_evidence", b["actions"], b["id"])
            self.assertFalse(set(b["ai_acts"]) & {"CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT"}, b["id"])

    def test_the_engine_store_still_names_its_guards(self):
        src = (ROOT / "src" / "ProcSense.Core" / "Scoring" / "Evidence.cs").read_text(encoding="utf-8")
        for name in ("UnknownAct", "CriticalNeedsAction", "LowConfidence", "NoRunningStep", "ContextMismatch", "DuplicateBehaviour", "DuplicateUtterance"):
            self.assertIn(name, src)

    def test_the_shipped_gate_is_off(self):
        feats = json.loads((ROOT / "content" / "ai" / "features.json").read_text(encoding="utf-8"))
        flat = json.dumps(feats)
        self.assertIn("speech_interpretation", flat)
        node = feats.get("features", feats)
        entry = node["speech_interpretation"] if "speech_interpretation" in node else None
        if isinstance(entry, dict):
            self.assertFalse(entry.get("enabled", entry.get("default", False)))
        else:
            self.assertFalse(entry)


class Sets(unittest.TestCase):
    def test_the_committed_sets_are_what_the_generator_makes(self):
        import contextlib
        import io

        import metamorph
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(metamorph.main(["--check"]), 0)

    def test_nothing_crosses_a_split_and_every_row_is_provisional_and_traceable(self):
        m = json.loads((SETS / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual((m["leakage"]["seed_families_crossing_splits"], m["leakage"]["templates_crossing_splits"]), ([], []))
        self.assertTrue(m["synthetic"] and not m["independent"])
        for sp in ("dev", "challenge", "final"):
            for r in rows(sp):
                self.assertIn(r["expected"], ("credit_allowed", "no_credit", "ambiguous_review"))
                self.assertTrue(r["seed_family"] and r["template"] and r["provenance"], r["id"])
                self.assertIn(r["review_status"], ("AUTO_PROVISIONAL", "PENDING_HUMAN_REVIEW"))
                if r["expected"] == "ambiguous_review" or r["critical"]:
                    self.assertEqual(r["review_status"], "PENDING_HUMAN_REVIEW", r["id"])

    def test_every_category_has_a_taxonomy_entry_with_a_safeguard_or_a_stated_residual(self):
        tax = {c["id"]: c for c in json.loads((SETS / "taxonomy.json").read_text(encoding="utf-8"))["categories"]}
        seen = {r["category"] for sp in ("dev", "challenge", "final") for r in rows(sp)}
        self.assertEqual(seen - set(tax), set())
        for c in tax.values():
            self.assertTrue(c["residual"] and c["tests"] and c["expected"] in ("credit_allowed", "no_credit", "ambiguous_review"), c["id"])
            if c["expected"] == "no_credit":
                self.assertTrue(c["safeguards"], c["id"])
        for cid, c in tax.items():
            if c["generated"]:
                self.assertIn(cid, seen, cid)

    def test_the_final_split_is_not_a_regression_source_and_has_no_unlocked_result(self):
        reg = json.loads((SETS / "regression_cases.json").read_text(encoding="utf-8"))["rows"]
        self.assertEqual({r["split"] for r in reg} - {"dev", "challenge"}, set())
        final_ids = {r["id"] for r in rows("final")}
        self.assertFalse({r["id"] for r in reg} & final_ids)


if __name__ == "__main__":
    unittest.main()
