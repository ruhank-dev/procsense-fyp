#!/usr/bin/env python
"""Content-hash cache of rendered walkthrough frame sets (P3-17 final QA).

A frame set is reusable when nothing that can change the picture has changed: the project's scenes, shaders, scripts, materials,
prefabs, URP assets and project settings (content hash for files up to 1 MB, size + modification time for larger ones such as
textures and models), the walkthrough test itself (path and capture code), and the capture options (config id, resolution, JPG on/off, diff on/off).

  python frame_cache.py key  --cfg B29 --w 1920 --h 1080 --diff 0 --jpg 1     print the cache key
  python frame_cache.py get  --cfg ... (same options)                           print the cached directory, exit 0 on hit, 1 on miss
  python frame_cache.py put  --cfg ... --src <dir with <cfg>/ frames and <cfg>_walk.csv>   store (move) a finished run under its key
  python frame_cache.py link --cfg ... --dest <dir>                             directory junction <dest>/<cfg> and copy of <cfg>_walk.csv from the cache
  python frame_cache.py merge --cfg ... --src <staging dir> --dest <dir>        overlay a partial run (BENCH_SEG) onto a copy of the latest full set
  python frame_cache.py prune --keep 3                                          keep the newest N entries per config

Cache root: D:\\ProcSenseWork\\frame_cache (override with PROCSENSE_WORK). Nothing is deleted except by prune.
"""
import argparse, csv, hashlib, json, os, shutil, sys, time

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UNITY = os.path.join(REPO, "unity")
WORK = os.environ.get("PROCSENSE_WORK", r"D:\ProcSenseWork")
ROOT = os.path.join(WORK, "frame_cache")
HASHED = ["Assets/Scenes", "Assets/Shaders", "Assets/Scripts", "Assets/Materials", "Assets/Prefabs", "Assets/Resources", "Assets/Settings",
          "Assets/Characters", "Assets/Animations", "Assets/Data", "Assets/XR", "Assets/XRI", "Assets/TextMesh Pro", "Assets/Models", "Assets/Textures",
          "Assets/StreamingAssets", "Assets/Tests/PlayMode/QuestWalkthrough.cs", "Assets/Tests/PlayMode/PerfSupport.cs"]
LOOSE = ["Assets/DefaultVolumeProfile.asset", "Assets/UniversalRenderPipelineGlobalSettings.asset", "Packages/manifest.json", "Packages/packages-lock.json"]
SMALL = 1 << 20
MEMO = os.path.join(ROOT, "_hash_memo.json")


def _walk(p):
    full = os.path.join(UNITY, p)
    if os.path.isfile(full):
        yield p, full
        return
    for dp, dn, fn in os.walk(full):
        dn.sort()
        for f in sorted(fn):
            fp = os.path.join(dp, f)
            yield os.path.relpath(fp, UNITY).replace(os.sep, "/"), fp


def inputs_hash():
    memo = {}
    if os.path.exists(MEMO):
        try: memo = json.load(open(MEMO))
        except Exception: memo = {}
    h = hashlib.sha1(); newmemo = {}
    files = []
    for d in HASHED + LOOSE:
        files += list(_walk(d))
    files += [("ProjectSettings/" + f, os.path.join(UNITY, "ProjectSettings", f)) for f in sorted(os.listdir(os.path.join(UNITY, "ProjectSettings"))) if f.endswith(".asset")]
    for rel, fp in files:
        try: st = os.stat(fp)
        except OSError: continue
        if st.st_size <= SMALL:
            k = "%d:%d" % (st.st_size, st.st_mtime_ns); m = memo.get(rel)
            if m and m[0] == k: dig = m[1]
            else: dig = hashlib.sha1(open(fp, "rb").read()).hexdigest()
            newmemo[rel] = [k, dig]; h.update(("%s=%s\n" % (rel, dig)).encode())
        else:
            h.update(("%s=%d:%d\n" % (rel, st.st_size, st.st_mtime_ns)).encode())
    os.makedirs(ROOT, exist_ok=True)
    json.dump(newmemo, open(MEMO, "w"))
    return h.hexdigest()


def key_for(a):
    s = "%s|%s|%sx%s|diff%s|jpg%s" % (inputs_hash(), a.cfg, a.w, a.h, a.diff, a.jpg)
    return a.cfg + "_" + a.w + "x" + a.h + "_" + hashlib.sha1(s.encode()).hexdigest()[:16]


