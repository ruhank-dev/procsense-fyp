"""Draws every treatment effect and vital-sign pattern, and the Usman deterioration, as curves for the clinician's review pack (p0-physiology-events-behaviour-s07, plan test EXP-PHY-1).

    uv run --project tools tools/physio/plot_effects.py            # write
    uv run --project tools tools/physio/plot_effects.py --check    # fail if the files are out of date

Each effect is applied to a calm adult (the walkthrough's all-normal row) and the LitePhysio equations are stepped one second at a time for two hours: x moves toward baseline + effect * profile + couplings by dt / tau, held in its guard bounds.
This is a second implementation of the same equations (the C# engine is the authority); `PlotTwinTests` in the unit tests read effects.csv and check the C# engine gives the same numbers, so the picture a reviewer sees is the behaviour the game has.
Every chart for one variable uses the same y-axis range, so the sizes can be compared. Output: docs/research/physio-plots/effects.csv, effects.svg and usman.svg. Standard library only.
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PHYSIO = ROOT / "content" / "physio"
OUT = ROOT / "docs" / "research" / "physio-plots"
NORMAL = {"HR": 76, "SBP": 124, "DBP": 78, "RR": 14, "SpO2": 98, "Temp": 36.7, "pain": 0, "glucose": 5.5, "nausea": 0, "sedation": 0, "volume_deficit": 0}
SECONDS = 7200
SAMPLE_EVERY = 60
# fixed y ranges per variable so charts of the same variable compare
YRANGE = {"HR": (60, 140), "SBP": (80, 160), "DBP": (40, 100), "RR": (4, 30), "SpO2": (85, 100), "Temp": (36, 40), "pain": (0, 10), "glucose": (3, 10), "nausea": (0, 10), "sedation": (0, 3), "volume_deficit": (0, 1000)}


def factor(profile: dict, age: float) -> float:
    onset, peak, plateau, offset = profile["onset_seconds"], profile["peak_seconds"], profile["plateau_seconds"], profile["offset_seconds"]
    if age < onset or age >= onset + peak + plateau + offset:
        return 0.0
    a = age - onset
    if a < peak:
        return a / peak
    a -= peak
    if a < plateau:
        return 1.0
    a -= plateau
    return 0.0 if offset <= 0 else 1 - a / offset


def simulate(model: dict, couplings: list[dict], effect: dict) -> dict[str, list[float]]:
    variables = {v["id"]: v for v in model["variables"]}
    base = dict(NORMAL)
    state = dict(NORMAL)
    out = {k: [state[k]] for k in effect["targets"]}
    for t in range(1, SECONDS + 1):
        targets = dict(base)
        f = factor(effect["profile"], t)          # the effect was applied at time 0, and the engine ages it to t after the step
        for k, v in effect["targets"].items():
            targets[k] += v * f
        for c in couplings:
            targets[c["target"]] += c["gain"] * (state[c["source"]] - base[c["source"]])
        for vid, v in variables.items():
            step = min(1.0, 1.0 / v["tau_seconds"])
            clamp = lambda x: max(v["min"], min(v["max"], x))
            state[vid] = clamp(state[vid] + (clamp(targets[vid]) - state[vid]) * step)
        if t % SAMPLE_EVERY == 0:
            for k in out:
                out[k].append(state[k])
    return out


def line_chart(title: str, series: list[tuple[str, list[float], str]], yr: tuple[float, float], w: int, h: int, x_label: str) -> str:
    left, bottom = 34, 18
    lo, hi = yr
    parts = [f'<text x="4" y="12" font-size="10">{title}</text>',
             f'<rect x="{left}" y="18" width="{w - left - 6}" height="{h - 18 - bottom}" fill="none" stroke="#bbb"/>',
             f'<text x="2" y="{h - bottom}" font-size="8">{lo:g}</text><text x="2" y="26" font-size="8">{hi:g}</text>',
             f'<text x="{left}" y="{h - 4}" font-size="8">{x_label}</text>']
    for name, ys, color in series:
        n = max(len(ys) - 1, 1)
        pts = " ".join(f"{left + k * (w - left - 6) / n:.1f},{18 + (h - 18 - bottom) * (1 - (max(lo, min(hi, y)) - lo) / (hi - lo)):.1f}" for k, y in enumerate(ys))
        parts.append(f'<polyline points="{pts}" fill="none" stroke="{color}" stroke-width="1.4"/>')
        parts.append(f'<text x="{w - 6}" y="{18 + (h - 18 - bottom) * (1 - (max(lo, min(hi, ys[-1])) - lo) / (hi - lo)):.1f}" font-size="8" text-anchor="end" fill="{color}">{name}</text>')
    return "\n".join(parts)


def build() -> dict[str, str]:
    model = json.loads((PHYSIO / "model.json").read_text(encoding="utf-8"))
    couplings = json.loads((PHYSIO / "couplings.json").read_text(encoding="utf-8"))["couplings"]
    effects = json.loads((PHYSIO / "effects.json").read_text(encoding="utf-8"))["effects"]
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(["effect", "variable", "seconds", "value"])
    cells = []
    for e in effects:
        if not e["targets"]:
            continue
        res = simulate(model, couplings, e)
        for var, ys in res.items():
            for k, y in enumerate(ys):
                w.writerow([e["id"], var, k * SAMPLE_EVERY, f"{y:.4f}"])
            cells.append((e["id"], var, ys))
    cw, ch, cols = 250, 120, 4
    rows = (len(cells) + cols - 1) // cols
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{cols * cw}" height="{rows * ch + 24}" font-family="sans-serif">',
           '<text x="6" y="14" font-size="12">Treatment effects and patterns on a calm adult over two hours (design magnitudes, provisional; same y range per variable)</text>']
    for i, (eid, var, ys) in enumerate(cells):
        x, y = (i % cols) * cw, (i // cols) * ch + 24
        svg.append(f'<g transform="translate({x},{y})">' + line_chart(f"{eid}: {var}", [(var, ys, "#2a6fdb")], YRANGE[var], cw - 6, ch - 4, "0 to 2 h") + "</g>")
    svg.append("</svg>")
    # the Usman deterioration (untreated line from the walkthrough's observation times; the treated values are the book's)
    det = json.loads((ROOT / "content" / "physiology" / "deterioration.json").read_text(encoding="utf-8"))
    tl = det["timelines"]["VAR-S2-PD"]
    pts = tl["points"]
    minutes = [p["minutes"] for p in pts]

    def interp(key: str) -> list[float]:
        out = []
        for m in range(0, minutes[-1] + 1, 5):
            for a, b in zip(pts, pts[1:]):
                if a["minutes"] <= m <= b["minutes"]:
                    out.append(a[key] + (b[key] - a[key]) * (m - a["minutes"]) / (b["minutes"] - a["minutes"]))
                    break
        return out

    us = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{4 * 250}" height="150" font-family="sans-serif">',
          '<text x="6" y="14" font-size="12">Example Patient D (Mr Usman): untreated course between the book\'s observation times (straight lines) and the treated values (dashed marker at the end)</text>']
    for i, (key, name, var) in enumerate([("hr", "heart rate", "HR"), ("sbp", "systolic pressure", "SBP"), ("rr", "breathing rate", "RR"), ("spo2", "oxygen saturation", "SpO2")]):
        ys = interp(key)
        us.append(f'<g transform="translate({i * 250},24)">' + line_chart(f"Usman: {name}", [(name, ys, "#c0392b")], YRANGE[var], 244, 120, "0 to 105 min") + "</g>")
    us.append("</svg>")
    return {"effects.csv": buf.getvalue(), "effects.svg": "\n".join(svg) + "\n", "usman.svg": "\n".join(us) + "\n"}


def main(argv: list[str]) -> int:
    files = build()
    if "--check" in argv:
        stale = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8").replace("\r\n", "\n") != t]
        if stale:
            print(f"FAIL  docs/research/physio-plots/{', '.join(stale)} out of date; run tools/physio/plot_effects.py")
            return 1
        print("docs/research/physio-plots is up to date")
        return 0
    OUT.mkdir(parents=True, exist_ok=True)
    for n, t in files.items():
        (OUT / n).write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files in docs/research/physio-plots")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
