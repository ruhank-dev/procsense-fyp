"""Candidate quiz items for the learning-effect study (p1-evaluation-s03): `uv run --project tools tools/eval/build_quiz.py [--check]`.

Lists the confirmed teaching points of docs/research/teaching_script_check.csv (each with its source) as CANDIDATES in content/eval/quiz_candidates.json. A candidate is a claim, not a question: a clinician reviews
the wording and writes the correct and the wrong answers before any item is put to a participant, so this file never contains a question or an answer key of its own. Only claims whose check result starts with
`confirmed` are listed (design choices of the book are not), each with the caveat the check recorded.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "docs" / "research" / "teaching_script_check.csv"
OUT = ROOT / "content" / "eval" / "quiz_candidates.json"


def build() -> str:
    with SRC.open(encoding="utf-8", newline="") as f:
        rows = [r for r in csv.DictReader(f) if r["result"].strip().lower().startswith("confirmed")]
    doc = {"note": "Candidates for the learning-effect quiz, one per confirmed teaching point. NOT questions: a clinician writes the question and the wrong answers; nothing here is put to a participant until then (p1-evaluation-s03, DT-206).",
           "status": "awaiting clinician review", "candidates": [{"id": r["claim_id"], "claim": r["claim"], "source": r["source"], "url": r["url"], "level": r["level"], "applies_to": r["applies_to"], "check": r["result"]} for r in rows]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("quiz candidates: " + ("current" if ok else "out of date: run tools/eval/build_quiz.py"))
        return 0 if ok else 1
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT).as_posix()} ({len(json.loads(text)['candidates'])} candidates)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
