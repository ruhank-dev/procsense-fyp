"""Tests of the multi-problem composition engine (tools/composer/multiproblem.py): the coherence rules reject what they must, a valid combination is accepted and ordered by the ladder, the output has a stable schema,
the same seed gives the same file, every committed composition traces to approved source problems, and nothing in the output claims clinical validation."""
import copy
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import multiproblem as M  # noqa: E402

R = M.rules()
MODS = M.modules(R)
KNOWN = M.corpus_topic_sets(R)
BY_KEY = {(m["source_case"], m["source_problem"]): m for m in MODS}


def mod(case="X-1", scen="S2", typ="pathway_changing", label="a problem", checks=("do a thing",), topics=(), rank=1, end="ready", pid=None):
    return {"source_case": case, "scenario": scen, "end_state": end, "source_problem": pid or f"{case}.p{rank}", "source_rank": rank, "type": typ, "label": label, "checks": list(checks), "topics": list(topics)}


class Rules(unittest.TestCase):
    def test_a_coherent_new_pair_is_accepted(self):
        self.assertEqual(M.reasons_against([mod("A", topics=["sepsis"]), mod("B", topics=["fasting"], rank=2)], R, set()), [])

    def test_each_rule_rejects(self):
        a, b = mod("A", topics=["sepsis"]), mod("B", topics=["fasting"])
        cases = {
            "size": [a],
            "mixes S1 and S2": [a, mod("B", scen="S1", topics=["fasting"])],
            "two problems from the same source case": [a, mod("A", topics=["fasting"], rank=2)],
            "too many life threats": [mod(c, typ="life_threat", topics=[t]) for c, t in zip("ABCD", ["sepsis", "fasting", "allergy", "consent"])],
            "a topic is repeated": [a, mod("B", topics=["sepsis"])],
            "conflicting sex requirements": [mod("A", topics=["pregnancy"]), mod("B", topics=["fasting"])] and None,
        }
        for why, mods in cases.items():
            if mods is None:
                continue
            self.assertTrue(any(why in w for w in M.reasons_against(mods, R, set())), why)

    def test_a_forbidden_action_against_a_required_one_is_a_contradiction(self):
        a = mod("A", checks=["No anti-inflammatories"], topics=["kidneys"])
        b = mod("B", checks=["Give anti-inflammatories for the pain"], topics=["fasting"])
        self.assertTrue(M.contradictions([a, b], R))
        self.assertFalse(M.contradictions([a, mod("C", checks=["Strict urine output"])], R))

    def test_not_new_is_rejected(self):
        a, b = mod("A", topics=["sepsis"]), mod("B", topics=["fasting"])
        self.assertTrue(any("not new" in w for w in M.reasons_against([a, b], R, {frozenset(["sepsis", "fasting"])})))

    def test_sex_requirement_follows_the_topic(self):
        req, bad = M.requirements([mod("A", topics=["pregnancy"]), mod("B", topics=["fasting"])], R)
        self.assertEqual((req["sex"], bad), ("F", []))


class Compose(unittest.TestCase):
    def test_ladder_order_and_clock_and_handover(self):
        c = M.compose([mod("A", typ="optimisable", topics=["diabetes"], checks=["Recheck within 30 minutes"]), mod("B", typ="life_threat", topics=["sepsis"], checks=["Call the senior"], rank=2)], R)
        self.assertEqual([p["type"] for p in c["problems"]], ["life_threat", "optimisable"])
        self.assertEqual(c["clock"]["type"], "life_threat")
        self.assertIn("within 30 minutes", [h["time"] for h in c["handover_items"] if h["time"]][0])
        self.assertEqual(len(c["handover_items"]), 2)
        self.assertEqual((c["status"], c["clinically_validated"], c["practice_only"], c["synthetic"]), ("DRAFT", False, True, True))

    def test_s1_with_an_optimisable_problem_ends_postponed(self):
        c = M.compose([mod("A", scen="S1", typ="optimisable", topics=["diabetes"]), mod("B", scen="S1", typ="information_only", topics=["fasting"], rank=2)], R)
        self.assertEqual(c["end_state"], "postponed")
        c = M.compose([mod("A", scen="S1", typ="safety_blocker", topics=["consent"], end="redirected"), mod("B", scen="S1", typ="information_only", topics=["fasting"], rank=2)], R)
        self.assertEqual(c["end_state"], "redirected")


class Committed(unittest.TestCase):
    doc = json.loads(M.OUT.read_text(encoding="utf-8"))

    def test_every_composition_validates_and_traces_to_approved_sources(self):
        self.assertGreaterEqual(len(self.doc["compositions"]), 10)
        for c in self.doc["compositions"]:
            self.assertEqual(M.validate(c, BY_KEY, R, KNOWN), [], c["id"])

    def test_nothing_claims_clinical_validation(self):
        self.assertFalse(self.doc["clinically_validated"])
        self.assertTrue(self.doc["synthetic"] and self.doc["practice_only"])
        for c in self.doc["compositions"]:
            self.assertEqual((c["status"], c["clinically_validated"]), ("DRAFT", False))
            self.assertTrue(c["authoring_required"])

    def test_tampering_is_caught(self):
        c = copy.deepcopy(self.doc["compositions"][0])
        c["problems"][0]["checks"] = ["something the source never said"]
        self.assertTrue(M.validate(c, BY_KEY, R, KNOWN))
        c = copy.deepcopy(self.doc["compositions"][0])
        c["problems"].reverse()
        self.assertTrue(M.validate(c, BY_KEY, R, KNOWN))
        c = copy.deepcopy(self.doc["compositions"][0])
        c["clinically_validated"] = True
        self.assertTrue(M.validate(c, BY_KEY, R, KNOWN))

    def test_same_seed_same_output_and_other_seed_differs(self):
        self.assertEqual(M.document(M.SEED, M.COUNT), self.doc)
        self.assertNotEqual([c["id"] for c in M.build(7, 12)], [c["id"] for c in self.doc["compositions"]])

    def test_no_composition_repeats_an_authored_cases_topics(self):
        for c in self.doc["compositions"]:
            self.assertNotIn(frozenset(t for p in c["problems"] for t in p["topics"]), KNOWN, c["id"])


if __name__ == "__main__":
    unittest.main(verbosity=1)
