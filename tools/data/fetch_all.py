"""Downloads exactly the open datasets the generated content is built from, into tools/data/cache/ (git-ignored), and checks them against tools/data/fetch_manifest.json.

    uv run --project tools --extra images tools/data/fetch_all.py            # fetch what is missing, then verify (about 3 GB the first time)
    uv run --project tools --extra images tools/data/fetch_all.py --check    # only verify what is already there (no network)
    uv run --project tools --extra images tools/data/fetch_all.py --list     # print what would be fetched and why
    uv run --project tools --extra images tools/data/fetch_all.py --make-manifest   # (maintainers) rewrite the manifest from the current cache

You do NOT need any of this to run the project, the tests or CI: every generated picture, report and table is committed. You need it only to REBUILD the pictures
(`task data-rebuild`) or to change which source images are used. What is fetched, and why each part is needed:

* UIdataGB (Mendeley, CC BY 4.0): the five class archives behind the real gallbladder ultrasound images (ultrasound_real_selection.json).
* AMOS 2022 (Zenodo, CC BY 4.0): ten scans (CT and MRI, with labels), read as single members out of the 24 GB archive; the base slices for CT and MRI (xsec.py).
* NIH ChestX-ray14 (Hugging Face copy of the NIH data): five parquet shards of about 450 MB each, the real chest films (cxr_real_selection.json).
* PTB-XL (PhysioNet, CC BY 4.0): the metadata file and the recordings named in content/ecg/index.json (the ECG builder fetches any further records it needs by itself).
Each file's size and SHA-256 are checked against the manifest, so a partial or corrupt download is caught. The licence and attribution of every dataset is in docs/research/datasets/.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CACHE = HERE / "cache"
MANIFEST = HERE / "fetch_manifest.json"
sys.path.insert(0, str(HERE))

AMOS_SCANS = ["amos_0004", "amos_0025", "amos_0088", "amos_0127", "amos_0259", "amos_0540", "amos_0555", "amos_0584", "amos_0600"]
UIDATAGB_CLASSES = [1, 3, 4, 5, 8]
NIH_URL = "https://huggingface.co/datasets/BahaaEldin0/NIH-Chest-Xray-14/resolve/main/data/test-{n:05d}-of-00010.parquet"


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 22), b""):
            h.update(block)
    return h.hexdigest()


def nih_shards() -> list[int]:
    sel = json.loads((HERE / "cxr_real_selection.json").read_text(encoding="utf-8"))
    names = {s["shard"] for s in sel["images"]} | {s["shard"] for s in sel["free_air_bases"]}
    return sorted(int(n.split("-")[1]) for n in names)


def ecg_records() -> list[str]:
    idx = json.loads((ROOT / "content" / "ecg" / "index.json").read_text(encoding="utf-8"))["images"]
    return sorted({i["source"]["record"] for i in idx if i.get("source")})


def wanted() -> list[dict]:
    """Every file to fetch: path relative to the cache, how to get it, and why."""
    out = []
    for c in UIDATAGB_CLASSES:
        out.append({"path": None, "kind": "uidatagb", "class": c, "why": "real gallbladder ultrasound images"})
    for s in AMOS_SCANS:
        for k in ("imagesTr", "labelsTr"):
            out.append({"path": f"amos/{k}_{s}.nii.gz", "kind": "amos", "scan": s, "why": "CT/MRI base slice" if k == "imagesTr" else "organ labels (gallbladder position)"})
    for n in nih_shards():
        out.append({"path": f"nih/test-{n:05d}.parquet", "kind": "nih", "n": n, "why": "real chest films"})
    out.append({"path": "ptbxl/ptbxl_database.csv", "kind": "ptbxl", "file": "ptbxl_database.csv", "why": "ECG metadata"})
    for r in ecg_records():
        out.append({"path": f"ptbxl/{r}.hea", "kind": "ptbxl", "file": r + ".hea", "why": "ECG recording"})
        out.append({"path": f"ptbxl/{r}.dat", "kind": "ptbxl", "file": r + ".dat", "why": "ECG recording"})
    return out


def download(url: str, dest: Path, tries: int = 5) -> None:
    """Resumable download: a partial .part file continues with a Range request."""
    dest.parent.mkdir(parents=True, exist_ok=True)
    part = dest.with_suffix(dest.suffix + ".part")
    for attempt in range(tries):
        try:
            have = part.stat().st_size if part.exists() else 0
            req = urllib.request.Request(url, headers={"User-Agent": "procsense-fyp-data-fetch", **({"Range": f"bytes={have}-"} if have else {})})
            with urllib.request.urlopen(req, timeout=120) as r, open(part, "ab" if have and r.status == 206 else "wb") as f:
                while True:
                    block = r.read(1 << 20)
                    if not block:
                        break
                    f.write(block)
            part.replace(dest)
            return
        except Exception as e:   # noqa: BLE001
            if attempt == tries - 1:
                raise SystemExit(f"could not download {url}: {e}")


def fetch(item: dict) -> None:
    if item["kind"] == "uidatagb":
        import fetch_uidatagb
        fetch_uidatagb.CACHE.mkdir(parents=True, exist_ok=True)
        listing = json.loads(fetch_uidatagb.http(fetch_uidatagb.API, {"Accept": "application/json"}))
        for entry in listing:
            if entry["filename"][0].isdigit() and int(entry["filename"][0]) == item["class"]:
                fetch_uidatagb.fetch(entry)
    elif item["kind"] == "amos":
        import fetch_amos
        fetch_amos.main([item["scan"]])
    elif item["kind"] == "nih":
        download(NIH_URL.format(n=item["n"]), CACHE / item["path"])
    elif item["kind"] == "ptbxl":
        import extract_ptbxl as ptb
        ptb.get(item["file"], binary=not item["file"].endswith((".csv", ".hea")))


def files_of(item: dict) -> list[Path]:
    if item["kind"] == "uidatagb":
        return sorted((CACHE / "uidatagb").glob(f"{item['class']}*.zip"))
    return [CACHE / item["path"]]


def manifest() -> dict:
    return json.loads(MANIFEST.read_text(encoding="utf-8")) if MANIFEST.exists() else {"files": {}}


def verify(items: list[dict], strict: bool) -> list[str]:
    known = manifest()["files"]
    problems = []
    for it in items:
        paths = files_of(it)
        if not paths:
            problems.append(f"missing: {it['kind']} class {it.get('class')}")
            continue
        for p in paths:
            rel = p.relative_to(CACHE).as_posix()
            if not p.exists():
                problems.append(f"missing: {rel}")
                continue
            want = known.get(rel)
            if want is None:
                if strict and it["kind"] != "ptbxl":
                    problems.append(f"{rel}: not in the manifest (run --make-manifest)")
            elif p.stat().st_size != want["bytes"] or sha256(p) != want["sha256"]:
                problems.append(f"{rel}: size or SHA-256 differs from the manifest (delete it and fetch again)")
    return problems


def make_manifest(items: list[dict]) -> None:
    files = {}
    for it in items:
        if it["kind"] == "ptbxl":
            continue            # tiny files, read through the PhysioNet cache; not worth pinning one by one
        for p in files_of(it):
            files[p.relative_to(CACHE).as_posix()] = {"bytes": p.stat().st_size, "sha256": sha256(p)}
    MANIFEST.write_text(json.dumps({"schema": 1, "note": "Written by fetch_all.py --make-manifest from a verified download; checked by fetch_all.py", "files": files}, indent=1) + "\n", encoding="utf-8")
    print(f"manifest written for {len(files)} files")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--make-manifest", action="store_true")
    a = ap.parse_args()
    items = wanted()
    if a.list:
        for it in items:
            if it["kind"] != "ptbxl" or it.get("file") == "ptbxl_database.csv":
                print(f"{it['kind']:9} {it.get('path') or 'class ' + str(it['class']):45} {it['why']}")
        print(f"... plus {sum(1 for i in items if i['kind'] == 'ptbxl') - 1} small PTB-XL recording files")
        return 0
    if a.make_manifest:
        make_manifest(items)
        return 0
    if not a.check:
        for it in items:
            if not all(p.exists() for p in files_of(it)) or not files_of(it):
                print(f"fetching {it['kind']} {it.get('path') or 'class ' + str(it['class'])} ({it['why']})")
                fetch(it)
    problems = verify(items, strict=True)
    for p in problems:
        print("  - " + p)
    print("all source data present and verified" if not problems else f"{len(problems)} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
