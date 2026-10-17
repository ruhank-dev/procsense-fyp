"""The classifier benchmark's data preparation and safety rules (tools/bench; docs/research/classifier_benchmark): the scenario cards match the real taxonomy, a dataset cannot be frozen without independent
authors, a second label on every critical act and an adjudicator, the benchmark refuses data that is not frozen, refuses to score the test split twice, and the taxonomy and `critical_acts` must agree. No model is called."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import bench_classifier as B  # noqa: E402
import dataset as D  # noqa: E402
import synthetic as SY  # noqa: E402


def filled(split="dev", authors=("ann", "bob")):
    """Synthetic placeholder rows used ONLY to exercise the validators; these never become a dataset."""
    rows = D.build_cards(split)
    for i, r in enumerate(rows):
        r.update({c: "" for c in D.FILL_COLUMNS})
        r.update({"text": f"placeholder sentence {split}{i}a {split}{i}b {split}{i}c", "author": authors[i % 2]})
        if r["critical"] == "true":
            r.update({"label_2": r["act"], "labeller": "carol"})
    return rows


class Cards(unittest.TestCase):
    def test_counts_match_the_taxonomy(self):
        tax = D.taxonomy()
        ids, crit = [a["id"] for a in tax], {a["id"] for a in tax if a["critical"]}
        self.assertEqual((len(ids), len(crit)), (20, 5))
        for split, total in (("dev", 100), ("test", 200)):
            per_crit, per_other, per_none, special, _ = D.SPLITS[split]
            cards = D.build_cards(split)
            self.assertEqual(len(cards), total)
            by_act = {a: sum(c["act"] == a for c in cards) for a in ids}
            self.assertEqual(set(by_act), set(ids))
            for a, n in by_act.items():
                self.assertEqual(n, per_none if a == "NONE" else per_crit if a in crit else per_other, a)
            for bucket in D.SPECIAL:
                self.assertEqual(sum(c["bucket"] == bucket for c in cards), special, (split, bucket))
            for a in ids:                                                  # every act keeps rows in ordinary wording
                self.assertGreaterEqual(sum(c["act"] == a and c["bucket"] in ("plain", "paraphrase") for c in cards), 1, a)
            self.assertEqual(len({c["card_id"] for c in cards}), total)

    def test_ambiguous_cards_name_a_real_different_act_and_never_include_wording(self):
        ids = {a["id"] for a in D.taxonomy()}
        for split in D.SPLITS:
            for c in D.build_cards(split):
                if c["bucket"] == "ambiguous":
                    self.assertIn(c["confusable"], ids)
                    self.assertNotEqual(c["confusable"], c["act"])
                else:
                    self.assertEqual(c["confusable"], "")
                self.assertNotIn("text", c)

    def test_cards_are_deterministic_and_the_committed_sheets_are_current(self):
        self.assertEqual(D.build_cards("test"), D.build_cards("test"))
        for split in D.SPLITS:
            have = [{c: r[c] for c in D.CARD_COLUMNS} for r in D.read_sheet(D.SHEETS / f"cards_{split}.csv")]
            self.assertEqual(have, D.build_cards(split), f"cards_{split}.csv is out of date: run tools/bench/dataset.py cards")

    def test_dev_and_test_use_different_author_slots_and_both_slots_in_each(self):
        slots = {s: {c["author_slot"] for c in D.build_cards(s)} for s in D.SPLITS}
        self.assertEqual(slots["dev"] & slots["test"], set())
        self.assertTrue(all(len(v) == 2 for v in slots.values()))


class Freezing(unittest.TestCase):
    def setUp(self):
        tax = D.taxonomy()
        self.acts, self.crit = {a["id"] for a in tax}, {a["id"] for a in tax if a["critical"]}

    def check(self, dev=None, test=None):
        return D.problems({"dev": dev or filled("dev", ("ann", "bob")), "test": test or filled("test", ("dan", "eve"))}, self.acts, self.crit)

    def test_a_complete_independent_set_has_no_problems(self):
        self.assertEqual(self.check(), [])

    def test_blank_sheets_cannot_be_frozen(self):
        blank = {s: D.build_cards(s) for s in D.SPLITS}
        for rows in blank.values():
            for r in rows:
                r.update({c: "" for c in D.FILL_COLUMNS})
        self.assertTrue(D.problems(blank, self.acts, self.crit))

    def test_one_author_for_both_splits_is_refused(self):
        bad = self.check(test=filled("test", ("ann", "eve")))
        self.assertTrue(any("same author wrote for dev and test" in p for p in bad))

    def test_a_split_needs_two_authors(self):
        self.assertTrue(any("at least two authors" in p for p in self.check(dev=filled("dev", ("ann", "ann")))))

    def test_a_critical_act_without_a_second_label_is_refused(self):
        dev = filled("dev")
        row = next(r for r in dev if r["critical"] == "true")
        row["label_2"] = ""
        self.assertTrue(any("without a second label" in p for p in self.check(dev=dev)))

    def test_a_disagreement_needs_a_third_person_to_adjudicate(self):
        dev = filled("dev")
        row = next(r for r in dev if r["critical"] == "true")
        row["label_2"] = "NONE" if row["act"] != "NONE" else "CLARIFY"
        self.assertTrue(any("nobody adjudicated" in p for p in self.check(dev=dev)))
        row["adjudicated_label"], row["adjudicator"] = row["act"], row["author"]
        self.assertTrue(any("third person" in p for p in self.check(dev=dev)))
        row["adjudicator"] = "frank"
        self.assertEqual(self.check(dev=dev), [])
        self.assertEqual(D.final_label(row), row["act"])

    def test_the_second_labeller_cannot_be_the_author(self):
        dev = filled("dev")
        row = next(r for r in dev if r["critical"] == "true")
        row["labeller"] = row["author"]
        self.assertTrue(any("must not be the author" in p for p in self.check(dev=dev)))

    def test_repeated_wording_is_refused(self):
        dev = filled("dev")
        dev[1]["text"] = dev[0]["text"]
        self.assertTrue(any("same wording" in p for p in self.check(dev=dev)))

    def test_near_duplicates_and_kappa(self):
        self.assertEqual(D.near_duplicates(["Are you allergic to anything?", "Are you allergic to anything"]), [(0, 1, 1.0)])
        self.assertEqual(D.near_duplicates(["a b c d", "w x y z"]), [])
        self.assertEqual(D.kappa(["a", "b", "a", "b"], ["a", "b", "a", "b"]), 1.0)
        self.assertEqual(D.kappa(["a", "b", "a", "b"], ["a", "b", "b", "a"]), 0.0)


class Benchmark(unittest.TestCase):
    def test_taxonomy_and_critical_acts_must_agree(self):
        tax = D.taxonomy()
        self.assertEqual(B.critical_acts(tax), {a["id"] for a in tax if a["critical"]})
        self.assertIn("SBAR_ELEMENT", B.critical_acts(tax))
        broken = [dict(a, critical=False) if a["id"] == "SBAR_ELEMENT" else a for a in tax]
        with self.assertRaises(SystemExit):
            B.critical_acts(broken)

    def test_independent_mode_is_blocked_until_a_frozen_manifest_exists(self):
        with tempfile.TemporaryDirectory() as t:
            old = B.INDEP
            B.INDEP = Path(t)
            try:
                with self.assertRaises(SystemExit) as e:
                    B.load_data("independent", False)
                self.assertIn("BLOCKED", str(e.exception))
            finally:
                B.INDEP = old

    def test_independent_mode_refuses_a_file_that_does_not_match_its_hash_and_a_second_test_run(self):
        import hashlib
        with tempfile.TemporaryDirectory() as t:
            d, out = Path(t) / "indep", Path(t) / "out"
            d.mkdir()
            out.mkdir()
            row = {"id": "D001", "text": "x", "act": "NONE", "independent": True, "author": "ann", "bucket": "plain"}
            (d / "dev.jsonl").write_text(json.dumps(row) + "\n", encoding="utf-8")
            (d / "test.jsonl").write_text(json.dumps(row) + "\n", encoding="utf-8")
            files = {n: hashlib.sha256((d / n).read_bytes()).hexdigest() for n in ("dev.jsonl", "test.jsonl")}
            (d / "manifest.json").write_text(json.dumps({"files": files}), encoding="utf-8")
            old = (B.INDEP, B.OUT)
            B.INDEP, B.OUT = d, out
            try:
                data = B.load_data("independent", False)
                self.assertTrue(data["valid_for_claims"])
                (out / "classifier-bench-test-used.json").write_text(json.dumps({"manifest_sha": hashlib.sha256((d / "manifest.json").read_bytes()).hexdigest()}), encoding="utf-8")
                with self.assertRaises(SystemExit) as e:
                    B.load_data("independent", False)
                self.assertIn("scored once", str(e.exception))
                B.load_data("independent", True)                                     # a deliberate override works
                (d / "test.jsonl").write_text(json.dumps(row | {"text": "changed"}) + "\n", encoding="utf-8")
                with self.assertRaises(SystemExit) as e:
                    B.load_data("independent", True)
                self.assertIn("frozen hash", str(e.exception))
            finally:
                B.INDEP, B.OUT = old

    def test_smoke_data_is_never_valid_for_claims(self):
        data = B.load_data("smoke", False)
        self.assertFalse(data["valid_for_claims"])
        self.assertIn("NOT an independent benchmark", data["provenance"])
        self.assertTrue(B.verdict({"valid_for_claims": False}).startswith("NOT A VALID EVALUATION"))

    def test_metrics_credit_and_intervals(self):
        labels = ["A", "B", "C"]
        truth, pred = ["A", "A", "B", "B", "C", "C"], ["A", "B", "B", "B", "C", "A"]
        self.assertAlmostEqual(B.macro_f1(truth, pred, labels), (0.5 + 0.8 + 2 / 3) / 3, places=6)
        stats = B.credited_stats(truth, pred, [True, False, True, True, False, True])
        self.assertEqual((stats["credited"], stats["false_credits"], stats["credited_precision"]), (4, 1, 0.75))
        groups = ["g1", "g2", "g3", "g4", "g5", "g6"]
        a = B.bootstrap(truth, pred, [True] * 6, groups, labels, seed=1, n=50)
        self.assertEqual(a, B.bootstrap(truth, pred, [True] * 6, groups, labels, seed=1, n=50))
        self.assertEqual(B.interval([0.1] * 10), [None, None])                       # too few resamples to give an interval

    def test_verdict_declares_no_winner_when_the_candidates_are_not_separated(self):
        base = {"critical_acts_credited_from_speech": 0, "credited_precision_ci95": [0.85, 1.0]}
        result = {"valid_for_claims": True, "candidates": {"a": base | {"macro_f1": 0.8}, "b": base | {"macro_f1": 0.79}, "c": base | {"macro_f1": 0.5}},
                  "paired_macro_f1_differences": {"a minus b": {"ci95": [-0.05, 0.07]}}}
        self.assertTrue(B.verdict(result).startswith("no clear winner"))
        result["paired_macro_f1_differences"]["a minus b"]["ci95"] = [0.01, 0.07]
        self.assertTrue(B.verdict(result).startswith("a is ahead of b"))
        result["candidates"]["a"]["critical_acts_credited_from_speech"] = 1          # a critical act credited from speech can never win
        self.assertTrue(B.verdict(result).startswith("no clear winner"))


class SyntheticSet(unittest.TestCase):
    """The model-written, model-labelled set (tests/classifier_synthetic): what it is, that it says so, and that it is leakage-checked. Nothing here makes it independent."""

    @staticmethod
    def rows(name):
        return [json.loads(l) for l in (SY.OUT / f"{name}.jsonl").read_text(encoding="utf-8").splitlines()]

    def test_the_committed_files_are_what_the_sources_give(self):
        files = SY.build()
        for n, t in files.items():
            self.assertEqual((SY.OUT / n).read_text(encoding="utf-8"), t, n)

    def test_counts_profiles_and_labels(self):
        train, dev, test = (self.rows(n) for n in ("train", "dev", "test"))
        self.assertEqual((len(train), len(dev), len(test)), (320, 100, 200))
        ids = {a["id"] for a in D.taxonomy()}
        for rs in (train, dev, test):
            self.assertEqual({r["act"] for r in rs}, ids)
        authors = [{r["author"] for r in rs} for rs in (train, dev, test)]
        self.assertEqual(authors[0] & authors[1] | authors[0] & authors[2] | authors[1] & authors[2], set())      # a style is in one split only
        self.assertEqual(sorted(authors[0] | authors[1] | authors[2]), sorted(SY.PERSONAS))

    def test_it_says_it_is_synthetic_and_not_independent_everywhere(self):
        for n in ("train", "dev", "test"):
            self.assertTrue(all(r["synthetic"] and r["independent"] is False for r in self.rows(n)), n)
        m = json.loads((SY.OUT / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual((m["synthetic"], m["independent"]), (True, False))
        self.assertIn("no person wrote", m["note"])

    def test_every_critical_row_was_second_labelled_and_every_disagreement_adjudicated(self):
        crit = {a["id"] for a in D.taxonomy() if a["critical"]}
        for n in ("dev", "test"):
            rs = self.rows(n)
            self.assertTrue(all(r["second_label"] for r in rs if r["intended_act"] in crit), n)
            self.assertTrue(all(r["adjudicated"] for r in rs if r["second_label"] and r["second_label"] != r["intended_act"]), n)

    def test_no_dev_or_test_sentence_repeats_training_wording(self):
        old = [json.loads(l)["text"] for n in ("train", "dev", "test") for l in (ROOT / "tests" / "classifier" / f"{n}.jsonl").read_text(encoding="utf-8").splitlines()]
        train = old + [r["text"] for r in self.rows("train")]
        for n in ("dev", "test"):
            self.assertEqual(D.near_duplicates([r["text"] for r in self.rows(n)], train), [], n)
        self.assertEqual(D.near_duplicates([r["text"] for r in self.rows("dev")], [r["text"] for r in self.rows("test")]), [])

    def test_the_benchmark_refuses_a_changed_file_and_marks_the_data_invalid_for_claims(self):
        data = B.load_data("synthetic", True)
        self.assertFalse(data["valid_for_claims"])
        self.assertIn("NOT INDEPENDENT", data["provenance"])
        with tempfile.TemporaryDirectory() as t:
            copy = Path(t)
            for f in SY.OUT.glob("*.jsonl"):
                (copy / f.name).write_bytes(f.read_bytes())
            (copy / "manifest.json").write_bytes((SY.OUT / "manifest.json").read_bytes())
            (copy / "test.jsonl").write_text(self.rows_text(copy) + " ", encoding="utf-8")
            old = B.SYNTH
            B.SYNTH = copy
            try:
                with self.assertRaises(SystemExit) as e:
                    B.load_data("synthetic", True)
                self.assertIn("frozen hash", str(e.exception))
            finally:
                B.SYNTH = old

    @staticmethod
    def rows_text(copy):
        return (copy / "test.jsonl").read_text(encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
