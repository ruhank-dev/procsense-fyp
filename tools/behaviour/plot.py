"""Draws the behaviour model's transition magnitudes for the clinician's review pack (p0-physiology-events-behaviour-s10).

    uv run --project tools tools/behaviour/plot.py            # write
    uv run --project tools tools/behaviour/plot.py --check    # fail if the files are out of date

For every transition rule, how each attribute moves when the rule fires six times in a row from the attribute's starting value (diminishing returns and 'every Nth time' applied, held inside the range).
The output is docs/research/behaviour/transition_magnitudes.csv (the numbers) and transition_magnitudes.svg (one small chart per rule), so a clinician can judge whether the magnitudes look plausible.
It reads only content/behaviour/transitions.json: no dependency beyond the standard library.
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "content" / "behaviour" / "transitions.json"
OUT = ROOT / "docs" / "research" / "behaviour"
STEPS = 6


def series(rule: dict, attr: dict) -> list[float]:
    """The attribute's value after each of STEPS triggers of this rule (the start value is the first point)."""
    value = float(attr["start"])
    points = [value]
    seen = fired = 0
    delta = rule["deltas"].get(attr["id"])
    for _ in range(STEPS):
        seen += 1
        if delta is not None and seen % rule["every"] == 0:
            value += delta * (rule["diminishing"] ** fired)
            fired += 1
        value = max(attr["min"], min(attr["max"], value))
        points.append(round(value, 4))
    return points


def build() -> tuple[str, str]:
    doc = json.loads(SRC.read_text(encoding="utf-8"))
    attrs = {a["id"]: a for a in doc["attributes"]}
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(["rule", "trigger", "attribute", "start", "after_1", "after_2", "after_3", "after_4", "after_5", "after_6", "diminishing", "every", "ref"])
    charts = []
    for rule in doc["rules"]:
        for aid in rule["deltas"]:
            pts = series(rule, attrs[aid])
            w.writerow([rule["id"], rule["trigger"], aid, *pts, rule["diminishing"], rule["every"], rule["ref"]])
            charts.append((rule["id"], aid, pts, attrs[aid]))
    cell_w, cell_h, cols = 260, 110, 3
    rows = (len(charts) + cols - 1) // cols
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{cols * cell_w}" height="{rows * cell_h + 30}" font-family="sans-serif" font-size="10">',
             '<text x="8" y="16" font-size="12">Behaviour transitions: an attribute after 1 to 6 repeats of one trigger (start value on the left)</text>']
    for i, (rid, aid, pts, a) in enumerate(charts):
        x0, y0 = (i % cols) * cell_w + 10, (i // cols) * cell_h + 34
        span = max(a["max"] - a["min"], 1e-9)
        xs = [x0 + 30 + k * (cell_w - 60) / STEPS for k in range(STEPS + 1)]
        ys = [y0 + 60 - (p - a["min"]) / span * 50 for p in pts]
        poly = " ".join(f"{x:.1f},{y:.1f}" for x, y in zip(xs, ys))
        parts.append(f'<g><text x="{x0}" y="{y0}">{rid}: {aid}</text><line x1="{x0 + 30}" y1="{y0 + 60}" x2="{x0 + cell_w - 30}" y2="{y0 + 60}" stroke="#888"/>'
                     f'<polyline points="{poly}" fill="none" stroke="#2a6fdb" stroke-width="1.5"/><text x="{x0}" y="{y0 + 75}">{pts[0]:g} to {pts[-1]:g} (range {a["min"]:g} to {a["max"]:g})</text></g>')
    parts.append("</svg>")
    return buf.getvalue(), "\n".join(parts) + "\n"


def main(argv: list[str]) -> int:
    csv_text, svg_text = build()
    files = {"transition_magnitudes.csv": csv_text, "transition_magnitudes.svg": svg_text}
    if "--check" in argv:
        stale = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8").replace("\r\n", "\n") != t]
        if stale:
            print(f"FAIL  docs/research/behaviour/{', '.join(stale)} out of date; run tools/behaviour/plot.py")
            return 1
        print("docs/research/behaviour is up to date")
        return 0
    OUT.mkdir(parents=True, exist_ok=True)
    for n, t in files.items():
        (OUT / n).write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files in docs/research/behaviour")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
