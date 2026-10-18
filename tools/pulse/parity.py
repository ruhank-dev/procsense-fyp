"""LitePhysio against Pulse on the same scenarios (p1-pulse-physiology-s03, EXP-PULSE-1 and EXP-PULSE-2): `uv run --project tools tools/pulse/parity.py [--check]`.

Inputs: the scenarios of content/physio/pulse_mapping.json, the Pulse traces recorded by tools/pulse/pulse_bridge.py (content/physio/pulse_traces), and the LitePhysio equations (content/physio/*.json, stepped one second at a
time exactly as tools/physio/plot_effects.py does, which the C# engine is tested against). No Pulse is needed to run this; it reads the committed traces.

For every scenario and variable (heart rate, systolic and diastolic pressure, respiratory rate, oxygen saturation) and for the window after each action it compares the LARGEST change inside the window with the
value just before the action (so a short effect such as a bolus of adrenaline is not lost by the end of the window). A change smaller than the dead band (HR 2/min, BP 3 mmHg, RR 1/min, SpO2 1 point) counts as no change. Reported: whether the two engines move in the same DIRECTION, and the size of each
change. Pulse's skin temperature is recorded but not compared (the Core's Temp is a core temperature and Pulse cools the skin in a room). What it cannot say: which engine is nearer a real patient (the
mapped actions are not the same dose of anything), and the plan's reviewer preference (a person looking at the plots; none has) and the headset cost (Sannan, DC-05). The cost of Pulse on the desktop is in each trace.
Outputs: docs/research/physio-plots/pulse-parity/ (results.json, parity.md and one SVG per scenario).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "physio"))
import plot_effects as PE  # noqa: E402

OUT = ROOT / "docs" / "research" / "physio-plots" / "pulse-parity"
TRACES = ROOT / "content" / "physio" / "pulse_traces"
COMPARED = ["HR", "SBP", "DBP", "RR", "SpO2"]
DEAD = {"HR": 2.0, "SBP": 3.0, "DBP": 3.0, "RR": 1.0, "SpO2": 1.0}
COLORS = {"HR": "#c0392b", "SBP": "#2c3e50", "DBP": "#7f8c8d", "RR": "#2980b9", "SpO2": "#27ae60"}


def lite(scenario: dict, model: dict, couplings: list[dict], effects: dict) -> list[dict]:
    """LitePhysio stepped one second at a time, with the scenario's actions applied at their times; sampled every sample_every_s."""
    variables = {v["id"]: v for v in model["variables"]}
    base = {**PE.NORMAL, **scenario["baseline"]}
    state = dict(base)
    applied: list[tuple[dict, int]] = []
    out = [{"t": 0, **{k: state[k] for k in COMPARED}}]
    pending = sorted(scenario["lite_events"], key=lambda e: e["t"])
    for t in range(1, scenario["duration_s"] + 1):
        while pending and pending[0]["t"] < t:
            applied.append((effects[pending[0]["id"]], pending.pop(0)["t"]))
        targets = dict(base)
        for eff, start in applied:
            f = PE.factor(eff["profile"], t - start)
            for k, v in eff["targets"].items():
                targets[k] += v * f
        for c in couplings:
            targets[c["target"]] += c["gain"] * (state[c["source"]] - base[c["source"]])
        for vid, v in variables.items():
            step = min(1.0, 1.0 / v["tau_seconds"])
            clamp = lambda x, v=v: max(v["min"], min(v["max"], x))
            state[vid] = clamp(state[vid] + (clamp(targets[vid]) - state[vid]) * step)
        if t % scenario["sample_every_s"] == 0:
            out.append({"t": t, **{k: state[k] for k in COMPARED}})
    return out


def at(samples: list[dict], t: float, var: str) -> float:
    return min(samples, key=lambda s: abs(s["t"] - t))[var]


def peak_change(samples: list[dict], start: int, end: int, var: str) -> tuple[float, int]:
    """The largest change (signed) from the value at `start` inside [start, end], and when it happened: a short effect such as a bolus of adrenaline is caught, not lost by the end of the window."""
    x0 = at(samples, start, var)
    best, when = 0.0, start
    for s in samples:
        if start <= s["t"] <= end and abs(s[var] - x0) > abs(best):
            best, when = s[var] - x0, s["t"]
    return best, when - start


def windows(scenario: dict) -> list[tuple[str, int, int]]:
    ev = sorted(scenario["lite_events"], key=lambda e: e["t"])
    ends = [e["t"] for e in ev[1:]] + [scenario["duration_s"]]
    return [(e["id"], e["t"], end) for e, end in zip(ev, ends)]


def compare(scenario: dict, pulse: list[dict], lite_s: list[dict]) -> list[dict]:
    rows = []
    for name, start, end in windows(scenario):
        for var in COMPARED:
            dp, tp = peak_change(pulse, start, end, var)
            dl, tl = peak_change(lite_s, start, end, var)
            sp = 0 if abs(dp) < DEAD[var] else (1 if dp > 0 else -1)
            sl = 0 if abs(dl) < DEAD[var] else (1 if dl > 0 else -1)
            rows.append({"window": f"{name} ({start} to {end} s)", "variable": var, "pulse_change": round(dp, 2), "lite_change": round(dl, 2), "pulse_peak_after_s": tp, "lite_peak_after_s": tl,
                         "relevant": bool(sp or sl), "same_direction": sp == sl, "pulse_moves_lite_does_not": bool(sp and not sl), "lite_moves_pulse_does_not": bool(sl and not sp)})
    return rows


