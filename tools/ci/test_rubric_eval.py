"""The rubric-evaluator experiment (tools/bench/rubric_eval.py, rubric_compare.py, rubric_labelling_kit.py): everything that needs no model is tested here, so the experiment cannot drift without notice. The model is never called.
Objectives come from the approved taxonomy; eligibility keeps out-of-window objectives; the schema bounds the answer; a defective model answer is dropped, never repaired; credit is separate from interpretation, a critical objective is never credited from speech,
and the production guards only remove credit; the annotation kit validates, measures agreement and never invents a label."""
import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import rubric_eval as R  # noqa: E402
import rubric_labelling_kit as K  # noqa: E402

ELIG = R.eligible(None)


def raw(*findings) -> dict:
    return {"content": json.dumps({"findings": list(findings)}), "thinking": ""}


def finding(o="ASK_TOPIC", v="SUPPORTED", actor="trainee", mod="actual", ev="do you sleep well", why="MEETS_RUBRIC") -> dict:
    return {"objective": o, "verdict": v, "actor": actor, "modality": mod, "evidence": ev, "reason": why}


class Objectives(unittest.TestCase):
    def test_objectives_are_the_approved_acts_without_none(self):
        tax = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]
        self.assertEqual([o["id"] for o in R.objectives()], [a["id"] for a in tax if a["id"] != "NONE"])
        self.assertEqual(R.critical_ids(), {"SBAR_ELEMENT", "CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT"})
        p = R.system_prompt("lean")
        for a in R.objectives():
            self.assertIn(a["definition"], p)                                  # the approved definition is used verbatim

    def test_eligibility_keeps_out_of_window_objectives_and_no_state_means_all(self):
        self.assertEqual(len(ELIG), 19)
        e9 = R.eligible(9)
        self.assertEqual({o["id"] for o in e9}, {o["id"] for o in ELIG})              # the experiment keeps them (flagged); the engine's rule rejects them (ADR-0022), applied by p1-ai-language-s10
        win = [o["id"] for o in e9 if o["in_window"]]
        self.assertIn("CHECK_UNDERSTANDING", win)                                      # step 9 holds the teach-back behaviour (mapping.json b07)
        self.assertTrue(all(o["in_window"] is False for o in e9[len(win):]))           # in-window first
        self.assertTrue(all(o["in_window"] is None for o in ELIG))

    def test_schema_is_bounded_and_limits_objectives_to_the_eligible_set(self):
        s = R.schema(["ASK_TOPIC", "CLARIFY"])
        self.assertEqual(s["properties"]["findings"]["maxItems"], R.MAX_FINDINGS)
        self.assertEqual(s["properties"]["findings"]["items"]["properties"]["objective"]["enum"], ["ASK_TOPIC", "CLARIFY"])
        self.assertFalse(s["properties"]["findings"]["items"]["additionalProperties"])


class Interpretation(unittest.TestCase):
    def test_omitted_objectives_are_not_supported_and_valid_findings_are_kept(self):
        ev = R.evaluate("Do you sleep well at night?", raw(finding()), ELIG)
        self.assertTrue(ev["ok"] and not ev["problems"])
        self.assertEqual(ev["results"]["ASK_TOPIC"]["verdict"], "SUPPORTED")
        self.assertEqual(ev["results"]["CLARIFY"]["verdict"], "NOT_SUPPORTED")
        self.assertEqual(len(ev["results"]), 19)

    def test_defects_are_recorded_and_never_repaired(self):
        u = "Do you sleep well at night?"
        self.assertEqual(R.evaluate(u, {"content": "not json"}, ELIG)["problems"], ["NOT_JSON_OR_SCHEMA"])
        self.assertIn("TIMEOUT", R.evaluate(u, {"error": "TIMEOUT"}, ELIG)["problems"])
        e = R.evaluate(u, raw(finding(ev="a sentence that was never said")), ELIG)
        self.assertIn("EVIDENCE_NOT_IN_UTTERANCE", e["problems"])
        self.assertFalse(e["results"]["ASK_TOPIC"]["evidence_in_utterance"])
        e = R.evaluate(u, raw(finding(mod="negated")), ELIG)                                 # SUPPORTED but not actual: inconsistent, flagged
        self.assertIn("SUPPORTED_BUT_NOT_ACTUAL_TRAINEE", e["problems"])
        e = R.evaluate(u, raw(finding(), finding()), ELIG)
        self.assertIn("DUPLICATE_OBJECTIVE", e["problems"])
        e = R.evaluate(u, raw(finding(o="MADE_UP")), ELIG)
        self.assertIn("FINDING_ENUM", e["problems"])
        self.assertIn("THINKING_PRESENT", R.evaluate(u, {"content": json.dumps({"findings": []}), "thinking": "hmm"}, ELIG)["problems"])


