"""Downloads a few scans (and their organ labels) of AMOS 2022 (Zenodo 7155725, CC BY 4.0) into tools/data/cache/amos/ by reading single members out of the 24 GB zip with HTTP range requests.

    uv run --project tools --extra images tools/data/fetch_amos.py amos_0088 amos_0156 amos_0540 ...

AMOS: Ji Y. et al., "AMOS: A Large-Scale Abdominal Multi-Organ Benchmark for Versatile Medical Image Segmentation", NeurIPS 2022 Datasets and Benchmarks, doi:10.5281/zenodo.7155725.
Scans 0001-0499 are CT and 0500-0600 are MRI; images and labels are NIfTI. Only a handful of files are needed (about 15 MB per CT, 3 MB per MRI), never the whole archive.
"""
from __future__ import annotations

import io
import sys
import urllib.request
import zipfile
from pathlib import Path

URL = "https://zenodo.org/api/records/7155725/files/amos22.zip/content"
CACHE = Path(__file__).resolve().parent / "cache" / "amos"


class HTTPFile(io.RawIOBase):
    def __init__(self, url: str):
        self.url, self.pos = url, 0
        h = urllib.request.urlopen(urllib.request.Request(url, headers={"Range": "bytes=0-0", "User-Agent": "procsense-fyp-data-fetch"}), timeout=60).headers
        self.size = int(h["Content-Range"].split("/")[1])

    def seekable(self): return True
    def readable(self): return True
    def tell(self): return self.pos

    def seek(self, o, w=0):
        self.pos = {0: o, 1: self.pos + o, 2: self.size + o}[w]
        return self.pos

    def read(self, n=-1):
        n = self.size - self.pos if n < 0 else min(n, self.size - self.pos)
        if n <= 0:
            return b""
        for attempt in range(5):
            try:
                d = urllib.request.urlopen(urllib.request.Request(self.url, headers={"Range": f"bytes={self.pos}-{self.pos + n - 1}", "User-Agent": "procsense-fyp-data-fetch"}), timeout=180).read()
                break
            except Exception:   # noqa: BLE001
                if attempt == 4:
                    raise
        self.pos += len(d)
        return d

    def readinto(self, b):
        d = self.read(len(b))
        b[:len(d)] = d
        return len(d)


def main(ids: list[str]) -> int:
    CACHE.mkdir(parents=True, exist_ok=True)
    z = zipfile.ZipFile(HTTPFile(URL))
    for i in ids:
        for kind in ("imagesTr", "labelsTr"):
            member = f"amos22/{kind}/{i}.nii.gz"
            out = CACHE / f"{kind}_{i}.nii.gz"
            if out.exists():
                continue
            out.write_bytes(z.read(member))
            print("fetched", member, out.stat().st_size)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
