"""Builds the content the service verifies with: one folder per content version, for practice and for assessment (p1-backend-s09).

    uv run --project tools tools/content/package_versions.py --out server/engine/content

A headset plays with a content package of one mode (`package.py`), and writes that package's version into every session it saves. To replay such a session the service needs exactly that content, so it holds a folder
named by the version (12 characters of the content hash, then the mode) for each package it can verify. The folders are outside git (`server/engine/` is ignored): they are rebuilt from `content/` whenever an image is built.
"""
from __future__ import annotations

import json
import shutil
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import package  # noqa: E402

MODES = ("practice", "assessment")


def build(out: Path) -> list[str]:
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    versions = []
    with tempfile.TemporaryDirectory() as tmp:
        for mode in MODES:
            staged = Path(tmp) / mode
            manifest = package.write(mode, staged)
            version = manifest["content_version"]
            shutil.move(str(staged), str(out / version))
            versions.append(version)
    (out / "versions.json").write_text(json.dumps({"versions": versions}, indent=1) + "\n", encoding="utf-8", newline="\n")
    return versions


def main(argv: list[str]) -> int:
    if "--out" not in argv:
        print("usage: package_versions.py --out <folder>")
        return 2
    out = Path(argv[argv.index("--out") + 1])
    versions = build(out)
    print("content for verification: " + ", ".join(versions) + f" in {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
