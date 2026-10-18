"""The fresh hold-out (tests/classifier_synthetic_v3) and the adversarial sets: what they are, that they say so, and that nothing in them repeats an earlier set. Nothing here makes them independent.

The adversarial check is exercised with a stand-in embedder: what is verified is the counting (a critical sentence credited under an ordinary act is counted, a critical act is not), not the real head.
"""
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "server"))
import adversarial_critical as A  # noqa: E402
import synthetic3 as S3  # noqa: E402
from classify import classifier as LLM  # noqa: E402
from classify import embedding as E  # noqa: E402

S2 = S3.S


class Synthetic3(unittest.TestCase):
    def test_the_committed_files_are_what_the_sources_and_judgements_give(self):
        for n, t in S2.build().items():
            self.assertEqual((S2.OUT / n).read_text(encoding="utf-8"), t, n)

    def test_structure_and_no_repetition_of_earlier_sets(self):
        data = S2.rows()
        self.assertEqual(S2.structure_problems(data), [])
        self.assertEqual({k: len(v) for k, v in data.items()}, {"test": 300})
        lk = S2.leakage(data, False)
        self.assertEqual([k for k, v in lk["jaccard_pairs"].items() if v], [])
        self.assertTrue(S3.S.GROUPS["test"].isdisjoint({"G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8"}))        # new scenario groups, new topics

    def test_it_says_it_is_synthetic_ai_judged_and_not_independent(self):
        rows = [json.loads(line) for line in (S2.OUT / "test.jsonl").read_text(encoding="utf-8").splitlines()]
        self.assertTrue(all(r["synthetic"] and r["independent"] is False and r["judge_1"] and r["judge_2"] for r in rows))
        m = json.loads((S2.OUT / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual((m["synthetic"], m["independent"], m["valid_for_claims"]), (True, False, False))
        self.assertIn("not an independent check", m["note"])
        contested = [r for r in rows if r["judge_1"] == r["judge_2"] != r["intended_act"]]
        self.assertTrue(all(r["judge_status"] == "adjudicated" for r in contested))


class Adversarial(unittest.TestCase):
    def test_the_sets_exist_and_are_marked(self):
        for path in A.SETS.values():
            doc = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual((doc["synthetic"], doc["independent"]), (True, False))
            self.assertGreaterEqual(len(doc["rows"]), 48)
            critical = {a["id"] for a in LLM.taxonomy()["acts"] if a["critical"]}
            self.assertTrue(all(r["act"] in critical for r in doc["rows"]))

    def test_a_critical_sentence_credited_under_an_ordinary_act_is_counted_and_a_critical_act_is_not(self):
        ids = [a["id"] for a in LLM.taxonomy()["acts"]]
        critical = {a["id"] for a in LLM.taxonomy()["acts"] if a["critical"]}
        head = {"kind": "embedding_logreg_head.v1", "dimension": 3, "classes": ids, "coef": [[8.0, 0.0, 0.0] if c == "ASK_TOPIC" else [0.0, 0.0, 0.0] for c in ids], "intercept": [0.0] * len(ids),
                "thresholds": {c: (None if c in critical or c == "NONE" else 0.5) for c in ids}, "_critical": critical}
        ordinary = E.EmbeddingActClassifier(lambda text: [1.0, 0.0, 0.0], head)               # always names ASK_TOPIC with a large margin
        result = A.evaluate(ordinary, "holdout")
        # every sentence the regular-expression guard does not catch is credited under ASK_TOPIC: counted; the ones it catches are not credited
        from classify.guard import critical_cue
        rows = json.loads(A.SETS["holdout"].read_text(encoding="utf-8"))["rows"]
        self.assertEqual(result["credited_as_ordinary_count"], sum(1 for r in rows if not critical_cue(r["text"])))
        head2 = dict(head, coef=[[8.0, 0.0, 0.0] if c == "STATE_ALLERGY" else [0.0, 0.0, 0.0] for c in ids])
        named = E.EmbeddingActClassifier(lambda text: [1.0, 0.0, 0.0], head2)                 # always names a critical act: recorded, never credited
        self.assertEqual(A.evaluate(named, "holdout")["credited_as_ordinary_count"], 0)

    def test_the_committed_report_shows_the_change(self):
        r = json.loads((ROOT / "reports" / "ai" / "classifier-adversarial.json").read_text(encoding="utf-8"))
        self.assertGreater(r["before_any_change_17_oct"]["tuning"]["credited_as_ordinary_count"], 0)
        self.assertEqual(r["shipped_head_with_cues_v3_and_mass_guard"]["tuning"]["credited_as_ordinary_count"], 0)
        self.assertEqual(r["shipped_head_with_cues_v3_and_mass_guard"]["holdout"]["credited_as_ordinary_count"], 0)


if __name__ == "__main__":
    unittest.main()
