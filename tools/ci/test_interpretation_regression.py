"""Regression suite for the context-aware interpretation path (server/interpret; p1-ai-language-s11, s12; ADR-0022). No model service is called: tests/classifier_regression/cases.json carries each sentence's embedding, so the real
gate (hybrid head, TF-IDF agreement, critical-mass cap, critical cues, assertion check) runs under the interpreter, and a stub classifier plays the hostile or failing cases.
Checked: an eligible, performed, non-critical act is offered with the context it needs (ANSWER_QUESTION needs a waiting question, CLOSED_LOOP a waiting read-back); the same sentence is not offered outside its behaviour window;
no sentence of the must-not-credit groups is ever offered, in any context; a critical topic is never offered and points at the verified actions; a classifier that names a critical or an ineligible act at full confidence is refused;
a behaviour already credited is not offered again; uncertainty gets exactly one clarification built from approved wording, then the menu; a down classifier, a missing ranking and a statement act asked as a question are refused; an unknown act
cannot be offered; every offer carries the words of the utterance as its span."""
import ast
import json
import sys
import unittest
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "server"))
from classify.classifier import Result  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from interpret.decide import CLARIFY_LIMIT, Interpreter  # noqa: E402
from interpret.status import is_question, status_of  # noqa: E402

CASES = json.loads((ROOT / "tests" / "classifier_regression" / "cases.json").read_text(encoding="utf-8"))["cases"]
LOOKUP = {c["text"]: c["embedding"] for c in CASES}
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
CRITICAL = set(MAPPING["critical_acts"])
ALL_STEPS = list(range(1, 16))


def eligible_at(steps, accepted=()):
    out = []
    for b in MAPPING["behaviours"]:
        if set(b["steps"]) & set(steps):
            out += [{"act": a, "behaviour": b["id"], "critical": a in CRITICAL} for a in b["ai_acts"]]
    return out


@dataclass
class Entry:
    act: str
    behaviour: str
    critical: bool = False


@dataclass
class Context:
    """The attributes of server/schemas/interpret.py ContextIn that the interpreter reads (pydantic is not installed in the tools environment)."""
    steps: list
    eligible: list
    accepted: list = field(default_factory=list)
    patient_question_pending: bool = False
    readback_pending: bool = False


def ctx(steps, pending=False, readback=False, accepted=(), eligible=None):
    rows = eligible if eligible is not None else eligible_at(steps)
    return Context(list(steps), [Entry(**r) for r in rows], list(accepted), pending, readback)


def window_of(act):
    return sorted({s for b in MAPPING["behaviours"] if act in b["ai_acts"] for s in b["steps"]})


class Stub:
    """A classifier that returns what it is told (the hostile and failing cases)."""
    def __init__(self, act="ASK_TOPIC", p=0.9, credited=True, refused="", probs=None, source="ensemble", error=""):
        self.result = Result(act, p, credited, source, act in CRITICAL, error, refused)
        self.probs = probs if probs is not None else {act: p}

    class embedding:
        head = {}

        @staticmethod
        def critical_mass(probs):
            return 0.0

    def classify_with_probs(self, sentence):
        return self.result, self.probs


