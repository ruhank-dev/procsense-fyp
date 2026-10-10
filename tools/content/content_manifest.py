"""Writes content/manifest.json: every content file with its size and SHA-256, and one hash for the whole content package.

    uv run --project tools tools/content/content_manifest.py            # rewrite
    uv run --project tools tools/content/content_manifest.py --check    # fail if the manifest is out of date (CI)

The content hash identifies a content version: it changes whenever any file under content/ changes (the extract folder `_extract` and the manifest itself are not part of it). A build or a
session log can record it to say exactly which content it used. The manifest also counts files per folder so a missing folder is noticed.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "manifest.json"
EXCLUDE_DIRS = {"_extract", "__pycache__"}


def files() -> list[Path]:
    out = [p for p in CONTENT.rglob("*") if p.is_file() and p != OUT and not (set(p.relative_to(CONTENT).parts) & EXCLUDE_DIRS)]
    return sorted(out, key=lambda p: p.relative_to(CONTENT).as_posix())     # a plain string order: Path ordering ignores case on Windows but not on Linux, which changed the hash


def build() -> dict:
    entries = {}
    for p in files():
        data = p.read_bytes()
        if p.suffix in (".json", ".csv", ".md"):
            data = data.replace(b"\r\n", b"\n")      # line endings differ between Windows and Linux checkouts; the content does not
        entries[p.relative_to(CONTENT).as_posix()] = {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}
    h = hashlib.sha256("\n".join(f"{k}:{v['sha256']}" for k, v in entries.items()).encode("utf-8")).hexdigest()
    folders: dict[str, int] = {}
    for k in entries:
        folders[k.split("/")[0] if "/" in k else "(root)"] = folders.get(k.split("/")[0] if "/" in k else "(root)", 0) + 1
    return {"schema": 1, "content_hash": h, "files": len(entries), "bytes": sum(v["bytes"] for v in entries.values()), "per_folder": dict(sorted(folders.items())), "entries": entries}


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    m = build()
    text = json.dumps(m, indent=1, ensure_ascii=False) + "\n"
    if "--check" in argv:
        current = OUT.read_text(encoding="utf-8").replace("\r\n", "\n") if OUT.exists() else ""
        if current != text:
            print("content/manifest.json is out of date: run `uv run --project tools tools/content/content_manifest.py`")
            return 1
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"{m['files']} files, {m['bytes'] / 1e6:.1f} MB, content hash {m['content_hash'][:16]}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