def svg(title: str, pulse: list[dict], lite_s: list[dict], duration: int) -> str:
    w, h, cols = 330, 150, 3
    cells = []
    for i, var in enumerate(COMPARED):
        lo = min(min(s[var] for s in pulse), min(s[var] for s in lite_s))
        hi = max(max(s[var] for s in pulse), max(s[var] for s in lite_s))
        pad = max((hi - lo) * 0.1, DEAD[var])
        x, y = (i % cols) * w, (i // cols) * h + 24
        cells.append(f'<g transform="translate({x},{y})">' + PE.line_chart(var, [("Pulse", [s[var] for s in pulse], "#e67e22"), ("Lite", [s[var] for s in lite_s], "#2c3e50")], (lo - pad, hi + pad), w, h, f"0 to {duration // 60} min") + "</g>")
    rows = (len(COMPARED) + cols - 1) // cols
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{cols * w}" height="{rows * h + 24}" font-family="sans-serif">'
            f'<text x="6" y="14" font-size="12">{title}: Pulse (orange) against LitePhysio (dark), same actions at the same times</text>' + "".join(cells) + "</svg>\n")


def build() -> dict[str, str]:
    m = json.loads((ROOT / "content" / "physio" / "pulse_mapping.json").read_text(encoding="utf-8"))
    model = json.loads((PE.PHYSIO / "model.json").read_text(encoding="utf-8"))
    couplings = json.loads((PE.PHYSIO / "couplings.json").read_text(encoding="utf-8"))["couplings"]
    effects = {e["id"]: e for e in json.loads((PE.PHYSIO / "effects.json").read_text(encoding="utf-8"))["effects"]}
    files: dict[str, str] = {}
    results, md_rows = [], []
    for s in m["scenarios"]:
        trace = json.loads((TRACES / f"{s['id']}.json").read_text(encoding="utf-8"))
        lt = lite(s, model, couplings, effects)
        rows = compare(s, trace["samples"], lt)
        files[f"{s['id']}.svg"] = svg(s["label"], trace["samples"], lt, s["duration_s"])
        relevant = [r for r in rows if r["relevant"]]
        results.append({"scenario": s["id"], "label": s["label"], "pulse": trace["engine"]["version"] + "-" + trace["engine"]["git_hash"][:8], "ms_of_wall_time_per_simulated_second": round(trace["wall_seconds_per_simulated_second"] * 1000, 1),
                        "compared": len(rows), "relevant": len(relevant), "same_direction": sum(r["same_direction"] for r in relevant), "rows": rows})
        for r in rows:
            if r["relevant"]:
                md_rows.append(f"| {s['id']} | {r['window']} | {r['variable']} | {r['pulse_change']:+.1f} ({r['pulse_peak_after_s']}) | {r['lite_change']:+.1f} ({r['lite_peak_after_s']}) | {'yes' if r['same_direction'] else '**no**'} |")
    rel = sum(r["relevant"] for r in results)
    same = sum(r["same_direction"] for r in results)
    cost = [r["ms_of_wall_time_per_simulated_second"] for r in results]
    files["results.json"] = json.dumps({"synthetic_actions": True, "note": "Mapped actions are not the same dose of anything; compare shape and direction, not numbers. No reviewer has judged the plots.", "relevant_comparisons": rel, "same_direction": same,
                                       "same_direction_share": round(same / rel, 3) if rel else None, "pulse_ms_per_simulated_second_min": min(cost), "pulse_ms_per_simulated_second_max": max(cost), "scenarios": results}, indent=1) + "\n"
    md = ["# Pulse against LitePhysio (EXP-PULSE-1 and EXP-PULSE-2)", "",
          "Generated by `tools/pulse/parity.py` from `content/physio/pulse_mapping.json`, the Pulse traces in `content/physio/pulse_traces` and the LitePhysio equations; do not edit by hand. Pulse " + results[0]["pulse"] +
          " built on this desktop (tools/pulse/README.md). Each row is the largest change inside the window after an action, in the unit of the variable, with a dead band (HR 2, BP 3, RR 1, SpO2 1) under which a change counts as none.", "",
          f"**{same} of {rel} comparisons where either engine moves agree in direction ({round(100 * same / rel)} percent).** Cost of Pulse on the desktop: {min(cost):.0f} to {max(cost):.0f} ms of wall time per simulated second (50 steps of 20 ms, so about {min(cost) / 50:.1f} to {max(cost) / 50:.1f} ms a step), processor shared with other work; the headset figure is Sannan's (DC-05).", "",
          "| Scenario | Window | Variable | Pulse (peak after s) | Lite (peak after s) | Same direction |", "|---|---|---|---|---|---|"] + md_rows + ["",
          "Plots: " + ", ".join(f"[{s['id']}]({s['id']}.svg)" for s in m["scenarios"]) + ".", "",
          "What the disagreements are about (read from the rows): LitePhysio's fluid bolus raises pressure whether or not the cause of the loss is still acting, while Pulse's bleeding continues and the bolus only slows the fall; "
          "Pulse's morphine slows the heart as well as the breathing, LitePhysio's opioid pattern does not touch the heart rate; LitePhysio's pain pattern raises the diastolic pressure, Pulse's acute stress barely moves it. "
          "Neither engine is corrected from the other here: these are the candidates for the reviewer's choice (none has been made) and for a Pulse-driven option in practice mode.", ""]
    files["parity.md"] = "\n".join(md)
    return files


def main(argv: list[str]) -> int:
    files = build()
    if "--check" in argv:
        bad = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8") != t]
        print("pulse parity " + ("current" if not bad else "OUT OF DATE (" + ", ".join(bad) + "): run tools/pulse/parity.py"))
        return 1 if bad else 0
    OUT.mkdir(parents=True, exist_ok=True)
    for n, t in files.items():
        (OUT / n).write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files to {OUT.relative_to(ROOT).as_posix()}")
    print(files["parity.md"].split("\n")[4])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
