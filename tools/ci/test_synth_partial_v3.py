"""The fresh 32-item partial-credit acceptance set v3 and its single scored run (DT-247 follow-up, ADR-0025), and the final acceptance record the `speech_evaluation` gate reads: the set is whole and new (no copy of any committed test sentence, ten or more long items),
its data, criteria, prompt and reply contract are unchanged since they were frozen, it was scored exactly once, the stored verdict follows from the criteria, no reply was rejected for long evidence, and the acceptance record equals what the two stored reports give. No model, no cloud."""
from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SET = ROOT / "tests" / "synthetic_partial_v3"


def j(p):
    return json.loads(p.read_text(encoding="utf-8"))


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def words(t):
    return set(re.sub(r"[^a-z0-9 ]", " ", t.lower()).split())


class Set(unittest.TestCase):
    def test_the_set_is_whole_and_covers_every_band_and_long_speech(self):
        rows = [json.loads(l) for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        man = j(SET / "partial_credit.manifest.json")
        self.assertEqual((len(rows), man["n"], man["sha256"]), (32, 32, sha(SET / "partial_credit.jsonl")))
        self.assertEqual({r["band"] for r in rows}, {"full", "partial", "wrong", "irrelevant"})
        self.assertGreaterEqual(sum(len(r["utterance"]) > 160 for r in rows), 10)             # long, natural speech: the DT-247 case
        self.assertTrue(all(r["long"] == (len(r["utterance"]) > 160) for r in rows))

    def test_no_sentence_is_a_copy_of_a_committed_test_sentence(self):
        rows = [json.loads(l)["utterance"] for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
        old = []
        for f in list((ROOT / "tests").glob("synthetic_*/partial_credit*.jsonl")) + list((ROOT / "tests").glob("synthetic_*/dataset.jsonl")):
            if SET in f.parents:
                continue
            old += [json.loads(l)["utterance"] for l in f.read_text(encoding="utf-8").splitlines() if l.strip()]
        for f in (ROOT / "tests").glob("synthetic_heldout_v*/utterances.json"):
            old += list(j(f).values())
        for u in rows:
            for o in old:
                a, b = words(u), words(o)
                self.assertFalse(len(a) >= 4 and len(b) >= 4 and len(a & b) / len(a | b) >= 0.7, u)


class ScoredOnce(unittest.TestCase):
    def test_data_criteria_prompt_and_contract_are_what_was_frozen(self):
        cfg = j(SET / "eval_config.json")
        for f, h in cfg["data_sha256"].items():
            self.assertEqual(sha(SET / f), h, f)
        for f in ("server/evaluate/contract.py", "server/evaluate/prompt.py", "src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs"):
            self.assertEqual(sha(ROOT / f), cfg["code_sha256"][f], f + " changed after the configuration was frozen")

    def test_it_was_scored_exactly_once_with_the_frozen_prompt(self):
        lock, cfg, raw = j(SET / "RUN_LOCK.json"), j(SET / "eval_config.json"), j(SET / "run_raw.json")
        self.assertEqual((lock["prompt_hash"], raw["prompt_hash"]), (cfg["prompt_hash"], cfg["prompt_hash"]))
        self.assertEqual((len(raw["results"]), raw["stopped"]), (32, ""))
        self.assertGreater(lock["scored_utc"], cfg["frozen_utc"])

    def test_the_stored_verdict_follows_from_the_criteria_and_no_reply_was_rejected_for_long_evidence(self):
        rep, acc, raw = j(ROOT / "reports" / "ai" / "speech-evaluation-partial-v3.json"), j(SET / "acceptance.json"), j(SET / "run_raw.json")
        ops = {"==": lambda a, b: a == b, ">=": lambda a, b: a >= b, "<=": lambda a, b: a <= b}
        self.assertEqual([c["id"] for c in rep["criteria"]], [c["id"] for c in acc["criteria"]])
        for c, a in zip(rep["criteria"], acc["criteria"]):
            self.assertEqual((c["metric"], c["op"], c["value"]), (a["metric"], a["op"], a["value"]))
            self.assertEqual(c["passed"], ops[c["op"]](c["observed"], c["value"]), c["id"])
        self.assertEqual(rep["verdict"], "APPROVED" if all(c["passed"] for c in rep["criteria"]) else "NOT APPROVED (owner decision)")
        self.assertEqual(sum(1 for r in raw["results"].values() if r["status"] != "VALID" and any("text fields" in c for c in r["codes"])), 0)


class Record(unittest.TestCase):
    def test_the_acceptance_record_the_gate_reads_is_what_the_two_reports_give(self):
        p = subprocess.run([sys.executable, str(ROOT / "tools" / "eval" / "speech_acceptance.py"), "--check"], capture_output=True, text=True, cwd=ROOT)
        self.assertEqual(p.returncode, 0, p.stdout + p.stderr)
        rec = j(ROOT / "reports" / "ai" / "speech-evaluation-acceptance.json")
        self.assertEqual((rec["final_verdict"], rec["gate_inputs"]["critical_physical_completions"], rec["gate_inputs"]["window_state_bypass_items"]), ("APPROVED", 0, 0))


if __name__ == "__main__":
    unittest.main()
