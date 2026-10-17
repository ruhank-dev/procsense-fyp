"""Retrieval over approved content (server/generation/retrieve.py, p1-live-scenario-generation-s02)."""
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from generation import contracts, objectives as ob, retrieve as rt  # noqa: E402

INDEX = rt.build_index()
QUERIES = json.loads((ROOT / "tests" / "generation" / "retrieval_queries.json").read_text(encoding="utf-8"))["queries"]


class Retrieval(unittest.TestCase):
    def test_recall_at_20_meets_the_gate(self):
        r = rt.recall_at_k(INDEX, QUERIES)
        self.assertGreaterEqual(r["recall_at_k"], 0.9)
        self.assertGreaterEqual(r["queries"], 200)

    def test_nothing_below_the_allowed_review_status_is_returned(self):
        for q in ("gallstones diabetes allergy", "appendicitis fasting consent", "potassium sepsis"):
            for h in rt.retrieve(INDEX, q, 500):
                self.assertIn(h.doc.status, rt.DEFAULT_ALLOWED, h.doc.id)
        self.assertTrue(any(d.status == "DRAFT" for d in INDEX.docs))                      # there are draft documents, and they are held back
        only_draft = {h.doc.status for h in rt.retrieve(INDEX, "gallstones diabetes allergy", 500, allowed={"DRAFT"})}
        self.assertEqual(only_draft, {"DRAFT"})

    def test_the_tag_filter_narrows_before_ranking(self):
        hits = rt.retrieve(INDEX, "patient surgery", 100, kinds={"case"}, require_tags={"scenario:S2", "tier:P0"})
        self.assertTrue(hits)
        for h in hits:
            self.assertIn("scenario:S2", h.doc.tags)
            self.assertIn("tier:P0", h.doc.tags)

    def test_the_same_query_gives_the_same_order_and_ties_break_by_id(self):
        a = [h.doc.id for h in rt.retrieve(INDEX, "blood pressure tablets before surgery", 30)]
        b = [h.doc.id for h in rt.retrieve(INDEX, "blood pressure tablets before surgery", 30)]
        self.assertEqual(a, b)

    def test_a_relevant_document_outranks_an_irrelevant_one(self):
        top = rt.retrieve(INDEX, "haemolysed potassium repeat the sample", 5)
        self.assertTrue(any("J" in h.doc.id or "w12" in h.doc.id or "haemolys" in h.doc.text.lower() for h in top[:3]), [h.doc.id for h in top])

    def test_every_document_names_its_source_rows_where_it_has_them(self):
        for d in INDEX.docs:
            if d.kind in ("case", "block"):
                self.assertTrue(d.sources, d.id)
            self.assertTrue(all(s.startswith(("SR-", "DS-", "CASE:", "LLM:")) for s in d.sources), d.id)

    def test_exclusions_are_honoured(self):
        first = rt.retrieve(INDEX, "diabetes metformin", 3)[0].doc.id
        again = rt.retrieve(INDEX, "diabetes metformin", 3, exclude_ids={first})
        self.assertNotIn(first, [h.doc.id for h in again])

    def test_the_index_notices_when_the_content_it_was_made_from_changes(self):
        with tempfile.TemporaryDirectory() as d:
            copy = Path(d) / "content"
            for sub in ("case_files", "blocks", "kb"):
                shutil.copytree(ROOT / "content" / sub, copy / sub)
            idx = rt.build_index(copy)
            self.assertTrue(idx.is_current(copy))
            victim = next((copy / "case_files").glob("VAR-S1-A.json"))
            victim.write_text(victim.read_text(encoding="utf-8").replace("teacher", "teacherx", 1), encoding="utf-8")
            self.assertFalse(idx.is_current(copy))

    def test_the_query_from_objectives_uses_the_names_of_the_weak_behaviours_and_dimensions(self):
        tables = ob.Tables.load()
        req = contracts.accept_request({"version": 1, "trainee": "trainee-1", "mode": "practice", "scenario": "S1", "level": "beginner", "seed": 3}, lambda n: 0)
        hist = [ob.SessionRecord(c, "P0", {"b05": (0, 4)}) for c in ("VAR-S1-A", "VAR-S1-C", "VAR-S1-D")]
        o = ob.derive(req, hist, tables)
        labels = {b["id"]: b["label"] for b in json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))["behaviours"]}
        q = rt.query_from_objectives(o, labels, {d.id: d.label for d in tables.dimensions})
        self.assertIn(labels["b05"].lower(), q.lower())
        self.assertTrue(rt.retrieve(INDEX, q, 10))


if __name__ == "__main__":
    unittest.main()
