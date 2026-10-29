"""Turns an OVR Metrics Tool CSV from the weekly profiling route into the row of docs/plans/p0/p0-vr-platform/perf/README.md and judges it against the gate
(p0-evaluation-performance-s03; T-Q1, T-Q4: average at least 72 fps, no frame over 50 ms, memory at most 2.5 GB), and against last week: a drop of more than 5 %
in average fps, or a rise of more than 5 % in the worst frame or the memory, is a regression that needs a fix step.

    uv run --project tools tools/unity/perf_capture.py docs/plans/p0/p0-vr-platform/perf/2026-11-02-demo-abc1234.csv [--by Moiz] [--write]

OVR Metrics names its columns differently between versions, so each measure is looked up under the names it is known to use; a CSV without them is refused, not guessed.
Standard library only.
"""
from __future__ import annotations

import argparse
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
README = ROOT / "docs" / "plans" / "p0" / "p0-vr-platform" / "perf" / "README.md"
GATE_FPS, GATE_FRAME_MS, GATE_MEMORY_GB, REGRESSION = 72.0, 50.0, 2.5, .05
NAME = re.compile(r"^(\d{4}-\d{2}-\d{2})-([a-z]+)-([0-9a-f]{7,40})\.csv$")
COLUMNS = {
    "fps": ["average_frame_rate", "fps", "FPS", "frame_rate"],
    "frame_ms": ["app_frame_time_ms", "frame_time_ms", "app_gpu_time_ms", "Frame Time"],
    "memory_mb": ["app_pss_MB", "app_pss_mb", "used_memory_MB", "memory_mb", "Memory (MB)"],
}


def column(header: list[str], measure: str) -> str | None:
    return next((c for c in COLUMNS[measure] if c in header), None)


def summarise(path: Path) -> dict:
    with path.open(encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    if not rows:
        raise ValueError(f"{path.name}: no rows")
    header = list(rows[0])
    fps_col, frame_col, mem_col = column(header, "fps"), column(header, "frame_ms"), column(header, "memory_mb")
    if fps_col is None or mem_col is None:
        raise ValueError(f"{path.name}: no fps or memory column (looked for {COLUMNS['fps']} and {COLUMNS['memory_mb']}); columns are {header[:12]}")
    fps = [float(r[fps_col]) for r in rows if (r.get(fps_col) or "").strip()]
    mem = [float(r[mem_col]) for r in rows if (r.get(mem_col) or "").strip()]
    frames = [float(r[frame_col]) for r in rows if frame_col and (r.get(frame_col) or "").strip()]
    worst = max(frames) if frames else 1000.0 / min(fps)   # without frame times, the slowest second's average frame
    low = sorted(fps)[max(0, len(fps) // 100 - 1)] if len(fps) >= 100 else min(fps)
    s = {"avg_fps": sum(fps) / len(fps), "low_fps": low, "worst_ms": worst, "peak_gb": max(mem) / 1024.0, "frames_known": bool(frames)}
    s["pass"] = s["avg_fps"] >= GATE_FPS and s["worst_ms"] <= GATE_FRAME_MS and s["peak_gb"] <= GATE_MEMORY_GB
    return s


def last_row(readme: str) -> dict | None:
    rows = [line for line in readme.splitlines() if re.match(r"^\| \d{4}-\d{2}-\d{2} \|", line)]
    if not rows:
        return None
    cells = [c.strip() for c in rows[-1].strip("|").split("|")]
    return {"avg_fps": float(cells[3]), "worst_ms": float(cells[4]), "peak_gb": float(cells[5])}


def regressions(now: dict, before: dict | None) -> list[str]:
    if before is None:
        return []
    out = []
    if now["avg_fps"] < before["avg_fps"] * (1 - REGRESSION):
        out.append(f"average fps {before['avg_fps']:.1f} -> {now['avg_fps']:.1f}")
    if now["worst_ms"] > before["worst_ms"] * (1 + REGRESSION):
        out.append(f"worst frame {before['worst_ms']:.1f} -> {now['worst_ms']:.1f} ms")
    if now["peak_gb"] > before["peak_gb"] * (1 + REGRESSION):
        out.append(f"memory {before['peak_gb']:.2f} -> {now['peak_gb']:.2f} GB")
    return out


def row(path: Path, s: dict, by: str) -> str:
    m = NAME.match(path.name)
    if not m:
        raise ValueError(f"{path.name}: name it YYYY-MM-DD-<variant>-<commit>.csv")
    date, variant, commit = m.groups()
    return f"| {date} | {variant} | {commit[:7]} | {s['avg_fps']:.1f} | {s['worst_ms']:.1f} | {s['peak_gb']:.2f} | {'yes' if s['pass'] else 'no'} | {by} |"


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("csv", type=Path)
    ap.add_argument("--by", default="")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    s = summarise(a.csv)
    text = README.read_text(encoding="utf-8")
    line = row(a.csv, s, a.by)
    regs = regressions(s, last_row(text))
    print(line)
    print(f"1% low {s['low_fps']:.1f} fps" + ("" if s["frames_known"] else "; worst frame estimated from the slowest second (no frame-time column)"))
    print("gate: " + ("pass" if s["pass"] else "FAIL"))
    if regs:
        print("regression against last week (open a fix step in the owning phase): " + "; ".join(regs))
    if a.write:
        text = text.replace("| (none yet) | | | | | | | |\n", "")
        anchor = "|---|---|---|---|---|---|---|---|\n"
        rows_end = text.index(anchor) + len(anchor)
        while rows_end < len(text) and text[rows_end] == "|":
            rows_end = text.index("\n", rows_end) + 1
        text = text[:rows_end] + line + "\n" + text[rows_end:]
        README.write_text(text, encoding="utf-8", newline="\n")
        print(f"added the row to {README.relative_to(ROOT)}")
    return 0 if s["pass"] and not regs else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
