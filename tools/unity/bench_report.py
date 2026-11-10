#!/usr/bin/env python3
"""Benchmark report: compare configs against a base config (stdlib only).

Usage: python tools/unity/bench_report.py unity/Logs/bench/a.csv [b.csv ...]
           --base B20 [--views V1,V2] [--out report.md]
Columns that are missing/empty/NaN are reported as 'unavailable'; nothing is invented.
Duplicate (config,view) rows (repeat runs, several files) are averaged.
Negative % change = lower value = better (gain).
"""
import argparse, csv, math, sys
from collections import OrderedDict

METRICS = [("done_med_ms", "done_med_ms"), ("triangles", "triangles")]


def num(s):
    try:
        v = float(str(s).strip())
    except (ValueError, TypeError):
        return None
    return v if math.isfinite(v) else None


def load(paths):
    acc = {}      # (cfg, view) -> {col: [vals]}
    seen = {}     # col -> any valid value
    order = []
    for p in paths:
        with open(p, newline="", encoding="utf-8-sig") as f:
            for r in csv.DictReader(f):
                cfg, view = (r.get("config") or "").strip(), (r.get("view") or "").strip()
                if not cfg or not view:
                    continue
                if cfg not in order:
                    order.append(cfg)
                d = acc.setdefault((cfg, view), {})
                for col, raw in r.items():
                    v = num(raw)
                    if v is not None:
                        d.setdefault(col, []).append(v)
                        seen[col] = True
    data = {k: {c: sum(v) / len(v) for c, v in d.items()} for k, d in acc.items()}
    return data, order, seen


def pct(b, v):
    return (v - b) / b * 100.0 if b else None


def gmean_pct(pairs):
    pairs = [(b, v) for b, v in pairs if b and v and b > 0 and v > 0]
    if not pairs:
        return None
    g = math.exp(sum(math.log(v / b) for b, v in pairs) / len(pairs))
    return (g - 1) * 100.0


def fmt(x, nd=2):
    return "unavailable" if x is None else f"{x:.{nd}f}"


def fp(x):
    return "unavailable" if x is None else f"{x:+.1f}%"


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("csv", nargs="+")
    ap.add_argument("--base", required=True, help="base config name, e.g. B20")
    ap.add_argument("--views", help="comma list of view names or prefixes (e.g. V1,V4)")
    ap.add_argument("--out", help="write markdown to this file")
    a = ap.parse_args()

    data, order, seen = load(a.csv)
    views = []
    for (c, v) in data:
        if v not in views:
            views.append(v)
    if a.views:
        want = [w.strip() for w in a.views.split(",") if w.strip()]
        views = [v for v in views if any(v == w or v.startswith(w + " ") for w in want)]
    out = []
    w = out.append
    w(f"# Benchmark report (base {a.base})\n")
    w("Files: " + ", ".join(a.csv) + "\n")
    unavail = [c for c, _ in METRICS if c not in seen]
    if unavail:
        w("Unavailable columns (absent or empty in every input): " + ", ".join(unavail) + "\n")
    if a.base not in order:
        sys.exit(f"base config {a.base} not found; configs: {', '.join(order)}")
    summary = []
    for cfg in order:
        if cfg == a.base:
            continue
        w(f"## {cfg} vs {a.base}\n")
        w("| view | base done_med_ms | cfg done_med_ms | % | base tris | cfg tris | % |")
        w("|---|---|---|---|---|---|---|")
        pairs = {m: [] for m, _ in METRICS}
        for v in views:
            b, c = data.get((a.base, v)), data.get((cfg, v))
            if b is None or c is None:
                continue
            row = [v]
            for m, _ in METRICS:
                bv, cv = b.get(m), c.get(m)
                row += [fmt(bv, 2 if m == "done_med_ms" else 0), fmt(cv, 2 if m == "done_med_ms" else 0),
                        fp(pct(bv, cv) if bv is not None and cv is not None else None)]
                if bv is not None and cv is not None:
                    pairs[m].append((bv, cv))
            w("| " + " | ".join(row) + " |")
        g = {m: gmean_pct(pairs[m]) for m, _ in METRICS}
        w(f"| **geomean ({len(pairs['done_med_ms'])} / {len(pairs['triangles'])} views)** | | | "
          f"**{fp(g['done_med_ms'])}** | | | **{fp(g['triangles'])}** |\n")
        summary.append((cfg, g))
    key = lambda s: (s[1]["done_med_ms"] is None, s[1]["done_med_ms"] if s[1]["done_med_ms"] is not None
                     else (s[1]["triangles"] if s[1]["triangles"] is not None else 0))
    w("## Configs sorted by gain (most negative geomean first; done_med_ms, else triangles)\n")
    w("| rank | config | geomean done_med_ms | geomean triangles |")
    w("|---|---|---|---|")
    for i, (cfg, g) in enumerate(sorted(summary, key=key), 1):
        w(f"| {i} | {cfg} | {fp(g['done_med_ms'])} | {fp(g['triangles'])} |")
    text = "\n".join(out) + "\n"
    if a.out:
        with open(a.out, "w", encoding="utf-8") as f:
            f.write(text)
    print(text)


if __name__ == "__main__":
    main()