class Real(unittest.TestCase):
    interp = Interpreter(EnsembleActClassifier(lambda t: LOOKUP.get(t)))

    def run_case(self, text, c, used=0):
        return self.interp.interpret(c, text, "u-1", used)

    def test_every_creditable_sentence_is_offered_in_its_window_with_the_context_it_needs(self):
        offered = 0
        for case in CASES:
            if case["group"] != "may_credit":
                continue
            act = self.interp.clf.classify(case["text"]).act
            c = ctx(window_of(act)[:1], pending=act == "ANSWER_QUESTION", readback=act == "CLOSED_LOOP")
            out = self.run_case(case["text"], c)
            self.assertEqual(out["decision"], "OFFER_EVIDENCE", (case["text"], out["reasons"]))
            self.assertEqual(out["evidence"]["act"], act)
            self.assertIn(out["evidence"]["span"], case["text"])
            self.assertEqual(out["evidence"]["utterance_id"], "u-1")
            offered += 1
        self.assertEqual(offered, 14)

    def test_the_same_sentences_are_not_offered_outside_their_behaviour_window(self):
        for case in CASES:
            if case["group"] != "may_credit":
                continue
            act = self.interp.clf.classify(case["text"]).act
            outside = [s for s in ALL_STEPS if s not in window_of(act)]
            if not outside:
                continue
            out = self.run_case(case["text"], ctx(outside[:1], pending=True, readback=True))
            self.assertNotEqual(out["decision"], "OFFER_EVIDENCE", (case["text"], act))
            self.assertIsNone(out["evidence"])

    def test_nothing_in_the_must_not_groups_is_ever_offered_in_any_context(self):
        for case in CASES:
            if not case["group"].startswith("must_not_credit"):
                continue
            for c in (ctx(ALL_STEPS, pending=True, readback=True), ctx([2]), ctx([9], pending=True), ctx([])):
                out = self.run_case(case["text"], c)
                self.assertNotEqual(out["decision"], "OFFER_EVIDENCE", (case["text"], c.steps))
                self.assertIsNone(out["evidence"])

    def test_critical_topics_get_the_menu_pointing_at_verified_actions_and_never_an_offer(self):
        pointed = 0
        for case in CASES:
            if case["group"] != "must_not_credit_critical":
                continue
            out = self.run_case(case["text"], ctx(ALL_STEPS))
            if out["decision"] == "MENU" and out["reasons"] == ["CRITICAL_TOPIC"]:
                pointed += 1
                self.assertTrue(out["menu"])
        self.assertGreater(pointed, 0)

    def test_answer_question_needs_a_waiting_question_and_closed_loop_a_waiting_readback(self):
        for text, act, pending, readback in (("You'll be home before teatime, so your daughter can fetch you.", "ANSWER_QUESTION", True, False), ("Ceftriaxone two grams, drawn up and ready, will call when in.", "CLOSED_LOOP", False, True)):
            steps = window_of(act)[:1]
            self.assertNotEqual(self.run_case(text, ctx(steps))["decision"], "OFFER_EVIDENCE", act)
            self.assertIn("NO_PENDING_QUESTION" if act == "ANSWER_QUESTION" else "NO_READBACK_PENDING", self.run_case(text, ctx(steps))["reasons"])
            self.assertEqual(self.run_case(text, ctx(steps, pending=pending, readback=readback))["decision"], "OFFER_EVIDENCE", act)

    def test_a_behaviour_already_credited_is_not_offered_again(self):
        text = "The X-ray shows whether the bone has moved."
        steps = window_of("EXPLAIN_TEST")[:1]
        first = self.run_case(text, ctx(steps))
        self.assertEqual(first["decision"], "OFFER_EVIDENCE")
        again = self.run_case(text, ctx(steps, accepted=[first["evidence"]["behaviour"]]))
        self.assertNotEqual(again["decision"], "OFFER_EVIDENCE")
        self.assertIn("ALREADY_ACCEPTED", again["reasons"])