class Credit(unittest.TestCase):
    def test_credit_needs_every_condition(self):
        u = "Do you sleep well at night?"
        ok = R.evaluate(u, raw(finding()), ELIG)
        self.assertEqual(R.credit(u, ok, "ASK_TOPIC", True), (True, ""))
        for bad, why in ((finding(v="NOT_SUPPORTED"), "NOT_SUPPORTED_OR_UNCERTAIN"), (finding(v="UNCERTAIN"), "NOT_SUPPORTED_OR_UNCERTAIN"), (finding(actor="other"), "NOT_ACTUAL_TRAINEE"),
                         (finding(mod="intended"), "NOT_ACTUAL_TRAINEE"), (finding(ev="never said"), "EVIDENCE_NOT_IN_UTTERANCE")):
            self.assertEqual(R.credit(u, R.evaluate(u, raw(bad), ELIG), "ASK_TOPIC", True), (False, why))

    def test_a_critical_objective_is_never_credited_from_speech(self):
        u = "Can you tell me your full name and date of birth?"
        ev = R.evaluate(u, raw(finding(o="CONFIRM_IDENTITY", ev="tell me your full name and date of birth")), ELIG)
        self.assertEqual(R.credit(u, ev, "CONFIRM_IDENTITY", False), (False, "CRITICAL_NEVER_FROM_SPEECH"))
        self.assertEqual(R.credit(u, ev, "CONFIRM_IDENTITY", True), (False, "CRITICAL_NEVER_FROM_SPEECH"))

    def test_production_guards_only_remove_credit(self):
        u = "Please note she is allergic to penicillin."                                  # a model that calls this an ordinary act is stopped by the production cue
        ev = R.evaluate(u, raw(finding(o="EXPLAIN_ACTION", ev="allergic to penicillin")), ELIG)
        self.assertEqual(R.credit(u, ev, "EXPLAIN_ACTION", False), (True, ""))
        self.assertEqual(R.credit(u, ev, "EXPLAIN_ACTION", True), (False, "CRITICAL_CUE"))
        u = "I haven't asked about your sleep yet."
        ev = R.evaluate(u, raw(finding(ev="asked about your sleep")), ELIG)
        self.assertEqual(R.credit(u, ev, "ASK_TOPIC", False), (True, ""))                  # a model error the guard catches
        self.assertEqual(R.credit(u, ev, "ASK_TOPIC", True)[1].split(":")[0], "NON_ASSERTIVE")


class Kit(unittest.TestCase):
    def write(self, d: Path, name: str, rows: list[dict]) -> str:
        p = d / name
        with open(p, "w", encoding="utf-8", newline="") as fh:
            w = csv.DictWriter(fh, fieldnames=K.COLUMNS)
            w.writeheader()
            w.writerows(rows)
        return str(p)

    def row(self, i, utt, acts, actor="trainee", mod="actual", ev="", cred="yes", amb="no") -> dict:
        return {"item_id": i, "scenario_step": "2", "scenario_context": "x", "prior_turns": "", "speaker_role": "trainee", "utterance": utt, "acts_performed": acts, "actor": actor, "modality": mod, "evidence": ev, "credit_eligible": cred, "ambiguous": amb, "note": ""}

    def test_check_agree_merge(self):
        with tempfile.TemporaryDirectory() as t:
            d = Path(t)
            a = self.write(d, "a.csv", [self.row("1", "zzq alpha beta gamma", "ASK_TOPIC", ev="alpha beta"), self.row("2", "zzq delta epsilon", "NONE", "other", "reported", cred="no"), self.row("3", "zzq theta iota", "CLARIFY", ev="theta")])
            b = self.write(d, "b.csv", [self.row("1", "zzq alpha beta gamma", "ASK_TOPIC", ev="alpha beta"), self.row("2", "zzq delta epsilon", "NONE", "other", "reported", cred="no"), self.row("3", "zzq theta iota", "ASK_TOPIC", ev="theta", amb="yes")])
            self.assertEqual(K.check(a), [])
            ag = K.agree(a, b)
            self.assertEqual(ag["items_in_both"], 3)
            self.assertEqual([x["item_id"] for x in ag["differ"]], ["3"])
            adj = self.write(d, "j.csv", [self.row("3", "zzq theta iota", "CLARIFY", ev="theta")])
            m = K.merge(a, b, adj, str(d / "final.csv"))
            self.assertEqual(m["scored_items"], 2)
            self.assertEqual(m["ambiguous_excluded"], ["3"])                                  # an item either annotator flagged is never scored
            self.assertEqual(m["awaiting_adjudication"], [])

    def test_check_finds_defects_and_never_corrects(self):
        with tempfile.TemporaryDirectory() as t:
            f = self.write(Path(t), "a.csv", [self.row("1", "zzq one two", "NOT_AN_ACT"), self.row("2", "zzq one two three", "ASK_TOPIC", ev="not in it"), self.row("3", "zzq four five", "ASK_TOPIC", mod="intended", ev="four", cred="yes"), self.row("3", "zzq dup", "NONE", cred="no")])
            bad = " | ".join(K.check(f))
            for needle in ("unknown act", "evidence is not a part", "adjudicate", "duplicate item id"):
                self.assertIn(needle, bad)
            self.assertEqual(K.rule_credit(self.row("9", "x", "CONFIRM_IDENTITY", ev="x")), "no")          # critical act: never eligible
            self.assertEqual(K.rule_credit(self.row("9", "x", "ASK_TOPIC", ev="x")), "yes")

    def test_template_has_no_rows(self):
        with tempfile.TemporaryDirectory() as t:
            K.main(["template", "--out", t])
            self.assertEqual((Path(t) / "annotation_template.csv").read_text(encoding="utf-8").strip(), ",".join(K.COLUMNS))


if __name__ == "__main__":
    unittest.main()
