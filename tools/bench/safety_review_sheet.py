"""Blind review sheet for the unsafe-speech sets (p1-ai-language-s16): `tools/.venv/Scripts/python.exe tools/bench/safety_review_sheet.py [--check]` writes tests/safety_challenge/review_sheet.csv.

The sheet is what independent reviewers label (docs/testing/safety_challenge_review.md): EVERY row whose label is ambiguous or clinically relevant (review_status PENDING_HUMAN_REVIEW), plus a deterministic stratified sample of the other rows (at least 3 per split and template, and 10 percent) so that the by-construction labels can be checked.
It holds the sentence and the context a reviewer needs and NOTHING the system produced: no prediction, no expected label, no template note, so a reviewer cannot be steered. The key (expected label by construction) is in the .jsonl sets; the reviewers' columns are empty until a person fills them."""
from __future__ import annotations

import csv
import hashlib
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SETS = ROOT / "tests" / "safety_challenge"
CONTEXT = {"in_window": "The step is inside the window of the act the sentence is about", "out_of_window": "The step is OUTSIDE the window of the act the sentence is about", "no_context": "Inside the window, but no patient question or read-back is waiting"}
CHOICES = "credit_allowed | no_credit | unclear"
COLUMNS = ["id", "split", "sentence", "situation", "act_in_question", "reviewer_a_label", "reviewer_b_label", "adjudicated_label", "notes"]


def pick(rows: list[dict]) -> list[dict]:
    out = []
    by = {}
    for r in rows:
        by.setdefault((r["split"], r["template"]), []).append(r)
    for key, group in sorted(by.items()):
        pend = [r for r in group if r["review_status"] == "PENDING_HUMAN_REVIEW"]
        rest = sorted((r for r in group if r["review_status"] != "PENDING_HUMAN_REVIEW"), key=lambda r: hashlib.sha256(r["id"].encode()).hexdigest())
        n = max(3, round(0.10 * len(group)))
        out += pend + rest[:max(0, n - len(pend))] if len(pend) < n else pend
    return out


def build() -> str:
    rows = []
    for sp in ("dev", "challenge", "final"):
        rows += [json.loads(x) for x in (SETS / f"{sp}.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]
    chosen = sorted(pick(rows), key=lambda r: (hashlib.sha256(("sheet" + r["id"]).encode()).hexdigest()))          # shuffled: the order carries no template or label
    buf = io.StringIO(newline="")
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(COLUMNS)
    for r in chosen:
        act = r["act"] or ""
        w.writerow([r["id"], r["split"], r["text"], CONTEXT[r["condition"]] + (" (the sentence is the second of two in the same session)" if r["position"] == 1 else ""), act, "", "", "", ""])
    return buf.getvalue()


def main(argv):
    text = build()
    out = SETS / "review_sheet.csv"
    if "--check" in argv:
        if not out.exists() or out.read_text(encoding="utf-8") != text:
            print("review_sheet.csv is out of date; run tools/bench/safety_review_sheet.py")
            return 1
        print("review_sheet.csv is current")
        return 0
    out.write_text(text, encoding="utf-8", newline="")
    print("wrote", out.relative_to(ROOT).as_posix(), text.count("\n") - 1, "rows; choices:", CHOICES)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
