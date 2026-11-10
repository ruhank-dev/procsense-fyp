#!/usr/bin/env python3
"""Per-configuration workload counters from a StereoBenchmark csv: triangles, draw calls, SetPass calls, batches, CPU submit, memory (mean over cells and rounds).
Usage: python tools/unity/bench_metrics.py unity/Logs/bench/audit_screen.csv [--base B20] [--out report.md]
Counters are exact (Unity profiler counters of the last frame); times are medians per cell averaged over cells. Cells measured with other_busy > 0 are dropped.
"""
import argparse, csv, statistics
from collections import defaultdict


def num(s):
    try:
        return float(s)
    except (TypeError, ValueError):
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("csv"); ap.add_argument("--base", default="B20"); ap.add_argument("--out")
    a = ap.parse_args()
    rows = [r for r in csv.DictReader(open(a.csv, newline="", encoding="utf-8-sig")) if (num(r.get("other_busy")) or 0) == 0]
    cols = ["triangles", "draw_calls", "setpass_calls", "batches", "submit_med_ms", "pipe_med_ms", "gfx_mem_mb", "rt_mem_mb", "shadow_casters"]
    agg = defaultdict(lambda: defaultdict(list))
    for r in rows:
        for c in cols:
            v = num(r.get(c))
            if v is not None:
                agg[r["config"]][c].append(v)
    base = {c: statistics.mean(agg[a.base][c]) for c in cols if agg[a.base][c]}
    out = [f"| Config | Triangles (k) | Draw calls | SetPass | Batches | Shadow casters | CPU submit, both eyes (ms) | GPU-bound frame (ms) | Gfx memory (MB) | RT memory (MB) |", "|---|---|---|---|---|---|---|---|---|---|"]
    def f(c, k, scale=1.0, d=0):
        v = agg[c][k]
        if not v:
            return ""
        m = statistics.mean(v) / scale
        s = f"{m:.{d}f}"
        if c != a.base and k in base and base[k]:
            s += f" ({100 * (statistics.mean(v) / base[k] - 1):+.1f}%)"
        return s
    for c in sorted(agg, key=lambda x: (x != a.base, int(x[1:].split('-')[0]) if x[1:].split('-')[0].isdigit() else 999)):
        out.append(f"| {c}{' (base)' if c == a.base else ''} | {f(c,'triangles',1000)} | {f(c,'draw_calls')} | {f(c,'setpass_calls')} | {f(c,'batches')} | {f(c,'shadow_casters')} | {f(c,'submit_med_ms',1,2)} | {f(c,'pipe_med_ms',1,2)} | {f(c,'gfx_mem_mb')} | {f(c,'rt_mem_mb')} |")
    txt = "\n".join(out)
    print(txt)
    if a.out:
        open(a.out, "w", encoding="utf-8").write(txt + "\n")


if __name__ == "__main__":
    main()
