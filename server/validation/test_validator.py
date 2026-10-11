"""Tests of the grounding validator (plan tests T-GV1 to T-GV6; run by tools/ci/run_all.py)."""
from __future__ import annotations

import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE.parent))

from validation import make_vectors  # noqa: E402
from validation.validator import content_words, has_term, norm, numbers, validate  # noqa: E402

LEX = json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))
VECTORS = json.loads((ROOT / "tests" / "vectors" / "grounding_vectors.json").read_text(encoding="utf-8"))
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def utterance(text: str, facts=None) -> dict:
    return {"kind": "utterance", "role": "patient", "intent": "answer_question", "text": text, "fact_ids": facts or []}


def entities_in(text: str) -> list[str]:
    n = norm(text)
    return [t for cat in ("drug", "test", "condition", "allergen", "equipment") for t in LEX["categories"][cat] if has_term(n, t)]


def case_files() -> list[dict]:
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "case_files").glob("*.json"))]


class Vectors(unittest.TestCase):
    def test_the_vector_file_is_what_the_generator_writes(self):
        self.assertEqual(VECTORS, json.loads(make_vectors.build()))

    def test_T_GV5_every_vector_gets_exactly_the_expected_codes(self):
        wrong = []
        for v in VECTORS["vectors"]:
            got = list(validate(v["output"], VECTORS["contexts"][v["context"]], LEX).codes)
            if got != v["expect"]:
                wrong.append((v["name"], got, v["expect"]))
        self.assertEqual(wrong, [])
        self.assertGreaterEqual(len(VECTORS["vectors"]), 40)


class Probes(unittest.TestCase):
    def test_T_GV1_a_hidden_fact_told_verbatim_is_never_accepted_and_the_same_line_is_accepted_once_it_is_earned(self):
        probes = leaks = 0
        for c in case_files():
            for h in c["history"]:
                if h["volunteered"] or h.get("variant") is not None:
                    continue
                value = h["answer"]
                if not (numbers(value) or len(content_words(value)) >= 2):
                    continue                                  # "None." and "No." carry nothing to leak
                probes += 1
                ents = entities_in(value)
                ctx = {"allowed_intents": ["answer_question"], "facts": [{"id": "f", "value": value, "disclosed": False, "disclosable_now": False}], "allowed_entities": ents, "numbers": numbers(value)}
                text = value if len(value) <= 400 else value[:400]
                r = validate(utterance(text, ["f"]), ctx, LEX)
                if "DISCLOSURE_LEAK" not in r.codes:
                    leaks += 1
                ctx["facts"][0]["disclosable_now"] = True
                self.assertNotIn("DISCLOSURE_LEAK", validate(utterance(text, ["f"]), ctx, LEX).codes)
        self.assertGreaterEqual(probes, 100)
        self.assertEqual(leaks, 0, f"{leaks} of {probes} verbatim hidden facts were accepted")

    def test_T_GV2_no_drug_the_case_does_not_mention_is_ever_accepted(self):
        ctx = {"allowed_intents": ["answer_question"], "facts": [], "allowed_entities": [], "numbers": []}
        accepted = [d for d in LEX["categories"]["drug"] if "OFF_CARD_ENTITY" not in validate(utterance(f"I take {d} every day."), ctx, LEX).codes]
        self.assertEqual(accepted, [])
        for cat in ("test", "condition", "allergen", "equipment"):
            accepted = [t for t in LEX["categories"][cat] if "OFF_CARD_ENTITY" not in validate(utterance(f"They mentioned {t} to me."), ctx, LEX).codes]
            self.assertEqual(accepted, [], cat)

    def test_T_GV3_few_correct_lines_are_wrongly_rejected(self):
        """The case's own answers, said when they are earned and with the case's own entities allowed, are not rejected (at most 2 in 100)."""
        total = rejected = 0
        bad = []
        for c in case_files():
            for h in c["history"]:
                if h.get("variant") is not None:
                    continue
                total += 1
                ctx = {"allowed_intents": ["answer_question"], "facts": [{"id": "f", "value": h["answer"], "disclosed": False, "disclosable_now": True}], "allowed_entities": entities_in(h["answer"]), "numbers": numbers(h["answer"])}
                text = h["answer"][:400]
                r = validate(utterance(text, ["f"]), ctx, LEX)
                if not r.ok:
                    rejected += 1
                    bad.append((c["id"], h["topic"], r.codes))
        self.assertGreater(total, 400)
        self.assertLessEqual(rejected / total, 0.02, f"{rejected} of {total} rejected: {bad[:5]}")

    def test_T_GV6_odd_input_is_a_schema_failure_not_a_crash(self):
        for odd in (None, 1, 1.5, True, [], [1, 2], "x", {}, {"kind": None}, {"kind": "utterance"}, {"kind": "debrief", "sections": "no"}, {"kind": "debrief", "sections": [None]}):
            self.assertEqual(validate(odd, VECTORS["contexts"]["patient"], LEX).codes, ("SCHEMA",))


