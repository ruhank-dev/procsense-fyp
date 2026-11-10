"""Turns the Quest diagnostic build's sweep CSVs (perf/sweep_*.csv pulled off the headset, P3-PRF-01) into the measured table of docs/qa/quest2_performance_results.md
(P3-PRF-17). For every view it lists each configuration's average FPS, 1% low, CPU and GPU frame time, and the change against B0 measured in the same sweep.

    uv run --project tools tools/unity/perf_sweep.py docs/testing/results/DC-13/csv/sweep_20261110_101500Z.csv [more.csv ...]            # print the table
    uv run --project tools tools/unity/perf_sweep.py docs/testing/results/DC-13/csv/sweep_*.csv --write                                   # replace the measured block in the results file

Only numbers in the files are used: a configuration without a B0 row in the same view gets no delta, "n/a" stays "n/a", and nothing is estimated. Standard library only.
"""
from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESULTS = ROOT / "docs" / "qa" / "quest2_performance_results.md"
BEGIN, END = "<!-- measured:begin -->", "<!-- measured:end -->"
FIELDS = ("config", "label", "changes_look", "view", "room", "frames", "avg_fps", "one_percent_low_fps", "avg_cpu_ms", "avg_gpu_ms")


def read(paths: list[Path]) -> list[dict]:
    rows = []
    for p in paths:
        lines = [line for line in p.read_text(encoding="utf-8").splitlines() if line and not line.startswith("#")]
        reader = csv.DictReader(lines)
        missing = [f for f in FIELDS if f not in (reader.fieldnames or [])]
        if missing:
            raise SystemExit(f"{p}: not a sweep CSV of the diagnostic build (missing {', '.join(missing)})")
        for r in reader:
            r["source"] = p.name
            rows.append(r)
    return rows


def number(text: str) -> float | None:
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def delta(value: str, base: str, digits: int = 2) -> str:
    v, b = number(value), number(base)
    return "" if v is None or b is None else f"{v - b:+.{digits}f}"


def table(rows: list[dict]) -> str:
    out = ["| Source | View | Config | Changes the look | Frames | Avg FPS | 1% low FPS | CPU ms | GPU ms | Δ FPS vs B0 | Δ GPU ms vs B0 |",
           "|---|---|---|---|---|---|---|---|---|---|---|"]
    for (source, view) in sorted({(r["source"], r["view"]) for r in rows}):
        group = [r for r in rows if r["source"] == source and r["view"] == view]
        base = next((r for r in group if r["config"] == "B0"), None)
        for r in group:
            d_fps = delta(r["avg_fps"], base["avg_fps"]) if base and r is not base else ""
            d_gpu = delta(r["avg_gpu_ms"], base["avg_gpu_ms"]) if base and r is not base else ""
            out.append(f"| {source} | {view} | {r['config']} {r['label']} | {r['changes_look']} | {r['frames']} | {r['avg_fps']} | {r['one_percent_low_fps']} | {r['avg_cpu_ms']} | {r['avg_gpu_ms']} | {d_fps} | {d_gpu} |")
    return "\n".join(out)


def write(block: str) -> None:
    text = RESULTS.read_text(encoding="utf-8")
    if BEGIN not in text or END not in text:
        raise SystemExit(f"{RESULTS} has no {BEGIN} ... {END} block")
    head, rest = text.split(BEGIN, 1)
    _, tail = rest.split(END, 1)
    RESULTS.write_text(head + BEGIN + "\n" + block + "\n" + END + tail, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("csv", nargs="+", type=Path)
    ap.add_argument("--write", action="store_true", help="replace the measured block in docs/qa/quest2_performance_results.md")
    a = ap.parse_args(argv)
    block = table(read(a.csv))
    if a.write:
        write(block)
        print(f"wrote the measured block of {RESULTS.relative_to(ROOT)}")
    else:
        print(block)
    return 0


if __name__ == "__main__":
    sys.exit(main())
