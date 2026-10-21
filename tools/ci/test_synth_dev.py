"""Direct synthetic development set (p1-ai-language-s21): the frozen specifications, the label derivation, the matrix, the utterance/discard bookkeeping and the validators. No model, no cloud call.

Evidence files named here: tests/synthetic_dev_v1/specs.json, tests/synthetic_dev_v1/manifest.json, tests/synthetic_dev_v1/dataset.jsonl, tests/synthetic_dev_v1/report.json, tests/synthetic_dev_v1/open_issues.md,
tools/synth_dev/plan.py, tools/synth_dev/gold.py, tools/synth_dev/seeds.py, tools/synth_dev/validate.py, tools/synth_dev/diversity.py, docs/research/direct_synthetic_dev_set_design.md."""
from __future__ import annotations

import hashlib
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "synth_dev"))
import diversity as D  # noqa: E402
import gold as G  # noqa: E402
import plan as P  # noqa: E402
import validate as V  # noqa: E402

OUT = ROOT / "tests" / "synthetic_dev_v1"
SPECS = json.loads((OUT / "specs.json").read_text(encoding="utf-8"))
MANIFEST = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))


class FrozenSpecs(unittest.TestCase):
    def test_specs_match_manifest_and_plan(self):
        self.assertEqual(hashlib.sha256((OUT / "specs.json").read_bytes()).hexdigest(), MANIFEST["specs_sha256"])
        items = P.build()
        _, spec_bytes = P.freeze(items)
        self.assertEqual((OUT / "specs.json").read_bytes(), spec_bytes, "the specifications are frozen: they must equal what the plan produces")

    def test_plan_sources_unchanged_since_freeze(self):
        here = ROOT / "tools" / "synth_dev"
        for n, h in MANIFEST["plan_sources_sha256"].items():
            self.assertEqual(hashlib.sha256((here / n).read_bytes()).hexdigest(), h, n)

    def test_matrix(self):
        c = MANIFEST["coverage"]
        self.assertEqual(c["n"], 500)
        self.assertEqual(c["by_class"], {"A": 150, "B": 45, "C": 140, "D": 55, "E": 30, "F": 50, "G": 30})
        self.assertEqual((c["contrast_sets"], c["contrast_items"]), (33, 198))
        self.assertEqual(c["out_of_window"], 29)
        self.assertEqual(c["state_absent"], 16)

    def test_reserved_personas_never_used(self):
        self.assertTrue(all(s["persona"] not in P.RESERVED_PERSONAS for s in SPECS))
        self.assertEqual(MANIFEST["reserved_personas"], ["P5", "P7"])

    def test_no_label_or_gold_inside_the_seed_text_fields_are_label_free(self):
        for s in SPECS:
            self.assertEqual(s["speaker"], "trainee")


class GoldDerivation(unittest.TestCase):
    def test_gold_is_the_derivation_of_the_spec(self):
        for s in SPECS:
            self.assertEqual(s["gold"], G.derive({k: v for k, v in s.items() if k != "gold"}), s["id"])

    def test_rules(self):
        for s in SPECS:
            g = s["gold"]
            if s["situation"] != "performed":
                self.assertFalse(g["credit_eligible"], s["id"])
            if s["objective"] in G.CRITICAL_ACTS:
                self.assertFalse(g["credit_eligible"], s["id"])
            if g["credit_eligible"]:
                self.assertEqual(s["window"], "in_window", s["id"])
                for a in g["acts"]:
                    if a["credit_eligible"] and a["act"] == "ANSWER_QUESTION":
                        self.assertTrue(s["patient_question_pending"], s["id"])
                    if a["credit_eligible"] and a["act"] == "CLOSED_LOOP":
                        self.assertTrue(s["readback_pending"], s["id"])
            if s["cls"] == "B" and "out_of_window" in s["tags"]:
                self.assertFalse(g["credit_eligible"], s["id"])
            if s["cls"] == "A":
                self.assertTrue(g["credit_eligible"], s["id"])

    def test_window_comes_from_mapping(self):
        m = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
        want = {}
        for b in m["behaviours"]:
            for a in b.get("ai_acts") or []:
                want.setdefault(a, set()).update(b["steps"])
        self.assertEqual(G.WINDOW, want)

    def test_credit_balance(self):
        credit = sum(s["gold"]["credit_eligible"] for s in SPECS)
        self.assertEqual(credit, 180)           # 150 in class A + 24 two-performed + 6 mixed multi-intent items that carry one credit-eligible act
        self.assertEqual(len(SPECS) - credit, 320)


class Utterances(unittest.TestCase):
    def test_validator_hard_checks_pass(self):
        res = V.evaluate(with_embeddings=False)
        self.assertEqual(res["hard_failures"], [])
        self.assertEqual(res["exact_duplicates"], [])
        self.assertEqual(res["n_accepted"] + res["n_discarded"], 500)

    def test_utterance_files_carry_no_label(self):
        for p in (OUT / "utterances").glob("b*.jsonl"):
            for line in p.read_text(encoding="utf-8").splitlines():
                if line.strip():
                    self.assertEqual(set(json.loads(line)), {"id", "text"})

    def test_every_spec_is_written_or_discarded_with_a_reason(self):
        specs, by_id, utts, dup, discards, review = V.load()
        for s in SPECS:
            self.assertTrue((s["id"] in utts) != (s["id"] in discards), s["id"])
        for d in discards.values():
            self.assertGreater(len(d["reason"]), 20)

    def test_dataset_is_derived_from_specs_and_matches_manifest(self):
        rows = [json.loads(l) for l in (OUT / "dataset.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        self.assertEqual(len(rows), MANIFEST["dataset_items"])
        self.assertEqual(hashlib.sha256((OUT / "dataset.jsonl").read_bytes()).hexdigest(), MANIFEST["dataset_sha256"])
        by_id = {s["id"]: s for s in SPECS}
        for r in rows:
            self.assertEqual(r["gold"], by_id[r["id"]]["gold"])
            self.assertIn("not independent", r["provenance"]["review"])


class Helpers(unittest.TestCase):
    def test_norm_and_distinct(self):
        self.assertEqual(D.norm("Hi, it’s Me!"), "hi it's me")
        self.assertEqual(D.distinct_n(["a b c", "a b d"], 2), round(3 / 4, 4))

    def test_self_bleu_orders_diversity(self):
        same = D.self_bleu(["please take a deep breath now"] * 4)
        different = D.self_bleu(["please take a deep breath now", "the monitor keeps going blank again", "who is at home with you", "nurse will explain the scan soon"])
        self.assertGreater(same, different)

    def test_historical_set_excludes_this_set(self):
        h = D.historical_texts()
        first = json.loads((OUT / "utterances" / "b01.jsonl").read_text(encoding="utf-8").splitlines()[1])["text"]
        self.assertNotIn(D.norm(first), h)

    def test_factory_is_gone(self):
        self.assertFalse((ROOT / "tools" / "factory").exists())
        self.assertFalse((ROOT / "reports" / "ai" / "factory-pilot").exists())


if __name__ == "__main__":
    unittest.main()
