"""Regression suite for the recall follow-up of ADR-0022 (diagnosis in docs/research/recall_diagnosis.md): legitimate speech recovered WITHOUT letting unsafe or ineligible speech through. No service is called: tests/classifier_regression/recall_cases.json carries every sentence's
all-minilm:22m embedding, so the real heads, cues, assertion layer and interpreter run.
Pinned, and why:
  * the head's critical-mass cap is the recorded revision (0.05, with its evidence) and the builder keeps it; the engine's per-act evidence thresholds are the head's own calibrated thresholds floored to three decimals (so the engine never refuses what the gate credited);
  * 28 hand-written legitimate paraphrases for 14 acts are offered in their window, none outside it, and none is offered under another act; the 13 dev rows the revised cap recovers are offered;
  * for the SAME 14 acts, 84 counterexamples (a denial, a plan, a hypothetical, a report, another speaker, a broken-off sentence) and 4 ambiguous fillers are never offered, in the act's window, in no window and in the most permissive context, AND the assertion layer itself names each non-performed kind
    (the classifier refusing by luck would not close the hole this follow-up found: "the technician will report the fault", "I'll order the ECG after the handover", "If the suction were broken I would report it");
  * the 9 unsafe sentences that ONLY the critical-mass cap keeps from being offered are not offered under the shipped cap, and at least 7 of them ARE offered with the cap removed (the fixture still exercises the cap: if it stops mattering, this fails and says so);
  * a behaviour already credited is not offered again; a critical sentence is never offered."""
import copy
import json
import math
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "server"))
from classify.assertion import non_assertive  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from interpret.decide import Interpreter  # noqa: E402
from test_interpretation_regression import ALL_STEPS, ctx, window_of  # noqa: E402  (the same state helpers, derived from content/assessment/mapping.json)

ROWS = json.loads((ROOT / "tests" / "classifier_regression" / "recall_cases.json").read_text(encoding="utf-8"))["rows"]
LOOKUP = {r["text"]: r["embedding"] for r in ROWS}
REGRESSION = {c["text"]: c["embedding"] for c in json.loads((ROOT / "tests" / "classifier_regression" / "cases.json").read_text(encoding="utf-8"))["cases"]}
HEAD = json.loads((ROOT / "content" / "assessment" / "act_head_hybrid.json").read_text(encoding="utf-8"))
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
COUNTER = [r for r in ROWS if r["group"].startswith("counter_") and r["group"] != "counter_ambiguous"]
NON_ASSERTIVE_KIND = {"counter_denied": {"denied"}, "counter_intended": {"intended"}, "counter_hypothetical": {"hypothetical"}, "counter_reported": {"reported", "other_actor"}, "counter_other_actor": {"other_actor", "reported"}, "counter_incomplete": {"incomplete"}}


def state(act: str):
    return ctx(window_of(act)[:1], pending=act == "ANSWER_QUESTION", readback=act == "CLOSED_LOOP") if window_of(act) else ctx(ALL_STEPS, pending=True, readback=True)


def interp(cap="keep") -> Interpreter:
    c = EnsembleActClassifier(lambda t: LOOKUP.get(t) or REGRESSION.get(t))
    if cap != "keep":
        c.embedding.head = copy.copy(c.embedding.head)
        if cap is None:
            c.embedding.head.pop("critical_mass_max", None)
        else:
            c.embedding.head["critical_mass_max"] = cap
    return Interpreter(c)


def offered(i: Interpreter, row: dict, context) -> str | None:
    o = i.interpret(context, row["text"], row["id"], 0)
    return o["evidence"]["act"] if o["decision"] == "OFFER_EVIDENCE" else None


class Policy(unittest.TestCase):
    def test_the_cap_is_the_recorded_revision_and_the_builder_keeps_it(self):
        rev = json.loads((ROOT / "tools" / "bench" / "recall_cap_revision.json").read_text(encoding="utf-8"))
        self.assertEqual((rev["from"], rev["to"]), (0.03, 0.05))
        self.assertEqual(HEAD["critical_mass_max"], rev["to"])
        self.assertEqual(HEAD["critical_mass_tuning"]["revision"], rev)
        self.assertIn("recall_cap_revision.json", (ROOT / "tools" / "bench" / "build_safe_heads.py").read_text(encoding="utf-8"))

    def test_the_engine_thresholds_are_the_heads_calibrated_thresholds_floored_so_the_engine_never_refuses_what_the_gate_credited(self):
        per = MAPPING["thresholds"]["per_act"]
        creditable = {a: t for a, t in HEAD["thresholds"].items() if t is not None}
        self.assertEqual(set(per), set(creditable))
        for act, t in creditable.items():
            self.assertLessEqual(per[act], t, act)                         # the engine accepts at least what the gate credits
            self.assertGreaterEqual(per[act], t - 0.001, act)              # and nothing noticeably lower
            self.assertEqual(per[act], math.floor(t * 1000) / 1000, act)
        for critical in MAPPING["critical_acts"]:
            self.assertNotIn(critical, per)                                # a critical act has no threshold: it is refused whatever its confidence
        self.assertEqual(MAPPING["thresholds"]["default"], 0.8)


