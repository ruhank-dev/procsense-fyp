"""Pinned, dependency-free entry point; Blender runs asset code in factory startup."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
VERSION = (HERE / "VERSION").read_text().strip()


def blender():
    candidates = [os.environ.get("BLENDER_EXE"), shutil.which("blender"),
                  ROOT / "build" / "blender" / f"blender-{VERSION}-windows-x64" / "blender.exe"]
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            result = subprocess.run([str(candidate), "--version"], capture_output=True, text=True, check=True)
            actual = result.stdout.splitlines()[0]
            if actual.split()[1] != VERSION:
                raise RuntimeError(f"Expected Blender {VERSION}, found {actual}. Set BLENDER_EXE.")
            return str(candidate)
    raise RuntimeError(f"Install Blender {VERSION} or set BLENDER_EXE; see tools/blender/README.md")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["doctor", "build", "validate", "preview", "all", "selftest"])
    parser.add_argument("asset", nargs="?", default="all")
    args = parser.parse_args()
    exe = blender()
    if args.command == "doctor":
        print(f"PASS Blender {VERSION}: {exe}")
        return 0
    assets = json.loads((HERE / "requirements.json").read_text())
    if args.asset != "all" and args.asset not in assets and args.asset != "hello_cube":
        parser.error(f"Unknown asset: {args.asset}")
    return subprocess.run([exe, "-b", "--factory-startup", "--python-exit-code", "1", "-P",
                           str(HERE / "pipeline.py"), "--", args.command, args.asset], cwd=ROOT).returncode


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
