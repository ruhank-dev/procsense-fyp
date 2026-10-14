"""Builds the content the service verifies with: one folder per content version, for practice and for assessment (p1-backend-s09).

    uv run --project tools tools/content/package_versions.py --out server/engine/content

A headset plays with a content package of one mode (`package.py`), and writes that package's version into every session it saves. To replay such a session the service needs exactly that content, so it holds a folder
named by the version (12 characters of the content hash, then the mode) for each package it can verify.
A development build of the headset (and the Unity Editor) reads the whole `content/` folder instead of a package, and its version is just the 12 characters of the content hash with no mode. The service holds that content too, as a folder
named by those 12 characters, so sessions from such a build verify as well; a case that is not approved for the session's mode is still refused by the engine, so this lets no unapproved case count. `--no-dev` leaves it out (a release image). The folders are outside git (`server/engine/` is ignored): they are rebuilt from `content/` whenever an image is built.
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


def copy_raw_content(out: Path) -> str:
    """The files of content/ that its manifest lists, and the manifest, in a folder named by the version the engine gives that content (12 characters of its hash)."""
    manifest_path = package.CONTENT / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    version = manifest["content_hash"][:12]
    target = out / version
    for rel in manifest["entries"]:
        destination = target / rel
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(package.CONTENT / rel, destination)
    shutil.copyfile(manifest_path, target / "manifest.json")
    return version


def build(out: Path, with_dev: bool = True) -> list[str]:
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
    if with_dev:
        versions.append(copy_raw_content(out))
    (out / "versions.json").write_text(json.dumps({"versions": versions}, indent=1) + "\n", encoding="utf-8", newline="\n")
    return versions


def main(argv: list[str]) -> int:
    if "--out" not in argv:
        print("usage: package_versions.py --out <folder> [--no-dev]")
        return 2
    out = Path(argv[argv.index("--out") + 1])
    versions = build(out, with_dev="--no-dev" not in argv)
    print("content for verification: " + ", ".join(versions) + f" in {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
