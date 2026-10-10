"""Builds every generated content table and its source-register rows, after checking each row against the book (and the official source where opened).

    uv run --project tools tools/content/build_tables.py            # check, write CSVs, rewrite the generated register blocks
    uv run --project tools tools/content/build_tables.py --check    # check only; also fail if the files on disk differ from what would be written (CI)

Each stage module in tools/content/tables/ holds its rows as data. A failed check stops the build and writes nothing.
"""
from __future__ import annotations

import importlib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent / "tables"))
sys.path.insert(0, str(Path(__file__).resolve().parent))
import framework  # noqa: E402

MODULES = ["d1_vitals", "d2_labs", "d3_decisions", "d4_scores", "d5_medicines", "d6_structure", "d7_whatif", "d8_cases", "d10_imaging", "d9_datasets", "d11_emr", "d12_case_imaging", "d13_whatif_imaging", "d14_case_files"]


def load():
    tables, official, extra = [], {}, []
    for name in MODULES:
        m = importlib.import_module(name)
        tables += m.TABLES
        official.update(getattr(m, "OFFICIAL_FILES", {}))
        if hasattr(m, "check_points"):
            extra.append(m.check_points)
    return tables, official, extra


def same(path: Path, text: str) -> bool:
    """True when the file on disk holds exactly this text (line endings ignored)."""
    return path.exists() and path.read_text(encoding="utf-8").replace("\r\n", "\n") == text.replace("\r\n", "\n")


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    tables, official, extra = load()
    problems = []
    for t in tables:
        problems += t.check(official.get(t.name, ""))
    for fn in extra:
        problems += fn()
    for t in tables:
        t.register_rows()
    if problems:
        print(f"{len(problems)} problem(s); nothing written")
        for p in problems[:60]:
            print("  - " + p)
        return 1
    checked = sum(len(t.rows) for t in tables)
    official_rows = sum(1 for t in tables for r in t.rows if r["official"])
    print(f"{checked} rows in {len(tables)} tables: every quote is on its page, every value is in its quote ({official_rows} rows also checked against the official source)")
    if "--check" in argv:
        stale = [f"content/{t.name}.csv" for t in tables if not same(framework.CONTENT / f"{t.name}.csv", t.csv_text())]
        if not same(framework.REGISTER, framework.merged_register(tables)):
            stale.append("docs/research/source_register.csv")
        for f in stale:
            print(f"  - {f} is out of date: run `task content-build` and commit it")
        return 1 if stale else 0
    for t in tables:
        t.write_csv()
    framework.rewrite_register(tables)
    print("wrote content tables and the generated register blocks")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
