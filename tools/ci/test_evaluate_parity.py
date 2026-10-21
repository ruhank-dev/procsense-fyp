"""The Python mirror of the communication score (tools/synth_dev/evaluator_metrics.score_act) and the engine's own (src/ProcSense.Core/Scoring, the CLI command `speech-score`) agree on every combination of objective, assertion, actor, grade triple, step window and waiting state (p1-ai-language-s22, ADR-0025).
The engine is authoritative; the mirror exists only so the evaluation reports can be recomputed without the CLI. Needs the CLI built (the full CI builds it first). No model, no cloud."""
from __future__ import annotations

import itertools
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "synth_dev"))
import engine_score as ES  # noqa: E402
import evaluator_metrics as M  # noqa: E402

ACTS = sorted(M.WINDOW) + [a for a in M.CRIT if a not in M.WINDOW]
ASSERT = [("performed", "trainee"), ("negated", "trainee"), ("reported", "other"), ("performed", "other")]
GRADES = [(3, 3, 3), (3, 3, 1), (2, 3, 2), (1, 1, 0), (0, 3, 3), (3, 0, 3), (3, 2, 0)]


class Parity(unittest.TestCase):
    def test_the_mirror_equals_the_engine_on_the_whole_grid(self):
        items, expect = [], {}
        n = 0
        for act, (asr, actor), (r, c, k), step, pq in itertools.product(sorted(set(ACTS)), ASSERT, GRADES, (2, 6, 9, 12), (False, True)):
            n += 1
            i = f"p{n}"
            spec = {"step": step, "patient_question_pending": pq, "readback_pending": pq}
            a = {"objective": act, "assertion": asr, "actor": actor, "form": "statement", "relevance": r, "correctness": c, "completeness": k, "performed_now": asr == "performed" and actor == "trainee", "evidence": "x", "rationale": ""}
            items.append({"id": i, "steps": [step], "patient_question_pending": pq, "readback_pending": pq, "verified": [], "acts": [{"act": act, "assertion": asr, "actor": actor, "form": "statement", "relevance": r, "correctness": c, "completeness": k, "span": "x"}]})
            expect[i] = M.score_act(a, spec)
        got = {x["id"]: x["communication_total"] for x in ES.score(items)}
        self.assertGreater(len(got), 1000)
        bad = [(i, got[i], expect[i]) for i in expect if abs(got[i] - expect[i]) > 1e-3]
        self.assertEqual(bad[:5], [], f"{len(bad)} of {len(expect)} differ")

    def test_a_claimed_physical_action_never_adds_a_physical_part(self):
        out = ES.score([{"id": "a", "steps": [1, 2, 8, 10, 13, 14], "acts": [{"act": "CONFIRM_IDENTITY", "assertion": "performed", "actor": "trainee", "form": "statement", "relevance": 3, "correctness": 3, "completeness": 3, "span": "x", "claims_physical": True}]}])[0]
        c = out["communication"][0]
        self.assertTrue(c["critical"] and c["claims_physical"] and not c["physical_verified"])
        self.assertLessEqual(out["communication_total"], 0.25 + 1e-9)


if __name__ == "__main__":
    unittest.main()