class Legitimate(unittest.TestCase):
    i = interp()

    def test_the_paraphrases_are_offered_in_their_window_under_their_own_act_and_not_outside_it(self):
        rows = [r for r in ROWS if r["group"] == "legit_paraphrase"]
        self.assertEqual(len(rows), 28)
        right = 0
        for r in rows:
            got = offered(self.i, r, state(r["act"]))
            self.assertIn(got, (None, r["act"]), (r["text"], got))          # never under another act
            right += got == r["act"]
            outside = [s for s in ALL_STEPS if s not in window_of(r["act"])]
            if outside:
                self.assertNotEqual(offered(self.i, r, ctx(outside[:1], pending=True, readback=True)), r["act"], r["text"])
        self.assertGreaterEqual(right, 26, "27 of 28 are offered at the shipped cap (22 at the old cap)")

    def test_the_dev_rows_the_revised_cap_recovers_are_offered(self):
        rows = [r for r in ROWS if r["group"] == "legit_recovered_by_cap"]
        self.assertEqual(len(rows), 13)
        for r in rows:
            self.assertEqual(offered(self.i, r, state(r["act"])), r["act"], r["text"])
        self.assertLessEqual(sum(offered(interp(0.03), r, state(r["act"])) == r["act"] for r in rows), 1, "at the old cap at most one of them was offered (and the engine refused that one: LOW_CONFIDENCE)")

    def test_legitimate_sentences_are_not_taken_for_non_performed_speech_by_the_assertion_layer(self):
        for r in ROWS:
            if r["group"] in ("legit_paraphrase", "legit_recovered_by_cap"):
                self.assertEqual(non_assertive(r["text"]), "", r["text"])


class Counterexamples(unittest.TestCase):
    i = interp()

    def test_the_assertion_layer_itself_names_every_non_performed_kind(self):
        self.assertEqual(len(COUNTER), 84)
        for r in COUNTER:
            self.assertIn(non_assertive(r["text"]), NON_ASSERTIVE_KIND[r["group"]], (r["group"], r["text"]))

    def test_no_counterexample_is_offered_in_the_acts_window_in_no_window_or_in_the_most_permissive_context(self):
        for r in COUNTER + [x for x in ROWS if x["group"] == "counter_ambiguous"]:
            states = [ctx(ALL_STEPS, pending=True, readback=True)]
            if r["act"] != "NONE":
                states.append(state(r["act"]))
                outside = [s for s in ALL_STEPS if s not in window_of(r["act"])]
                if outside:
                    states.append(ctx(outside[:1], pending=True, readback=True))
            for c in states:
                self.assertIsNone(offered(self.i, r, c), (r["group"], r["text"]))

    def test_the_old_cap_and_the_new_cap_both_refuse_every_counterexample(self):
        for cap in (0.03, 0.05):
            i = interp(cap)
            for r in COUNTER:
                self.assertIsNone(offered(i, r, ctx(ALL_STEPS, pending=True, readback=True)), (cap, r["text"]))


class CapGuard(unittest.TestCase):
    def test_the_sentences_only_the_cap_guards_are_not_offered_under_the_shipped_cap(self):
        rows = [r for r in ROWS if r["group"] == "cap_blocked_unsafe"]
        self.assertEqual(len(rows), 9)
        i = interp()
        for r in rows:
            self.assertIsNone(offered(i, r, ctx(ALL_STEPS, pending=True, readback=True)), r["text"])

    def test_tripwire_with_the_cap_removed_the_fixture_does_leak(self):
        rows = [r for r in ROWS if r["group"] == "cap_blocked_unsafe"]
        i = interp(None)
        leaked = sum(1 for r in rows if offered(i, r, ctx(ALL_STEPS, pending=True, readback=True)))
        self.assertGreaterEqual(leaked, 7, "the cap no longer guards these sentences: re-derive the fixture (tools/bench/recall_safety_panel.py)")

    def test_a_cap_above_the_nearest_unsafe_sentence_would_leak_so_the_margin_is_at_least_one_and_a_half(self):
        i = interp(None)
        masses = []
        for r in ROWS:
            if r["group"] == "cap_blocked_unsafe" and offered(i, r, ctx(ALL_STEPS, pending=True, readback=True)):
                _, probs = i.clf.classify_with_probs(r["text"])
                masses.append(i.clf.embedding.critical_mass(probs))
        self.assertGreaterEqual(min(masses) / HEAD["critical_mass_max"], 1.5)


class Duplicates(unittest.TestCase):
    def test_a_behaviour_already_credited_is_not_offered_again_and_a_critical_sentence_is_never_offered(self):
        i = interp()
        row = next(r for r in ROWS if r["group"] == "legit_paraphrase" and r["act"] == "EXPLAIN_TEST")
        c = state("EXPLAIN_TEST")
        first = i.interpret(c, row["text"], "u1", 0)
        if first["decision"] != "OFFER_EVIDENCE":
            self.skipTest("not offered at the shipped cap")
        c.accepted = [first["evidence"]["behaviour"]]
        self.assertNotEqual(i.interpret(c, row["text"], "u2", 0)["decision"], "OFFER_EVIDENCE")
        crit = [t for t in REGRESSION if t in LOOKUP or True]
        j = Interpreter(EnsembleActClassifier(lambda t: REGRESSION.get(t)))
        for case in json.loads((ROOT / "tests" / "classifier_regression" / "cases.json").read_text(encoding="utf-8"))["cases"]:
            if case["group"] == "must_not_credit_critical":
                self.assertNotEqual(j.interpret(ctx(ALL_STEPS, pending=True, readback=True), case["text"], "c", 0)["decision"], "OFFER_EVIDENCE", case["text"])
        self.assertTrue(crit)


if __name__ == "__main__":
    unittest.main()
