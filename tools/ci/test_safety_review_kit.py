"""Tests of the blind review kit (p1-ai-language-s17, EX-01 part B). The reviewer labels used here are TOY data made inside each test; no real review exists and none is written into the repository.
Pinned: a reviewer file holds only the blind columns and nothing a system or the generator produced; each reviewer gets their own order; a file with a missing, repeated or invalid row, or an `unclear` without a note, fails `check`; Cohen's kappa is the textbook value;
the rows that differ or are unclear go to the adjudicator; merge refuses without an adjudication, refuses the same file twice, excludes unclear rows, and reports the label-error rate of the construction as an override file without touching the sets."""
import csv
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import safety_review_kit as K  # noqa: E402


def fill(path: Path, label_of, note_of=lambda i: ""):
    rows = K.read(path)
    for r in rows:
        r["label"] = label_of(r["id"])
        r["note"] = note_of(r["id"])
    K.write(path, K.BLIND_COLUMNS, [[r[c] for c in K.BLIND_COLUMNS] for r in rows])


class Kit(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.d = Path(self.tmp.name)
        K.package(self.d)
        self.ids = [r["id"] for r in K.read(K.SHEET)]

    def tearDown(self):
        self.tmp.cleanup()

    def test_reviewer_files_are_blind_and_in_their_own_order(self):
        a, b = K.read(self.d / "reviewer_a.csv"), K.read(self.d / "reviewer_b.csv")
        self.assertEqual(len(a), 560)
        for rows in (a, b):
            self.assertEqual(list(rows[0].keys()), K.BLIND_COLUMNS)
            self.assertTrue(all(r["label"] == "" and r["note"] == "" for r in rows))
        self.assertNotEqual([r["id"] for r in a], [r["id"] for r in b])
        self.assertEqual(sorted(r["id"] for r in a), sorted(r["id"] for r in b))
        text = (self.d / "reviewer_a.csv").read_text(encoding="utf-8").lower() + json.dumps(K.read(K.SHEET)[0]).lower()
        for banned in ("credit_allowed", "no_credit", "ambiguous_review", "pending_human_review", "auto_provisional", "metamorph"):
            self.assertNotIn(banned, (self.d / "reviewer_a.csv").read_text(encoding="utf-8").lower())
        self.assertIn("sentence", text)

    def test_a_file_with_a_system_column_is_refused(self):
        with self.assertRaises(SystemExit):
            K.assert_blind(K.BLIND_COLUMNS + ["expected"])
        with self.assertRaises(SystemExit):
            K.assert_blind(["id", "sentence", "situation", "act_in_question", "label", "note", "prediction"])

    def test_check_finds_every_kind_of_defect(self):
        ids = set(self.ids)
        rows = [{"id": i, "label": "no_credit", "note": ""} for i in self.ids]
        self.assertEqual(K.check_rows(rows, ids), [])
        bad = rows[1:] + [dict(rows[2])]
        bad[0] = {"id": rows[5]["id"], "label": "maybe", "note": ""}
        bad.append({"id": "zzz", "label": "unclear", "note": ""})
        problems = " | ".join(K.check_rows(bad, ids))
        for needle in ("is missing", "appears 2 times", "is not in the sheet", "is not one of", "unclear needs a note"):
            self.assertIn(needle, problems)

    def test_kappa_is_the_textbook_value(self):
        a = ["no_credit"] * 20 + ["credit_allowed"] * 5 + ["no_credit"] * 5 + ["credit_allowed"] * 20
        b = ["no_credit"] * 20 + ["credit_allowed"] * 5 + ["credit_allowed"] * 5 + ["credit_allowed"] * 20
        self.assertAlmostEqual(K.kappa(["no_credit", "credit_allowed"] * 5, ["no_credit", "credit_allowed"] * 5), 1.0)
        self.assertAlmostEqual(K.kappa(["no_credit"] * 5 + ["credit_allowed"] * 5, ["credit_allowed"] * 5 + ["no_credit"] * 5), -1.0)
        po, pe = 0.9, (25 * 20 + 25 * 30) / 2500          # 5 of 50 rows differ; marginals 25/25 and 20/30 over 50 rows -> pe = 0.5
        self.assertAlmostEqual(K.kappa(a, b), (po - 0.5) / 0.5, places=6)

    def test_agree_sends_differences_and_unclear_rows_to_the_adjudicator_only(self):
        i1, i2, i3 = self.ids[0], self.ids[1], self.ids[2]
        fill(self.d / "reviewer_a.csv", lambda i: "unclear" if i == i3 else "no_credit", lambda i: "could be either" if i == i3 else "")
        fill(self.d / "reviewer_b.csv", lambda i: "credit_allowed" if i == i1 else "no_credit")
        self.assertEqual(K.agree(self.d / "reviewer_a.csv", self.d / "reviewer_b.csv", self.d), 0)
        adj = K.read(self.d / "adjudication.csv")
        self.assertEqual({r["id"] for r in adj}, {i1, i3})
        self.assertEqual(list(adj[0].keys()), ["id", "sentence", "situation", "act_in_question", "label_a", "label_b", "adjudicated_label", "note"])
        self.assertNotIn("expected", " ".join(adj[0].keys()))

    def test_merge_rules(self):
        i1, i3 = self.ids[0], self.ids[2]
        fill(self.d / "reviewer_a.csv", lambda i: "unclear" if i == i3 else "no_credit", lambda i: "x" if i == i3 else "")
        fill(self.d / "reviewer_b.csv", lambda i: "credit_allowed" if i == i1 else "no_credit")
        K.agree(self.d / "reviewer_a.csv", self.d / "reviewer_b.csv", self.d)
        out = self.d / "overrides.json"
        self.assertEqual(K.merge(self.d / "reviewer_a.csv", self.d / "reviewer_b.csv", self.d / "adjudication.csv", out), 1)          # nothing adjudicated yet
        self.assertFalse(out.exists())
        self.assertEqual(K.merge(self.d / "reviewer_a.csv", self.d / "reviewer_a.csv", self.d / "adjudication.csv", out), 1)          # the same file twice
        rows = K.read(self.d / "adjudication.csv")
        for r in rows:
            r["adjudicated_label"] = "unclear" if r["id"] == i3 else "no_credit"
        K.write(self.d / "adjudication.csv", list(rows[0].keys()), [list(r.values()) for r in rows])
        self.assertEqual(K.merge(self.d / "reviewer_a.csv", self.d / "reviewer_b.csv", self.d / "adjudication.csv", out), 0)
        res = json.loads(out.read_text(encoding="utf-8"))
        self.assertEqual(res["excluded_unclear"], [i3])
        self.assertEqual(res["reviewed_rows"], 559)
        wrong_total = sum(v["wrong"] for v in res["label_error_rate_by_split"].values())
        self.assertEqual(wrong_total, len(res["overrides"]))
        self.assertTrue(all(v["construction"] != v["reviewed"] for v in res["overrides"].values()))

    def test_the_sets_and_the_sheet_are_not_touched(self):
        before = {p.name: p.read_bytes() for p in K.SETS.glob("*") if p.is_file()}
        self.test_merge_rules()
        self.assertEqual(before, {p.name: p.read_bytes() for p in K.SETS.glob("*") if p.is_file()})


if __name__ == "__main__":
    unittest.main()