def read_csv(p):
    with open(p, newline="") as fh:
        r = csv.DictReader(fh); return r.fieldnames, list(r)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["key", "get", "put", "link", "merge", "prune"])
    ap.add_argument("--cfg", default="B29"); ap.add_argument("--w", default="1920"); ap.add_argument("--h", default="1080")
    ap.add_argument("--diff", default="0"); ap.add_argument("--jpg", default="1")
    ap.add_argument("--src"); ap.add_argument("--dest"); ap.add_argument("--keep", type=int, default=3)
    a = ap.parse_args()
    os.makedirs(ROOT, exist_ok=True)
    if a.cmd == "prune":
        by = {}
        for d in os.listdir(ROOT):
            p = os.path.join(ROOT, d)
            if os.path.isdir(p): by.setdefault(d.split("_")[0], []).append((os.path.getmtime(p), p))
        for k, v in by.items():
            for _, p in sorted(v, reverse=True)[a.keep:]: shutil.rmtree(p); print("pruned", p)
        return
    key = key_for(a); d = os.path.join(ROOT, key)
    if a.cmd == "key": print(key)
    elif a.cmd == "get":
        ok = os.path.isdir(os.path.join(d, a.cfg)) and os.path.exists(os.path.join(d, a.cfg + "_walk.csv"))
        if ok: print(d)
        sys.exit(0 if ok else 1)
    elif a.cmd == "put":
        os.makedirs(d, exist_ok=True)
        if os.path.isdir(os.path.join(d, a.cfg)): shutil.rmtree(os.path.join(d, a.cfg))   # a re-render of the same key replaces the entry
        shutil.move(os.path.join(a.src, a.cfg), os.path.join(d, a.cfg)); shutil.copy2(os.path.join(a.src, a.cfg + "_walk.csv"), os.path.join(d, a.cfg + "_walk.csv"))
        open(os.path.join(d, "stamp.txt"), "w").write(time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()))
        json.dump({"cfg": a.cfg, "w": a.w, "h": a.h, "diff": a.diff, "jpg": a.jpg}, open(os.path.join(d, "meta.json"), "w")); print(d)
    elif a.cmd == "link":
        os.makedirs(a.dest, exist_ok=True); tgt = os.path.join(a.dest, a.cfg)
        if os.path.lexists(tgt):
            try: os.rmdir(tgt)          # removes a junction without touching its target
            except OSError: shutil.rmtree(tgt)
        os.system('cmd /c mklink /J "%s" "%s" >nul' % (tgt, os.path.join(d, a.cfg)))
        shutil.copy2(os.path.join(d, a.cfg + "_walk.csv"), os.path.join(a.dest, a.cfg + "_walk.csv")); print(tgt)
    elif a.cmd == "merge":
        # latest full set of this config and resolution (any inputs) is the base; the partial run overlays its frames and CSV rows
        cands = sorted([os.path.join(ROOT, x) for x in os.listdir(ROOT) if x.startswith(a.cfg + "_" + a.w + "x" + a.h + "_") and os.path.isdir(os.path.join(ROOT, x, a.cfg))], key=os.path.getmtime)
        if not cands: sys.exit("no base set to merge into")
        base = cands[-1]; os.makedirs(a.dest, exist_ok=True); out = os.path.join(a.dest, a.cfg)
        if os.path.exists(out): shutil.rmtree(out)
        shutil.copytree(os.path.join(base, a.cfg), out)
        for f in os.listdir(os.path.join(a.src, a.cfg)): shutil.copy2(os.path.join(a.src, a.cfg, f), os.path.join(out, f))
        fn, rows = read_csv(os.path.join(base, a.cfg + "_walk.csv")); _, new = read_csv(os.path.join(a.src, a.cfg + "_walk.csv"))
        by = {r["frame"]: r for r in rows}
        for r in new: by[r["frame"]] = r
        with open(os.path.join(a.dest, a.cfg + "_walk.csv"), "w", newline="") as fh:
            w = csv.DictWriter(fh, fieldnames=fn); w.writeheader(); w.writerows(by[k] for k in sorted(by, key=int))
        print(out, "merged", len(new), "frames over", base)


if __name__ == "__main__":
    main()
