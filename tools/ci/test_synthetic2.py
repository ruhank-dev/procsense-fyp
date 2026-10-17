"""The AI-judged synthetic set, version 2 (tests/classifier_synthetic_v2): what it is, that it says so everywhere, that it is leakage-checked and that the committed files are what the sources and the judgements give.

Nothing here makes it independent or validated: no person wrote or labelled a row, and agreement between the two judge configurations is a consistency check, not proof that a label is right.
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "server"))
import bench_classifier as B  # noqa: E402
import dataset as D  # noqa: E402
import synthetic2 as S2  # noqa: E402


def rows(name):
    return [json.loads(line) for line in (S2.OUT / f"{name}.jsonl").read_text(encoding="utf-8").splitlines()]


class Synthetic2(unittest.TestCase):
    def test_the_committed_files_are_what_the_sources_and_judgements_give(self):
        for n, t in S2.build().items():
            self.assertEqual((S2.OUT / n).read_text(encoding="utf-8"), t, n)

    def test_structure_scenario_groups_and_leakage(self):
        data = S2.rows()
        self.assertEqual(S2.structure_problems(data), [])
        self.assertEqual({k: len(v) for k, v in data.items()}, {"train": 840, "dev": 420, "test": 600})
        leak = S2.leakage(data, False)
        self.assertEqual([k for k, v in leak["jaccard_pairs"].items() if v], [])
        for split, groups in S2.GROUPS.items():
            self.assertEqual({r["scenario_group"] for r in data[split]}, groups)

    def test_it_says_it_is_synthetic_ai_judged_and_not_independent_everywhere(self):
        for n in ("train", "dev", "test"):
            self.assertTrue(all(r["synthetic"] and r["independent"] is False for r in rows(n)), n)
        m = json.loads((S2.OUT / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual((m["synthetic"], m["independent"], m["valid_for_claims"], m["ai_judged"]), (True, False, False, True))
        self.assertIn("No person wrote", m["note"])
        a = json.loads((S2.OUT / "agreement.json").read_text(encoding="utf-8"))
        self.assertIn("not evidence that a label is right", a["note"])
        self.assertTrue((S2.OUT / "README.md").read_text(encoding="utf-8").lower().count("not independent") >= 1)

    def test_dev_and_test_rows_were_judged_twice_and_every_contested_row_adjudicated(self):
        for n in ("dev", "test"):
            rs = rows(n)
            self.assertTrue(all(r["judge_1"] and r["judge_2"] for r in rs), n)
            contested = [r for r in rs if r["judge_1"] == r["judge_2"] != r["intended_act"]]
            self.assertTrue(all(r["judge_status"] == "adjudicated" for r in contested), n)
        self.assertTrue(all(r["judge_status"] == "intent_only" for r in rows("train")))          # training rows are not judged and say so

    def test_the_final_label_differs_from_the_intent_only_by_a_recorded_adjudication(self):
        adj = S2.adjudications()
        for n in ("dev", "test"):
            for r in rows(n):
                if r["act"] != r["intended_act"]:
                    self.assertIn(r["id"], adj, r["id"])
                    self.assertEqual(adj[r["id"]][0], r["act"])

    def test_the_benchmark_loads_it_marks_it_invalid_for_claims_and_refuses_a_changed_file(self):
        data = B.load_data("synthetic2", True)
        self.assertFalse(data["valid_for_claims"])
        self.assertIn("NOT INDEPENDENT", data["provenance"])
        self.assertEqual((len(data["dev"]), len(data["test"])), (420, 600))
        with tempfile.TemporaryDirectory() as t:
            copy = Path(t)
            for f in S2.OUT.glob("*.jsonl"):
                (copy / f.name).write_bytes(f.read_bytes())
            (copy / "manifest.json").write_bytes((S2.OUT / "manifest.json").read_bytes())
            (copy / "test.jsonl").write_text((S2.OUT / "test.jsonl").read_text(encoding="utf-8") + " ", encoding="utf-8")
            old = B.SYNTH2
            B.SYNTH2 = copy
            try:
                with self.assertRaises(SystemExit) as e:
                    B.load_data("synthetic2", True)
                self.assertIn("does not match the frozen hash", str(e.exception))
            finally:
                B.SYNTH2 = old

    def test_the_taxonomy_labels_are_all_present_in_every_split(self):
        ids = {a["id"] for a in D.taxonomy()}
        for n in ("train", "dev", "test"):
            self.assertEqual({r["act"] for r in rows(n)}, ids, n)


if __name__ == "__main__":
    unittest.main()
