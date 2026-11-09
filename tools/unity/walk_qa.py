#!/usr/bin/env python
"""Automated QA for a ProcSense walkthrough frame sequence.

Usage:
  python tools/unity/walk_qa.py <frames_dir> [--csv walk.csv] [--out unity/Logs/walk_qa]
                                [--name B17] [--k-jump 8] [--k-pop 8]

Checks (all thresholds come from the sequence's own robust statistics):
  blank  : mean luminance extreme, or >35% near-white / near-black pixels, or near-zero std
  jump   : consecutive-frame mean-luminance change that camera motion (CSV x,y,z,yaw,pitch)
           does not explain (robust z of the residual after a median-slope fit on motion)
  pop    : frame-to-frame mean abs diff of 32x18 grayscale versus the rolling median of
           neighbouring diffs, scaled by the motion, flagged by robust z (MAD)
  geom   : (needs a CSV written with BENCH_DIFF=1) diff_cull_pct above --max-cull-pct (default 0.05) = geometry
           missing versus the same pose with every cull off; diff_orig_pct above --max-orig-pct (default 0.25)
           = difference from the original exterior
  static : on runs where the camera is (almost) still, frame-to-frame diff D above --static-d (default 1.5) =
           texture/LOD pop or shader flicker; "flicker" = luminance sign alternation of >=4 in 8 frames with
           amplitude above 0.4 grey on a still camera
  void   : fraction of pixels equal to the sequence's modal 'background' colour that is
           unusually high versus the sequence (only reported if the calibration is stable)
Outputs: <out>/<name>_qa.md, <out>/<name>_flags.csv, <out>/<name>_contact_*.jpg
"""
import argparse, csv, glob, os, sys
import numpy as np
from PIL import Image, ImageDraw

SW, SH = 32, 18


def robust_z(x):
    x = np.asarray(x, float)
    med = np.median(x)
    mad = np.median(np.abs(x - med)) * 1.4826
    mad = max(mad, 1e-6)
    return (x - med) / mad


def rolling_median(x, w):
    n = len(x)
    out = np.empty(n)
    for i in range(n):
        lo, hi = max(0, i - w), min(n, i + w + 1)
        seg = np.concatenate([x[lo:i], x[i + 1:hi]])
        out[i] = np.median(seg) if len(seg) else x[i]
    return out


LUMA = np.array([0.2126, 0.7152, 0.0722], np.float32)


