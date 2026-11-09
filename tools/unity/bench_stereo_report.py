#!/usr/bin/env python3
"""Compare configurations from the interleaved two-eye benchmark (StereoBenchmark).

Usage: python tools/unity/bench_stereo_report.py unity/Logs/bench/stereo_a.csv --base B20 [--metric done_med_ms] [--out report.md] [--seed 1]
Reads the summary csv (one row per config x cell x round) and the per-frame csv next to it (.frames.csv).
Method: per cell and round the configurations were measured back to back (interleaved, order rotated each round), so each round gives a paired ratio config/base.
Reported: median per config over rounds, coefficient of variation over rounds (noise), geometric-mean paired ratio with a 95% bootstrap interval over (cell, round) pairs,
exact two-sided sign test on the paired rounds, and per-frame p99 / max (spikes). A difference is called real only when the interval excludes 0 AND the sign test p < 0.05.
Stdlib only. Missing metrics are reported as unavailable; nothing is invented.
"""
import argparse, csv, math, random, statistics, sys
from collections import defaultdict


def num(s):
    try:
        v = float(s)
    except (TypeError, ValueError):
        return None
    return v if math.isfinite(v) else None


def sign_p(wins, n):
    if n == 0:
        return float("nan")
    k = min(wins, n - wins)
    p = sum(math.comb(n, i) for i in range(0, k + 1)) / 2 ** n
    return min(1.0, 2 * p)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("csv"); ap.add_argument("--base", default="B20"); ap.add_argument("--metric", default="done_med_ms")
    ap.add_argument("--out"); ap.add_argument("--seed", type=int, default=1); ap.add_argument("--workload", default=""); ap.add_argument("--keep-busy", action="store_true", help="keep cells measured while other heavy processes ran (default: dropped)")
    a = ap.parse_args()
    rows = list(csv.DictReader(open(a.csv, newline="", encoding="utf-8-sig")))
    if a.workload:
        rows = [r for r in rows if r["workload"] == a.workload]
    busy_n = sum(1 for r in rows if num(r.get("other_busy")) not in (None, 0.0))
    if not a.keep_busy and "other_busy" in (rows[0] if rows else {}):
        rows = [r for r in rows if num(r.get("other_busy")) in (None, 0.0)]
    cfgs = sorted({r["config"] for r in rows}, key=lambda c: (len(c), c))
    val = {(r["config"], r["cell"], int(r["round"])): num(r[a.metric]) for r in rows}
    cells = sorted({r["cell"] for r in rows}); rounds = sorted({int(r["round"]) for r in rows})
    rng = random.Random(a.seed); out = []
    out.append(f"## {a.metric} (workload: {a.workload or 'all'}), base {a.base}, {len(rounds)} rounds x {len(cells)} cells\n")
    out.append("| Config | Median ms (all cells) | CV over rounds (median of cells) | Paired ratio vs base (geo-mean) | 95% bootstrap interval | Sign test p (rounds) | Verdict |\n|---|---|---|---|---|---|---|")
    for c in cfgs:
        allv = [v for (cc, _, _), v in val.items() if cc == c and v is not None]
        cvs = []
        for cell in cells:
            xs = [val.get((c, cell, r)) for r in rounds]; xs = [x for x in xs if x]
            if len(xs) > 1 and statistics.mean(xs) > 0:
                cvs.append(statistics.pstdev(xs) / statistics.mean(xs))
        cv = statistics.median(cvs) * 100 if cvs else float("nan")
        if c == a.base:
            out.append(f"| {c} (base) | {statistics.median(allv):.2f} | {cv:.1f}% | 1.000 | | | |"); continue
        pairs = []
        for cell in cells:
            for r in rounds:
                x, b = val.get((c, cell, r)), val.get((a.base, cell, r))
                if x and b:
                    pairs.append(math.log(x / b))
        if not pairs:
            out.append(f"| {c} | unavailable | | | | | |"); continue
        g = math.exp(statistics.mean(pairs))
        boots = sorted(statistics.mean(rng.choices(pairs, k=len(pairs))) for _ in range(2000))
        lo, hi = math.exp(boots[int(0.025 * 2000)]), math.exp(boots[int(0.975 * 2000)])
        # sign test at round level: per round, geometric mean over cells; config faster when < 0
        rl = []
        for r in rounds:
            ps = [math.log(val[(c, cell, r)] / val[(a.base, cell, r)]) for cell in cells if val.get((c, cell, r)) and val.get((a.base, cell, r))]
            if ps:
                rl.append(statistics.mean(ps))
        wins = sum(1 for x in rl if x < 0); p = sign_p(wins, len(rl))
        real = (hi < 1 or lo > 1) and p < 0.05
        verdict = ("faster" if g < 1 else "slower") + " (real)" if real else "no significant difference"
        out.append(f"| {c} | {statistics.median(allv):.2f} | {cv:.1f}% | {g:.3f} ({(g - 1) * 100:+.1f}%) | {lo:.3f} to {hi:.3f} | {p:.3f} ({wins}/{len(rl)} rounds faster) | {verdict} |")
    out.append("\n### Per cell, median over rounds (ms) and workload counters (first round)\n")
    out.append("| Cell | " + " | ".join(cfgs) + " | tris " + " / ".join(cfgs) + " | draw / setpass " + a.base + " |\n|---|" + "---|" * (len(cfgs) + 2))
    cnt = {(r["config"], r["cell"]): r for r in rows if int(r["round"]) == rounds[0]}
    for cell in cells:
        ms = []
        for c in cfgs:
            xs = [val.get((c, cell, r)) for r in rounds]; xs = [x for x in xs if x]
            ms.append(f"{statistics.median(xs):.2f}" if xs else "n/a")
        tr = " / ".join(f"{float(cnt[(c, cell)]['triangles'])/1000:.0f}k" if (c, cell) in cnt and num(cnt[(c, cell)]["triangles"]) else "n/a" for c in cfgs)
        b = cnt.get((a.base, cell)); dsp = f"{b['draw_calls']} / {b['setpass_calls']}" if b else "n/a"
        out.append(f"| {cell} | " + " | ".join(ms) + f" | {tr} | {dsp} |")
    # spikes from the per-frame csv
    try:
        fr = list(csv.DictReader(open(a.csv.replace(".csv", ".frames.csv"), newline="", encoding="utf-8-sig")))
        out.append("\n### Frame-time spikes (all frames pooled, wall ms between frames, both eyes)\n")
        out.append("| Config | Workload | frames | median | p95 | p99 | p99.9 | max | frames > 2x median |\n|---|---|---|---|---|---|---|---|---|")
        g = defaultdict(list)
        for r in fr:
            if a.workload and r["workload"] != a.workload:
                continue
            v = num(r["wall_ms"])
            if v is not None:
                g[(r["config"], r["workload"])].append(v)
        for (c, w), xs in sorted(g.items(), key=lambda kv: (kv[0][1], len(kv[0][0]), kv[0][0])):
            xs.sort(); n = len(xs); q = lambda f: xs[min(n - 1, int(f * n))]
            med = xs[n // 2]
            out.append(f"| {c} | {w} | {n} | {med:.2f} | {q(.95):.2f} | {q(.99):.2f} | {q(.999):.2f} | {xs[-1]:.2f} | {sum(1 for x in xs if x > 2 * med)} |")
        # CPU side columns
        out.append("\n### CPU timing medians reported by the Editor (FrameTimingManager; blank = not reported)\n")
        out.append("| Config | submit ms (both eyes) | main thread ms | render thread ms |\n|---|---|---|---|")
        for c in cfgs:
            def med(col):
                xs = [num(r[col]) for r in rows if r["config"] == c]; xs = [x for x in xs if x]
                return f"{statistics.median(xs):.2f}" if xs else "unavailable"
            out.append(f"| {c} | {med('submit_med_ms')} | {med('main_med_ms')} | {med('render_med_ms')} |")
    except FileNotFoundError:
        out.append("\n(per-frame csv not found; spike table skipped)")
    text = "\n".join(out) + "\n"
    print(text)
    if a.out:
        open(a.out, "w", encoding="utf-8").write(text)


if __name__ == "__main__":
    main()