class DebriefMutations(unittest.TestCase):
    """A faithful debrief built from a real session's facts is accepted; every mutation of it is rejected (the grounding half of T-DB1 to T-DB3)."""

    @classmethod
    def setUpClass(cls):
        cls.sessions = []
        for case, script in (("VAR-S1-A", "skeleton1_correct"), ("VAR-S1-H", "VAR-S1-H_correct"), ("VAR-S2-B", "VAR-S2-B_correct")):
            with tempfile.TemporaryDirectory() as tmp:
                out = str(Path(tmp) / "run.json")
                p = subprocess.run(CLI + ["run", "--case", case, "--seed", "42", "--actions", str(ROOT / "tests" / "golden" / f"{script}.actions.json"), "--out", out, "--allow-draft"], cwd=ROOT, capture_output=True, text=True)
                assert p.returncode == 0, p.stdout + p.stderr
                p = subprocess.run(CLI + ["report", "--log", str(Path(tmp) / "run.session.jsonl"), "--out", str(Path(tmp) / "r.html"), "--json", str(Path(tmp) / "r.json"), "--skip-integrity"], cwd=ROOT, capture_output=True, text=True)
                assert p.returncode == 0, p.stdout + p.stderr
                cls.sessions.append(json.loads((Path(tmp) / "r.json").read_text(encoding="utf-8")))

    @staticmethod
    def context_and_output(facts: dict) -> tuple[dict, dict]:
        items = facts["items"]
        dfacts = [{"id": f"d{i}", "verdict": "credit" if it["status"] in ("credit", "info") else ("critical" if it["status"] == "critical" else "penalty"), "numbers": numbers(it["sentence"])} for i, it in enumerate(items)]
        ents = sorted({e for it in items for e in entities_in(it["sentence"])})
        logged = [e for e in facts["events"]]
        ctx = {"debrief_facts": dfacts, "allowed_entities": ents, "numbers": [], "log_events": logged}
        sections = [{"title": f"Item {i}", "text": it["sentence"], "fact_ids": [f"d{i}"]} for i, it in enumerate(items)]
        return ctx, {"kind": "debrief", "sections": sections}

    def test_the_template_debrief_of_a_real_session_is_accepted(self):
        for facts in self.sessions:
            ctx, out = self.context_and_output(facts)
            self.assertEqual(validate(out, ctx, LEX).codes, (), facts["case"])

    def test_every_mutation_is_rejected(self):
        rejected = tried = 0
        for facts in self.sessions:
            ctx, out = self.context_and_output(facts)
            n = len(out["sections"])
            for i in range(n):
                dropped = {"kind": "debrief", "sections": [s for k, s in enumerate(out["sections"]) if k != i]}
                tried += 1
                if dropped["sections"] and "DEBRIEF_MISSING_FACT" in validate(dropped, ctx, LEX).codes:
                    rejected += 1
                swapped = json.loads(json.dumps(out))
                swapped["sections"][i]["fact_ids"] = ["nope"]
                tried += 1
                rejected += "UNKNOWN_FACT" in validate(swapped, ctx, LEX).codes
                bent = json.loads(json.dumps(out))
                bent["sections"][i]["text"] += " Then 99 patients were harmed."
                tried += 1
                rejected += "UNGROUNDED_NUMBER" in validate(bent, ctx, LEX).codes
                event = json.loads(json.dumps(out))
                event["sections"][i]["text"] += " You also handled the allergic reaction."
                tried += 1
                rejected += "DEBRIEF_NEW_EVENT" in validate(event, ctx, LEX).codes
        self.assertGreater(tried, 60)
        self.assertEqual(rejected, tried, f"{tried - rejected} of {tried} mutations were accepted")


class Helpers(unittest.TestCase):
    def test_norm_keeps_the_point_inside_a_number(self):
        self.assertEqual(norm("Potassium 6.4, sugar 13.4!"), "potassium 6.4 sugar 13.4")
        self.assertEqual(numbers("98% and 6.40 and 3.0 and room 5."), ["98", "6.40", "3", "5"])

    def test_a_term_matches_a_whole_word_or_its_plural_only(self):
        self.assertTrue(has_term(norm("two apixabans a day"), "apixaban"))
        self.assertFalse(has_term(norm("insulinoma"), "insulin"))
        self.assertTrue(has_term(norm("Co-amoxiclav tablets"), "co-amoxiclav"))


if __name__ == "__main__":
    unittest.main()
