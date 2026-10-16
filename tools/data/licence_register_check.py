"""Checks docs/research/licence_provenance_register.md against the ids it must cover (p1-dataset-analysis-s01).

    uv run --project tools tools/data/licence_register_check.py

Fails when:
- an id of the pipeline JSON (`datasets`, `model_catalog`, `external_dependencies`) or of docs/research/dataset_register.csv has no row in the register;
- a row's decision is not one of ALLOWED, ALLOWED WITH CONDITIONS, NOT USED, REJECTED, VERIFY;
- a row that is not VERIFY has no date in its Checked column (a decision has to say when the licence was read);
- the dataset register calls a dataset USED while the licence register does not allow it, or the licence register rejects one the dataset register still uses.
"""
from __future__ import annotations

import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTER = ROOT / "docs" / "research" / "licence_provenance_register.md"
DATASETS = ROOT / "docs" / "research" / "dataset_register.csv"
PIPELINE = ROOT / "docs" / "architecture" / "procsense_full_pipeline.json"
DECISIONS = ("ALLOWED WITH CONDITIONS", "ALLOWED", "NOT USED", "REJECTED", "VERIFY")
ID = re.compile(r"^(DS-\d{4}|MC-\d{2}|EXT-[A-Z0-9]+)$")
DATE = re.compile(r"\b20\d\d-\d\d-\d\d\b")


def _cells(line: str) -> list[str]:
    return [c.strip() for c in line.strip().strip("|").split("|")]


def register_rows(text: str) -> list[dict]:
    """Every row of every table that has a Decision column: its ids, item, decision and checked date (the last column)."""
    rows, header = [], None
    for line in text.splitlines():
        if not line.startswith("|"):
            header = None
            continue
        cells = _cells(line)
        if header is None:
            header = cells
            continue
        if set(line.replace("|", "").strip()) <= {"-", " ", ":"}:
            continue
        if "Decision" not in header:
            continue
        ids = [i.strip() for i in cells[0].split(",") if ID.match(i.strip())]
        rows.append(dict(ids=ids, item=cells[0] if not ids else cells[1], decision=cells[header.index("Decision")], checked=cells[-1]))
    return rows


def decision_word(decision: str) -> str | None:
    return next((d for d in DECISIONS if decision.upper().startswith(d)), None)


def required_ids(pipeline: dict, dataset_rows: list[dict]) -> set[str]:
    ids = {x["id"] for key in ("datasets", "model_catalog", "external_dependencies") for x in pipeline.get(key, [])}
    return ids | {r["id"] for r in dataset_rows}


def problems(text: str, pipeline: dict, dataset_rows: list[dict]) -> list[str]:
    rows = register_rows(text)
    out = []
    covered = {i: r for r in rows for i in r["ids"]}
    out += [f"{i}: no row in the licence register" for i in sorted(required_ids(pipeline, dataset_rows) - set(covered))]
    for r in rows:
        name = ", ".join(r["ids"]) or r["item"]
        if decision_word(r["decision"]) is None:
            out.append(f"{name}: decision '{r['decision']}' is not one of {', '.join(DECISIONS)}")
        elif decision_word(r["decision"]) != "VERIFY" and not DATE.search(r["checked"]):
            out.append(f"{name}: decision {r['decision']} but no date in the Checked column")
    for d in dataset_rows:
        row = covered.get(d["id"])
        if row is None:
            continue
        word = decision_word(row["decision"])
        if d["status"].startswith("USED") and word not in ("ALLOWED", "ALLOWED WITH CONDITIONS"):
            out.append(f"{d['id']}: the dataset register uses it but the licence register says {row['decision']}")
        if word == "REJECTED" and not d["status"].startswith(("REJECTED", "NOT")):
            out.append(f"{d['id']}: rejected in the licence register but the dataset register says {d['status']}")
    return out


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    with DATASETS.open(encoding="utf-8", newline="") as f:
        dataset_rows = list(csv.DictReader(f))
    pipeline = json.loads(PIPELINE.read_text(encoding="utf-8"))
    text = REGISTER.read_text(encoding="utf-8")
    found = problems(text, pipeline, dataset_rows)
    for p in found:
        print("  - " + p)
    rows = register_rows(text)
    print(f"licence register: {len(rows)} rows cover {len(required_ids(pipeline, dataset_rows))} required ids" + (f"; {len(found)} problems" if found else ""))
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main())