def _features(path, exact):
    """One frame -> (mean luminance, near-white share, near-black share, luminance std, 32x18 grey thumbnail, quantised colour codes).
    Default decodes the JPEG at half size (DCT scaling, about 3x faster); --exact decodes at full size. Fractions and means agree to well below the thresholds."""
    im = Image.open(path)
    if not exact:
        im.draft("RGB", (im.width // 2, im.height // 2))
    im = im.convert("RGB")
    a = np.asarray(im, dtype=np.float32)
    g = a @ LUMA
    thumb = np.asarray(im.convert("L").resize((SW, SH), Image.BOX), np.float32)
    step = 8 if exact else 4
    q = (a[::step, ::step].astype(np.uint8) // 8).astype(np.uint16)
    code = q[..., 0] * 1024 + q[..., 1] * 32 + q[..., 2]
    return (float(g.mean()), float((g >= 240).mean()), float((g <= 12).mean()), float(g.std()), thumb, code.astype(np.uint16))


def _work(args):
    return _features(*args)


def load(frames_dir, exact=False, workers=0, use_cache=True):
    import pickle, hashlib, time
    from multiprocessing import Pool
    files = sorted(glob.glob(os.path.join(frames_dir, "f*.jpg")))
    if not files:
        sys.exit("no frames in " + frames_dir)
    # per-frame feature cache keyed by the real directory and (size, mtime) of every file: unchanged frames are never decoded twice
    root = os.path.join(os.environ.get("PROCSENSE_WORK", r"D:\ProcSenseWork"), "frame_cache", "feat")
    cp = os.path.join(root, hashlib.sha1((os.path.realpath(frames_dir).lower() + ("|exact" if exact else "|half")).encode()).hexdigest() + ".pkl")
    cache = {}
    if use_cache and os.path.exists(cp):
        try: cache = pickle.load(open(cp, "rb"))
        except Exception: cache = {}
    sig = []
    for f in files:
        st = os.stat(f); sig.append((st.st_size, st.st_mtime_ns))
    todo = [i for i, f in enumerate(files) if cache.get(os.path.basename(f), (None, None, None))[:2] != sig[i]]
    t0 = time.time()
    if todo:
        w = workers or max(1, (os.cpu_count() or 2) - 2)
        with Pool(w) as pool:
            res = pool.map(_work, [(files[i], exact) for i in todo], chunksize=16)
        for i, r in zip(todo, res):
            cache[os.path.basename(files[i])] = (sig[i][0], sig[i][1], r)
        if use_cache:
            os.makedirs(root, exist_ok=True); pickle.dump(cache, open(cp, "wb"), protocol=4)
    print("frames %d: decoded %d, cached %d, %.1fs" % (len(files), len(todo), len(files) - len(todo), time.time() - t0), file=sys.stderr)
    feats = [cache[os.path.basename(f)][2] for f in files]
    lum = np.array([x[0] for x in feats]); white = np.array([x[1] for x in feats]); black = np.array([x[2] for x in feats])
    std = np.array([x[3] for x in feats]); small = np.array([x[4] for x in feats]); bgfrac = [x[5] for x in feats]
    return files, lum, white, black, std, small, bgfrac


def load_csv(path, n):
    if not path or not os.path.exists(path):
        return None
    with open(path, newline="") as fh:
        rows = list(csv.DictReader(fh))
    return rows[:n] if len(rows) >= n else rows


def contact_sheet(files, idxs, flags, path, cols=5, tw=320):
    th = int(tw * 9 / 16)
    rows = (len(idxs) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * tw, rows * (th + 14)), (20, 20, 20))
    d = ImageDraw.Draw(sheet)
    for k, i in enumerate(idxs):
        im = Image.open(files[i]).convert("RGB").resize((tw, th))
        x, y = (k % cols) * tw, (k // cols) * (th + 14)
        sheet.paste(im, (x, y + 14))
        d.text((x + 3, y + 1), "f%05d %s" % (i, ",".join(flags[i])), fill=(255, 255, 0))
    sheet.save(path, quality=88)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("frames")
    ap.add_argument("--csv")
    ap.add_argument("--out", default=os.path.join("unity", "Logs", "walk_qa"))
    ap.add_argument("--name")
    ap.add_argument("--k-jump", type=float, default=10.0)
    ap.add_argument("--k-pop", type=float, default=10.0)
    ap.add_argument("--skip", type=int, default=2, help="warm-up frames excluded from jump/pop stats")
    ap.add_argument("--max-sheet", type=int, default=40)
    ap.add_argument("--exact", action="store_true", help="decode every JPEG at full size (about 3x slower; default is half-size DCT decoding)")
    ap.add_argument("--workers", type=int, default=0, help="decoder processes (default: logical CPUs minus 2)")
    ap.add_argument("--no-cache", action="store_true", help="ignore and do not write the per-frame feature cache")
    ap.add_argument("--max-cull-pct", type=float, default=0.05)
    ap.add_argument("--max-orig-pct", type=float, default=0.25)
    ap.add_argument("--static-d", type=float, default=1.5)
    ap.add_argument("--static-motion", type=float, default=0.002)
    ap.add_argument("--fail-on", default="", help="comma list of flag prefixes (geom,static,flicker,blank,white,black,jump,pop) that make the exit code 1")
    a = ap.parse_args()

    name = a.name or os.path.basename(os.path.normpath(a.frames))
    csvp = a.csv
    if not csvp:
        guess = os.path.join(os.path.dirname(os.path.normpath(a.frames)), name + "_walk.csv")
        csvp = guess if os.path.exists(guess) else None
    os.makedirs(a.out, exist_ok=True)

    files, lum, white, black, std, small, qcol = load(a.frames, a.exact, a.workers, not a.no_cache)
    n = len(files)
    rows = load_csv(csvp, n)
    flags = [[] for _ in range(n)]
    detail = [dict() for _ in range(n)]

    # (a) blank / extreme frames
    for i in range(n):
        r = []
        if lum[i] < 8: r.append("blank_dark")
        if lum[i] > 247: r.append("blank_white")
        if white[i] > 0.35: r.append("white%%>35")
        if black[i] > 0.35: r.append("black%%>35")
        if std[i] < 2.0: r.append("flat")
        flags[i] += [x.replace("%%", "%") for x in r]
        detail[i].update(lum=round(float(lum[i]), 2), white=round(float(white[i]), 3),
                         black=round(float(black[i]), 3))

    # motion per consecutive pair
    motion = np.zeros(n)
    if rows and len(rows) == n:
        def g(k): return np.array([float(r[k]) for r in rows])
        pos = np.stack([g("x"), g("y"), g("z")], 1)
        yaw, pit = g("yaw"), g("pitch")
        dy = (np.diff(yaw) + 180) % 360 - 180
        dp = np.diff(pit)
        dpos = np.linalg.norm(np.diff(pos, axis=0), axis=1)
        # normalise: 1 deg rotation ~ 0.05 m translation in visual effect
        motion[1:] = dpos + 0.05 * np.hypot(dy, dp)
    else:
        print("warning: no CSV, motion terms disabled", file=sys.stderr)

    dL = np.zeros(n)
    dL[1:] = np.abs(np.diff(lum))
    D = np.zeros(n)
    D[1:] = np.abs(np.diff(small, axis=0)).mean(axis=(1, 2))
    s = a.skip
    valid = np.arange(n) > s

    # motion-binned robust z: expectation of a metric given how far the camera moved
    def binned_z(x):
        z = np.zeros(n)
        idx = np.where(valid)[0]
        m = motion[idx]
        edges = np.unique(np.quantile(m, np.linspace(0, 1, 13)))
        b = np.clip(np.searchsorted(edges, m, side="right") - 1, 0, max(len(edges) - 2, 0))
        for k in np.unique(b):
            sel = idx[b == k]
            med = np.median(x[sel])
            mad = max(np.median(np.abs(x[sel] - med)) * 1.4826, 0.05 * max(med, 0.2))
            z[sel] = (x[sel] - med) / mad
        return z

    # (b) luminance jump not explained by motion
    slope = np.median(dL[valid] / np.maximum(motion[valid], 1e-3)) if motion.any() else 0.0
    zj = binned_z(dL)
    for i in range(n):
        if valid[i] and zj[i] > a.k_jump and dL[i] > 1.5:
            flags[i].append("jump")
    # (c) popping: diff well above both the motion-bin norm and the neighbours' median
    ref = rolling_median(D, 15)
    ratio = np.zeros(n)
    ratio[valid] = D[valid] / np.maximum(ref[valid], 0.05)
    zp = binned_z(D)
    for i in range(n):
        if valid[i] and zp[i] > a.k_pop and D[i] > 2.0 and ratio[i] > 2.5:
            flags[i].append("pop")
    for i in range(n):
        detail[i].update(dL=round(float(dL[i]), 3), D=round(float(D[i]), 3),
                         ref=round(float(ref[i]), 3), zjump=round(float(zj[i]), 1),
                         zpop=round(float(zp[i]), 1), motion=round(float(motion[i]), 4))

    # (d) void: share of pixels in the modal quantised colour of the whole sequence
    allq = np.concatenate([q.ravel().astype(np.int64) for q in qcol[::20]])
    vals, cnts = np.unique(allq, return_counts=True)
    modal = vals[np.argmax(cnts)]
    modal_share = cnts.max() / allq.size
    vf = np.array([(q == modal).mean() for q in qcol])
    void_note = "modal colour code %d covers %.1f%% of sampled pixels (sequence-wide)" % (modal, 100 * modal_share)
    void_active = modal_share < 0.02  # a true clear colour would dominate only where visible
    zv = robust_z(vf)
    if void_active:
        for i in range(n):
            if vf[i] > 0.10 and zv[i] > 10:
                flags[i].append("void")
        void_note += "; check ACTIVE (flag if >10%% of frame and z>10)" % ()
    else:
        void_note += "; modal colour is ordinary scene colour (large flat surfaces), void check DISABLED (not calibratable)"
    for i in range(n):
        detail[i]["bgshare"] = round(float(vf[i]), 3)

    # (e) geometry missing versus the unculled / original-exterior reference (BENCH_DIFF=1 CSV columns)
    geom_note = "no diff columns in CSV (run the walkthrough with BENCH_DIFF=1 to enable the missing-geometry check)"
    if rows and len(rows) == n and any((r.get("diff_cull_pct") or "") != "" for r in rows):
        def fv(r, k):
            try: return float(r.get(k) or "nan")
            except ValueError: return float("nan")
        dc = np.array([fv(r, "diff_cull_pct") for r in rows]); do = np.array([fv(r, "diff_orig_pct") for r in rows])
        for i in range(n):
            if dc[i] > a.max_cull_pct: flags[i].append("geom_cull")
            if do[i] > a.max_orig_pct: flags[i].append("geom_orig")
            detail[i].update(diff_cull=round(float(dc[i]), 4), diff_orig=round(float(do[i]), 4))
        geom_note = "worst vs unculled %.4f%% (frame %d), worst vs original exterior %.4f%% (frame %d); limits %g / %g" % (
            np.nanmax(dc), int(np.nanargmax(dc)), np.nanmax(do), int(np.nanargmax(do)), a.max_cull_pct, a.max_orig_pct)
    # (f) static-camera pop and flicker: with the camera still, consecutive frames should be almost identical
    static = motion < a.static_motion
    static[:s + 1] = False
    if not (rows and len(rows) == n): static[:] = False   # no camera motion data: cannot tell a still camera from a moving one
    nstatic = int(static.sum())
    for i in range(n):
        if static[i] and D[i] > a.static_d:
            flags[i].append("static_pop")
    sg = np.sign(np.diff(lum, prepend=lum[0]))
    amp = np.abs(np.diff(lum, prepend=lum[0]))
    for i in range(8, n):
        w = slice(i - 7, i + 1)
        if static[w].all() and (amp[w] > 0.4).sum() >= 4 and (sg[w][1:] * sg[w][:-1] < 0).sum() >= 4:
            flags[i].append("flicker")
    static_note = "%d frames with a still camera; limit D>%g" % (nstatic, a.static_d)

    flagged = [i for i in range(n) if flags[i]]
    base = os.path.join(a.out, name)
    with open(base + "_flags.csv", "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["frame", "t_s", "flags", "room", "seg"] + list(detail[0].keys()))
        for i in flagged:
            r = rows[i] if rows and i < len(rows) else {}
            w.writerow([i, r.get("t", round(i / 30, 3)), "|".join(flags[i]), r.get("room", ""), r.get("seg", "")]
                       + list(detail[i].values()))
    sheets = []
    # prioritise strongest flags in the sheets
    order = sorted(flagged, key=lambda i: -(max(zj[i], zp[i]) + (50 if any(f.startswith(("blank", "white", "black", "flat", "void")) for f in flags[i]) else 0)))
    top = sorted(order[:a.max_sheet])
    for k in range(0, len(top), 20):
        p = "%s_contact_%02d.jpg" % (base, k // 20 + 1)
        contact_sheet(files, top[k:k + 20], flags, p)
        sheets.append(p)

    def cnt(tag): return sum(1 for f in flags if any(x.startswith(tag) for x in f))
    md = ["# Walk QA: %s" % name, "",
          "frames: %d, csv: %s" % (n, csvp or "none"), "",
          "| check | flagged |", "|---|---|",
          "| blank/extreme (black/white/flat) | %d |" % sum(1 for f in flags if any(x.startswith(("blank", "white", "black", "flat")) for x in f)),
          "| luminance jump unexplained by motion (z>%g) | %d |" % (a.k_jump, cnt("jump")),
          "| popping vs neighbours (z>%g) | %d |" % (a.k_pop, cnt("pop")),
          "| void/background | %d |" % cnt("void"),
          "| missing geometry (geom_*) | %d |" % cnt("geom"),
          "| still-camera pop (static_pop) | %d |" % cnt("static"),
          "| flicker | %d |" % cnt("flicker"), "",
          "Luminance mean %.1f (min %.1f, max %.1f). Jump slope %.2f grey/unit motion. Median diff D %.3f." %
          (lum.mean(), lum.min(), lum.max(), slope, np.median(D[valid])),
          "", "Void check: " + void_note, "Geometry check: " + geom_note, "Static check: " + static_note, "",
          "## Flagged frames (%d)" % len(flagged), "",
          "| frame | t | flags | room | dL | D | ref | zjump | zpop |", "|---|---|---|---|---|---|---|---|---|"]
    for i in flagged[:200]:
        r = rows[i] if rows and i < len(rows) else {}
        d = detail[i]
        md.append("| %d | %s | %s | %s | %s | %s | %s | %s | %s |" % (
            i, r.get("t", round(i / 30, 2)), ",".join(flags[i]), r.get("room", ""),
            d["dL"], d["D"], d["ref"], d["zjump"], d["zpop"]))
    ev = []
    for i in flagged:
        if ev and i - ev[-1][1] <= 3:
            ev[-1][1] = i; ev[-1][2].update(flags[i])
        else:
            ev.append([i, i, set(flags[i])])
    md += ["", "## Events (flags within 3 frames merged): %d" % len(ev), "",
           "| frames | time s | room | segment | flags | peak D | peak dL |", "|---|---|---|---|---|---|---|"]
    for lo, hi, fl in ev:
        r = rows[lo] if rows and lo < len(rows) else {}
        md.append("| %d-%d | %.2f-%.2f | %s | %s | %s | %.1f | %.1f |" % (
            lo, hi, lo / 30, hi / 30, r.get("room", ""), r.get("seg", ""), ",".join(sorted(fl)),
            D[lo:hi + 1].max(), dL[lo:hi + 1].max()))
    md += ["", "Contact sheets: " + ", ".join(os.path.basename(p) for p in sheets)]
    with open(base + "_qa.md", "w") as fh:
        fh.write("\n".join(md) + "\n")
    print("\n".join(md[:14]))
    print("flagged %d frames; report %s_qa.md" % (len(flagged), base))
    import json
    summ = {"name": name, "frames": n, "flagged": len(flagged), "events": len(ev), "geometry": geom_note,
            "counts": {t: cnt(t) for t in ("blank", "white", "black", "flat", "jump", "pop", "void", "geom", "static", "flicker")}}
    json.dump(summ, open(base + "_summary.json", "w"), indent=1)
    bad = [t for t in a.fail_on.split(",") if t and cnt(t)]
    if bad:
        print("FAIL: " + ",".join(bad)); sys.exit(1)


if __name__ == "__main__":
    main()
