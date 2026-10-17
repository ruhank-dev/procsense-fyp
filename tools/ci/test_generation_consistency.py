"""The deterministic side-of-body and denied-pain rules (server/generation/consistency.py): no authored case or variant is flagged, every seeded side flip and denied pain is, and a silent or missing critic cannot let them through.

The rules are plain code over the patient's lines and the structured case. They exist because the model critic found only 13 of 29 flipped sides and 14 of 36 denied pains (reports/ai/critic-eval.json).
"""
import copy
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for p in ("server", "tools", "tools/generation"):
    sys.path.insert(0, str(ROOT / p))
import inconsistency_set as I  # noqa: E402
from composer import blocks as B  # noqa: E402
from generation import consistency as K  # noqa: E402

CASES = {c["id"]: c for c in B.load_cases()}
SEEDED = json.loads((ROOT / "tests" / "generation" / "text_inconsistencies.json").read_text(encoding="utf-8"))["members"]


def hit(member) -> bool:
    return any(f.line == member["line"] for f in K.check(I.apply(CASES[member["case"]], member["line"], member["text"])))


class Rules(unittest.TestCase):
    def test_no_authored_case_and_no_variant_answer_is_flagged(self):
        self.assertEqual([(c["id"], f) for c in CASES.values() for f in K.check(c)], [])
        for c in CASES.values():
            for h in c["history"]:
                if h.get("variant") is not None:
                    d = copy.deepcopy(c)
                    for x in d["history"]:
                        if x["topic"] == h["topic"] and x.get("variant") is None:
                            x["answer"] = h["answer"]
                    self.assertEqual(K.check(d), [], (c["id"], h["topic"], h["answer"]))

    def test_every_denied_pain_and_nearly_every_flipped_side_is_found(self):
        pain = [m for m in SEEDED if m["kind"] == "pain_denied_elsewhere"]
        self.assertEqual(sum(hit(m) for m in pain), len(pain))
        side = [m for m in SEEDED if m["kind"] == "side_flip"]
        missed = [m["text"] for m in side if not hit(m)]
        self.assertEqual(missed, ["I do not feel left; confused."])            # the one miss is a mutation artefact: swapping "right" in "I do not feel right" is not a claim about a side

    def test_phrasings_of_denied_pain_and_of_the_wrong_side(self):
        for t in ("No pain at all.", "I have no pain.", "I'm not in pain.", "No tummy pain, just tired.", "Pain free.", "It doesn't hurt.", "There is no abdominal pain."):
            self.assertTrue(K.denies_pain(t), t)
        for t in ("No chest pain.", "No pain when I pass urine.", "No pain in my calf.", "No back pain.", "No leg pain or swelling."):
            self.assertFalse(K.denies_pain(t), t)
        self.assertEqual([sorted(K._sides(t)) for t in ("It hurts low on the left.", "I do not feel right.", "The pain is not on the left.", "Pain in my left shoulder tip.", "I have left work.")], [["left"], [], [], [], []])

    def test_a_case_without_pain_may_say_it_has_none(self):
        c = copy.deepcopy(CASES["SC-19"])
        for e in c["examination"]:
            e["painful"] = False
            e["finding"] = "Calm, no tenderness."
        for h in c["history"]:
            if h["topic"] == "complaint" and h.get("variant") is None:
                h["answer"] = "I am confused."
        c["reason_answer"], c["presentation"]["summary"] = "I am confused.", "Confused."
        c["history"] = [h for h in c["history"] if h["topic"] != "red_flags"] + [{"topic": "red_flags", "answer": "No pain at all.", "volunteered": False, "variant": None}]
        self.assertEqual(K.check(c), [])


class Gate(unittest.TestCase):
    def test_the_package_gate_rejects_them_whatever_the_critic_says_and_repair_has_a_strategy(self):
        from unittest import mock

        from generation import objectives as ob, pipeline, repair, retrieve

        class Silent:                                                    # a critic that finds nothing
            def review(self, case):
                return []
        calls = {"n": 0}

        def first_time_only(case):
            calls["n"] += 1
            return [K.Finding("h.red_flags", "PAIN_DENIED", "x")] if calls["n"] == 1 else []
        base = dict(index=retrieve.build_index(), cases=list(CASES.values()), tables=ob.Tables.load())
        req = {"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": 3}
        with mock.patch.object(K, "check", first_time_only):
            r = pipeline.generate(req, [], [], pipeline.Context(**base, critic=Silent()))
        self.assertIsNotNone(r.package)
        self.assertEqual(r.package["provenance"]["repairs"][0]["codes"], ["PAIN_DENIED"])
        for code in ("PAIN_DENIED", "SIDE_VS_EXAM", "SIDE_VS_OPERATION"):
            self.assertIn(code, repair.TABLE)


class HeldOutSet(unittest.TestCase):
    """The second inconsistency set (DT-217): deterministic, committed, and worded so that the rule-based baseline does not saturate it."""

    def test_the_committed_file_is_what_the_builder_gives_and_the_baseline_leaves_room(self):
        import inconsistency_set_v2 as V
        built = V.build(list(CASES.values()))
        committed = json.loads((ROOT / "tests" / "generation" / "text_inconsistencies_v2.json").read_text(encoding="utf-8"))
        self.assertEqual(committed, built)
        self.assertGreaterEqual(len(built["members"]), 120)
        self.assertGreaterEqual(len(built["counts"]), 6)
        found = sum(m["line"] in I.baseline_flags(V.apply(CASES[m["case"]], m["line"], m["text"])) for m in built["members"])
        self.assertLess(found / len(built["members"]), 0.25)                  # the rules were written before this set and none of them looks for this wording

    def test_every_member_changes_only_its_own_line(self):
        import inconsistency_set_v2 as V
        for m in V.build(list(CASES.values()))["members"][:40]:
            before, after = CASES[m["case"]], V.apply(CASES[m["case"]], m["line"], m["text"])
            self.assertNotEqual(json.dumps(before, sort_keys=True), json.dumps(after, sort_keys=True))
            self.assertEqual(before["identity"], after["identity"])
            self.assertEqual(before["allergies"], after["allergies"])


if __name__ == "__main__":
    unittest.main()
