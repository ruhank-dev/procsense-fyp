"""The scenario knowledge base (tools/kb, p1-dataset-analysis-s06): the base is current and sourced, an ungrounded model relation is dropped, and nothing unreviewed reaches the base."""
import csv
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "kb"))
import build_kb  # noqa: E402
import extract_relations as ex  # noqa: E402

CHUNK = "Acute kidney injury caused by poor circulation. Fluids help; no anti-inflammatories; kidney-adjusted drugs."


class Base(unittest.TestCase):
    def test_the_committed_base_is_current_valid_and_sourced(self):
        self.assertEqual(build_kb.problems(), [])

    def test_every_relation_type_is_described_and_used_or_reserved(self):
        doc = json.loads(build_kb.OUT.read_text(encoding="utf-8"))
        used = {r["type"] for r in doc["relations"]}
        self.assertTrue(used <= set(build_kb.TYPES))
        self.assertTrue({"analyte_zone", "critical_limit", "test_indicated", "treatment_clash", "whatif", "problem_result", "finding_tag", "analyte_correlation"} <= used)

    def test_a_statistical_relation_carries_n_and_an_interval_and_weak_ones_say_so(self):
        doc = json.loads(build_kb.OUT.read_text(encoding="utf-8"))
        corr = [r for r in doc["relations"] if r["type"] == "analyte_correlation"]
        self.assertTrue(corr)
        for r in corr:
            s = r["strength"]
            self.assertIn("n", s)
            self.assertEqual(s["weak"], s["n"] < 50)
        self.assertTrue(next(r for r in corr if r["id"] == "corr.wbc.crp")["strength"]["weak"])

    def test_neighbours_lists_a_nodes_relations(self):
        ids = {r["id"] for r in build_kb.neighbours("analyte:potassium")}
        self.assertIn("limit.k.high", ids)

    def test_an_unreviewed_model_relation_marked_approved_is_a_problem(self):
        sneaky = {"id": "llm.zz", "type": "llm_proposed", "subject": {"kind": "term", "id": "a"}, "predicate": "leads_to", "object": {"kind": "term", "id": "b"}, "sources": ["LLM:llm.zz"],
                  "evidence": {"kind": "llm", "file": "x", "quote": "a leads to b somewhere"}, "confidence": "llm_unreviewed", "status": "approved"}
        old = build_kb.from_approved_llm
        build_kb.from_approved_llm = lambda: [sneaky]
        try:
            found = build_kb.problems()
        finally:
            build_kb.from_approved_llm = old
        self.assertTrue(any("unreviewed model relation is marked approved" in f for f in found), found)


class Extraction(unittest.TestCase):
    def test_a_grounded_relation_passes_and_each_kind_of_ungrounded_one_is_named(self):
        ok = {"subject": "poor circulation", "predicate": "leads_to", "object": "acute kidney injury", "quote": "Acute kidney injury caused by poor circulation"}
        self.assertIsNone(ex.verify(ok, CHUNK))
        self.assertEqual(ex.verify({**ok, "quote": "Kidneys always fail from poor circulation"}, CHUNK), "quote not found word for word")
        self.assertEqual(ex.verify({**ok, "object": "sepsis"}, CHUNK), "object not in the quote")
        self.assertEqual(ex.verify({**ok, "subject": "dehydration"}, CHUNK), "subject not in the quote")
        self.assertEqual(ex.verify({**ok, "predicate": "causes"}, CHUNK), "predicate not allowed")
        self.assertEqual(ex.verify({**ok, "quote": "Fluids"}, CHUNK), "quote not found word for word")      # too short to mean anything

    def test_the_quote_may_differ_in_case_and_spacing_only(self):
        ok = {"subject": "poor circulation", "predicate": "leads_to", "object": "acute kidney injury", "quote": "acute  kidney injury\ncaused by POOR circulation"}
        self.assertIsNone(ex.verify(ok, CHUNK))

    def test_extraction_with_a_fake_model_keeps_only_grounded_relations_and_counts_the_rest(self):
        answer = {"relations": [
            {"subject": "poor circulation", "predicate": "leads_to", "object": "acute kidney injury", "quote": "Acute kidney injury caused by poor circulation"},
            {"subject": "fluids", "predicate": "indicates", "object": "recovery", "quote": "Fluids will always cure it"},
        ]}
        out = ex.extract([{"id": "X:1", "source": "s", "text": CHUNK}], lambda prompt: json.dumps(answer), "fake", "d" * 64)
        self.assertEqual(out["stats"]["proposed"], 2)
        self.assertEqual(out["stats"]["kept"], 1)
        self.assertEqual(out["stats"]["dropped"], {"quote not found word for word": 1})
        self.assertEqual(out["relations"][0]["confidence"], "llm_unreviewed")
        self.assertEqual(out["relations"][0]["status"], "review")
        again = ex.extract([{"id": "X:1", "source": "s", "text": CHUNK}], lambda prompt: json.dumps(answer), "fake", "d" * 64)
        self.assertEqual(out["relations"][0]["id"], again["relations"][0]["id"])

    def test_a_reply_that_is_not_json_is_counted_not_fatal(self):
        out = ex.extract([{"id": "X:1", "source": "s", "text": CHUNK}], lambda prompt: "not json", "fake", "d" * 64)
        self.assertEqual((out["stats"]["bad_json"], out["stats"]["kept"]), (1, 0))

    def test_the_label_sheet_and_the_score(self):
        queue = {"relations": [{"id": f"llm.{i}", "chunk": "c", "subject": "a", "predicate": "leads_to", "object": "b", "quote": "q"} for i in range(10)]}
        rows = list(csv.reader(io.StringIO(ex.sheet(queue, 10))))
        self.assertEqual(len(rows), 11)
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "sheet.csv"
            marked = [rows[0]] + [r[:-1] + ["yes" if i < 8 else "no"] for i, r in enumerate(rows[1:])]
            p.write_text("\n".join(",".join(c for c in r) for r in marked), encoding="utf-8")
            s = ex.score(str(p))
            self.assertEqual((s["marked"], s["correct"], s["precision"]), (10, 8, 0.8))
            self.assertFalse(s["gate_0_8_met"])        # the gate needs 100 marked relations

    def test_approving_moves_a_queued_relation_to_the_approved_file_with_who_approved_it(self):
        with tempfile.TemporaryDirectory() as d:
            q, a = Path(d) / "q.json", Path(d) / "a.json"
            q.write_text(json.dumps({"relations": [{"id": "llm.1", "chunk": "c", "source": "s", "subject": "poor circulation", "predicate": "leads_to", "object": "acute kidney injury", "quote": "Acute kidney injury caused by poor circulation", "model": "m", "digest": "d" * 64}]}), encoding="utf-8")
            old = (ex.QUEUE, ex.APPROVED)
            ex.QUEUE, ex.APPROVED = q, a
            try:
                ex.approve(["llm.1", "llm.nope"], "Ruhan")
            finally:
                ex.QUEUE, ex.APPROVED = old
            doc = json.loads(a.read_text(encoding="utf-8"))
            self.assertEqual(len(doc["relations"]), 1)
            self.assertIn("approved by Ruhan", doc["relations"][0]["note"])
            self.assertEqual(doc["relations"][0]["evidence"]["quote"], "Acute kidney injury caused by poor circulation")


if __name__ == "__main__":
    unittest.main()