class Rules(unittest.TestCase):
    def test_a_hostile_classifier_naming_a_critical_act_at_full_confidence_is_never_offered(self):
        for act in CRITICAL:
            out = Interpreter(Stub(act, 1.0)).interpret(ctx(ALL_STEPS), "Some sentence.", "u", 0)
            self.assertNotEqual(out["decision"], "OFFER_EVIDENCE", act)
            self.assertIsNone(out["evidence"])

    def test_a_hostile_classifier_naming_an_ineligible_act_is_not_offered(self):
        c = ctx([2])
        self.assertNotIn("CHECK_UNDERSTANDING", [e.act for e in c.eligible])
        out = Interpreter(Stub("CHECK_UNDERSTANDING", 1.0)).interpret(c, "Can you tell me in your own words what will happen?", "u", 0)
        self.assertEqual((out["decision"], out["reasons"]), ("ABSTAIN", ["NOT_ELIGIBLE"]))

    def test_an_act_missing_from_the_taxonomy_cannot_be_offered(self):
        out = Interpreter(Stub("MADE_UP_ACT", 1.0)).interpret(ctx(ALL_STEPS), "Whatever.", "u", 0)
        self.assertNotEqual(out["decision"], "OFFER_EVIDENCE")

    def test_the_gate_not_crediting_is_never_overridden_by_the_context(self):
        out = Interpreter(Stub("ASK_TOPIC", 0.99, credited=False, refused="")).interpret(ctx([2]), "Do you sleep well at night?", "u", CLARIFY_LIMIT)
        self.assertNotEqual(out["decision"], "OFFER_EVIDENCE")

    def test_a_non_performed_sentence_is_abstained_from_without_a_question(self):
        for text in ("I haven't asked about your sleep yet.", "I will ask about your sleep in a moment.", "The nurse said she would ask about your sleep.", "If I were to ask about your sleep, I would start with this."):
            out = Interpreter(Stub("ASK_TOPIC", 0.99)).interpret(ctx([2]), text, "u", 0)
            self.assertEqual(out["decision"], "ABSTAIN", text)
            self.assertEqual(out["clarify"], "")

    def test_a_statement_act_asked_as_a_question_is_not_offered_but_a_question_act_is(self):
        self.assertNotEqual(Interpreter(Stub("EXPLAIN_PROCEDURE", 0.99)).interpret(ctx(window_of("EXPLAIN_PROCEDURE")[:1]), "Shall I explain the operation to you?", "u", 1)["decision"], "OFFER_EVIDENCE")
        self.assertEqual(Interpreter(Stub("ASK_TOPIC", 0.99)).interpret(ctx([2]), "Do you sleep well at night?", "u", 0)["decision"], "OFFER_EVIDENCE")

    def test_two_readers_that_disagree_get_one_clarification_then_the_menu(self):
        stub = Stub("EXPLAIN_PROCEDURE", 0.8, credited=False, refused="HEADS_DISAGREE:EXPLAIN_TEST", probs={"EXPLAIN_PROCEDURE": 0.8, "EXPLAIN_TEST": 0.1})
        steps = sorted(set(window_of("EXPLAIN_PROCEDURE")) & set(window_of("EXPLAIN_TEST")))
        self.assertTrue(steps, "both acts must share a step window for this case")
        c = ctx(steps[:1])
        first = Interpreter(stub).interpret(c, "The scan lets us look at the inside of the knee.", "u", 0)
        self.assertEqual(first["decision"], "CLARIFY")
        self.assertEqual(first["options"], ["EXPLAIN_PROCEDURE", "EXPLAIN_TEST"])
        self.assertEqual(first["clarify"], "Did you mean explaining the operation, or explaining a test?")
        self.assertIsNone(first["evidence"])
        second = Interpreter(stub).interpret(c, "The scan lets us look at the inside of the knee.", "u", CLARIFY_LIMIT)
        self.assertEqual(second["decision"], "MENU")
        self.assertIn("CLARIFICATION_LIMIT", second["reasons"])
        self.assertTrue(second["menu"])

    def test_a_waiting_question_and_an_explanation_that_could_be_its_answer_get_one_question(self):
        stub = Stub("EXPLAIN_PROCEDURE", 0.9, probs={"EXPLAIN_PROCEDURE": 0.9, "ANSWER_QUESTION": 0.3})
        steps = sorted(set(window_of("EXPLAIN_PROCEDURE")) & set(window_of("ANSWER_QUESTION")))
        self.assertTrue(steps)
        out = Interpreter(stub).interpret(ctx(steps[:1], pending=True), "The operation takes about an hour.", "u", 0)
        self.assertEqual((out["decision"], out["options"]), ("CLARIFY", ["EXPLAIN_PROCEDURE", "ANSWER_QUESTION"]))
        self.assertEqual(Interpreter(stub).interpret(ctx(steps[:1], pending=False), "The operation takes about an hour.", "u", 0)["decision"], "OFFER_EVIDENCE")
        after = Interpreter(stub).interpret(ctx(steps[:1], pending=True), "The operation takes about an hour.", "u", CLARIFY_LIMIT)
        self.assertNotEqual(after["decision"], "CLARIFY")

    def test_a_down_classifier_a_missing_ranking_and_no_eligible_objective_each_give_the_safe_answer(self):
        down = Stub("NONE", 0.0, credited=False, probs={}, source="unavailable", error="EMBEDDING_UNAVAILABLE")
        out = Interpreter(down).interpret(ctx([2]), "Do you sleep well at night?", "u", 0)
        self.assertEqual((out["decision"], out["reasons"]), ("MENU", ["EMBEDDING_UNAVAILABLE"]))
        self.assertTrue(out["menu"])
        self.assertEqual(Interpreter(object()).interpret(ctx([2]), "x", "u", 0)["reasons"], ["NO_RANKING"])
        self.assertEqual(Interpreter(Stub()).interpret(ctx([]), "Do you sleep well at night?", "u", 0)["decision"], "ABSTAIN")
        self.assertEqual(Interpreter(Stub()).interpret(ctx([2], eligible=[]), "Do you sleep well at night?", "u", 0)["decision"], "ABSTAIN")

    def test_status_of_separates_questions_from_statements_and_keeps_the_assertion_kinds(self):
        self.assertTrue(is_question("Do you sleep well at night?"))
        self.assertTrue(is_question("can you tell me your date of birth"))
        self.assertFalse(is_question("The operation takes about an hour."))
        self.assertFalse(is_question("Can you hear me. Good."))
        self.assertEqual(status_of("I haven't asked about your sleep yet."), "denied")
        self.assertEqual(status_of("The operation takes about an hour."), "actual")
        self.assertEqual(status_of("Do you sleep well at night?"), "question")

    def test_the_context_schema_lists_the_engines_keys(self):
        schema = json.loads((ROOT / "server" / "schemas" / "interpretation_context.json").read_text(encoding="utf-8"))
        tree = ast.parse((ROOT / "server" / "schemas" / "interpret.py").read_text(encoding="utf-8"))
        fields = {n.target.id for c in tree.body if isinstance(c, ast.ClassDef) and c.name == "ContextIn" for n in c.body if isinstance(n, ast.AnnAssign)}
        self.assertEqual(set(schema["required"]), fields)


if __name__ == "__main__":
    unittest.main()
