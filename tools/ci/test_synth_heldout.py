"""The fresh held-out set HO1 and its single scored run (p1-ai-language-s22, ADR-0025): the specifications are what the plan produces and match their manifest (frozen before any sentence), the dataset is what the specifications and sentences give, no sentence is a copy of
any committed test sentence, the data, the criteria and the prompt recorded before the run are unchanged, the set was scored exactly once, and the stored verdict follows from the pre-registered criteria and the stored numbers. No model, no cloud."""
from __future__ import annotations

import hashlib
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SET = ROOT / "tests" / "synthetic_heldout_v1"
sys.path.insert(0, str(ROOT / "tools" / "synth_heldout"))
import build as B  # noqa: E402
import plan as HP  # noqa: E402


def j(p):
    return json.loads(p.read_text(encoding="utf-8"))


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


class Frozen(unittest.TestCase):
    def test_specs_equal_the_plan_and_the_manifest_hash(self):
        items = HP.build()
        manifest, spec_bytes = HP.freeze(items)
        self.assertEqual(spec_bytes, (SET / "specs.json").read_bytes())
        self.assertEqual(hashlib.sha256(spec_bytes).hexdigest(), j(SET / "manifest.json")["specs_sha256"])
        self.assertEqual((len(items), manifest["reserved_personas_used"]), (259, ["P5", "P7"]))

    def test_no_sentence_is_a_copy_of_any_committed_test_sentence_and_the_dataset_is_current(self):
        items = HP.build()
        utt = j(SET / "utterances.json")
        probs, stats = B.problems(items, utt)
        self.assertEqual(probs, [])
        self.assertEqual(stats["exact_copies"] + stats["near_copies"], 0)
        man = j(SET / "manifest.json")
        self.assertIn(man["dataset_sha256"], {hashlib.sha256((SET / "dataset.jsonl").read_bytes()).hexdigest(), sha(SET / "dataset.jsonl")})          # a Windows checkout may hold CRLF
        # The manifest's utterances hash was taken when the set was built; the file was then rewritten with the same sentences in another
        # layout before the pre-run lock (eval_config.json, frozen before the single scored run) recorded it. Accept either frozen hash, and
        # only if every sentence still equals the frozen dataset (dataset.jsonl matches the manifest above), so no sentence can change.
        frozen = {man["utterances_sha256"], j(SET / "eval_config.json")["data_sha256"]["utterances.json"]}
        self.assertTrue(frozen & {hashlib.sha256((SET / "utterances.json").read_bytes()).hexdigest(), sha(SET / "utterances.json")}, "utterances.json changed after the freeze")
        rows = {r["id"]: r["utterance"] for r in map(json.loads, (SET / "dataset.jsonl").read_text(encoding="utf-8").splitlines())}
        self.assertEqual(utt, rows, "a sentence differs from the frozen dataset")

    def test_the_partial_credit_addendum_is_whole_and_new(self):
        rows = [json.loads(l) for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines()]
        self.assertEqual((len(rows), j(SET / "partial_credit.manifest.json")["sha256"]), (32, sha(SET / "partial_credit.jsonl")))
        self.assertEqual({r["band"] for r in rows}, {"full", "partial", "wrong", "irrelevant"})
        dev = {json.loads(l)["utterance"] for f in ("partial_credit_v1.jsonl", "partial_credit_v2.jsonl", "dataset.jsonl") for l in (ROOT / "tests" / "synthetic_dev_v1" / f).read_text(encoding="utf-8").splitlines()}
        self.assertFalse({r["utterance"] for r in rows} & dev)


class ScoredOnce(unittest.TestCase):
    def test_the_data_and_the_criteria_are_what_was_frozen_before_the_run(self):
        cfg = j(SET / "eval_config.json")
        for f, h in cfg["data_sha256"].items():
            self.assertEqual(sha(SET / f), h, f + " changed after the configuration was frozen")

    def test_it_was_scored_exactly_once_with_the_frozen_prompt(self):
        lock, cfg, raw = j(SET / "RUN_LOCK.json"), j(SET / "eval_config.json"), j(SET / "run_raw.json")
        self.assertEqual((lock["prompt_hash"], raw["prompt_hash"]), (cfg["prompt_hash"], cfg["prompt_hash"]))
        self.assertEqual(len(raw["results"]), 259)
        self.assertEqual(raw["stopped"], "")                                           # no quota or ceiling stop; free plan only
        self.assertGreater(lock["scored_utc"], cfg["frozen_utc"])

    def test_the_stored_verdict_follows_from_the_pre_registered_criteria(self):
        rep, acc = j(ROOT / "reports" / "ai" / "speech-evaluation-heldout.json"), j(SET / "acceptance.json")
        self.assertEqual([c["id"] for c in rep["criteria"]], [c["id"] for c in acc["criteria"]])
        ops = {"==": lambda a, b: a == b, ">=": lambda a, b: a >= b, "<=": lambda a, b: a <= b}
        for c, a in zip(rep["criteria"], acc["criteria"]):
            self.assertEqual((c["metric"], c["op"], c["value"], c["blocking"]), (a["metric"], a["op"], a["value"], a["blocking"]))      # the report cannot change a criterion
            self.assertEqual(c["passed"], ops[c["op"]](c["observed"], c["value"]), c["id"])
        blocked = [c for c in rep["criteria"] if c["blocking"] and not c["passed"]]
        missed = [c for c in rep["criteria"] if not c["blocking"] and not c["passed"]]
        self.assertEqual(rep["verdict"], "BLOCKED" if blocked else ("NOT APPROVED (owner decision)" if missed else "APPROVED"))
        self.assertTrue(rep["free_plan"]["paid_api"] is False and "Not independent" in rep["kind"])


if __name__ == "__main__":
    unittest.main()
