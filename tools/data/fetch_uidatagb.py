"""Downloads classes of the UIdataGB gallbladder ultrasound dataset (Mendeley Data, CC BY 4.0) into tools/data/cache/uidatagb/ and checks each file's SHA-256.

    uv run --project tools tools/data/fetch_uidatagb.py [--classes 1 3 4 5 8]

The server is slow per connection but fast across several, so each archive is fetched in parallel 4 MB ranges. Files already complete and matching their checksum are
skipped. Class numbers are the dataset's own: 1 Gallstones, 2 Abdomen and retroperitoneum, 3 cholecystitis, 4 Membranous and gangrenous cholecystitis, 5 Perforation,
6 Polyps and cholesterol crystals, 7 Adenomyomatosis, 8 Carcinoma, 9 Various causes of gallbladder wall thickening. The cache is git-ignored.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import threading
import urllib.request
from pathlib import Path

API = "https://data.mendeley.com/public-api/datasets/r6h24d2d3y/files?folder_id=root&version=2"
CACHE = Path(__file__).resolve().parent / "cache" / "uidatagb"
CHUNK = 4_000_000
THREADS = 8


def http(url: str, headers: dict | None = None, timeout: int = 90) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "procsense-fyp-data-fetch", **(headers or {})})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def fetch(entry: dict) -> None:
    name = entry["filename"].replace(" ", "_")
    path = CACHE / name
    size, want = entry["size"], entry["content_details"]["sha256_hash"]
    if path.exists() and path.stat().st_size == size and sha256(path) == want:
        print(f"{name}: already complete")
        return
    url = entry["content_details"]["download_url"]
    with open(path, "wb") as f:
        f.truncate(size)
    ranges = [(s, min(CHUNK, size - s)) for s in range(0, size, CHUNK)]
    lock = threading.Lock()
    todo = list(ranges)
    errors: list[str] = []

    def worker() -> None:
        while True:
            with lock:
                if not todo:
                    return
                start, n = todo.pop()
            for attempt in range(5):
                try:
                    data = http(url, {"Range": f"bytes={start}-{start + n - 1}"})
                    if len(data) != n:
                        raise OSError(f"short read {len(data)} of {n}")
                    with lock, open(path, "r+b") as f:
                        f.seek(start)
                        f.write(data)
                    break
                except Exception as e:   # noqa: BLE001
                    if attempt == 4:
                        errors.append(f"{start}: {e}")
    threads = [threading.Thread(target=worker) for _ in range(THREADS)]
    [t.start() for t in threads]
    [t.join() for t in threads]
    got = sha256(path)
    if errors or got != want:
        raise SystemExit(f"{name}: download incomplete or corrupt ({errors[:2]}; sha256 {got[:12]} expected {want[:12]})")
    print(f"{name}: ok, sha256 {got[:16]} matches the published checksum")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--classes", nargs="+", type=int, default=[1, 3, 4, 5, 8])
    wanted = set(ap.parse_args().classes)
    CACHE.mkdir(parents=True, exist_ok=True)
    listing = json.loads(http(API, {"Accept": "application/json"}))
    for entry in listing:
        if entry["filename"][0].isdigit() and int(entry["filename"][0]) in wanted:
            fetch(entry)
    return 0


if __name__ == "__main__":
    sys.exit(main())
