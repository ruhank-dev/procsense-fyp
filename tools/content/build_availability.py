"""Builds content/availability/medicine_availability.csv: one row per medicine group of content/medicines.csv with its availability flag and the source of the flag (p2-content-extensions-s04).

    uv run --project tools tools/content/build_availability.py            # write
    uv run --project tools tools/content/build_availability.py --check    # fail if the file is out of date or breaks a rule

Today every flag is `unknown` with source `none`: no source that permits this use exists (content/availability/permitted_sources.json, docs/research/drug_availability.md), and registration in the
DRAP register is never read as stock. When a permitted source is added and a file from it is supplied, rows from it replace these; a row with any other status than unknown must name a listed source.
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "availability" / "medicine_availability.csv"
CHECKED = "2026-10-18"
COLUMNS = ["medicine_id", "locally_available", "availability_source", "availability_checked", "source_kind"]


def medicine_ids() -> list[str]:
    with open(ROOT / "content" / "medicines.csv", encoding="utf-8", newline="") as f:
        return [r["id"] for r in csv.DictReader(f)]


def build() -> str:
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(COLUMNS)
    for mid in medicine_ids():
        w.writerow([mid, "unknown", "none", CHECKED, "none"])
    return buf.getvalue()


def problems(text: str) -> list[str]:
    listed = {s["id"] for s in json.loads((ROOT / "content" / "availability" / "permitted_sources.json").read_text(encoding="utf-8"))["sources"]}
    rows = list(csv.DictReader(io.StringIO(text)))
    out = []
    if {r["medicine_id"] for r in rows} != set(medicine_ids()):
        out.append("the medicine ids differ from content/medicines.csv")
    for r in rows:
        if r["locally_available"] not in ("yes", "no", "unknown"):
            out.append(f"{r['medicine_id']}: flag {r['locally_available']!r}")
        if r["locally_available"] != "unknown" and r["availability_source"] not in listed:
            out.append(f"{r['medicine_id']}: a flag other than unknown needs a listed permitted source, got {r['availability_source']!r}")
    return out


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        bad = problems(OUT.read_text(encoding="utf-8")) if OUT.exists() else ["file missing"]
        if not bad and OUT.read_text(encoding="utf-8") != text:
            bad = ["out of date: run tools/content/build_availability.py"]
        for b in bad:
            print("FAIL " + b)
        print(f"availability flags {'FAIL' if bad else 'current'} ({len(medicine_ids())} medicine groups, all unknown: no permitted source)")
        return 1 if bad else 0
    OUT.write_text(text, encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)} ({len(medicine_ids())} rows)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
