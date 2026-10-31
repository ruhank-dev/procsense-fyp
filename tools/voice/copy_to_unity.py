"""Copies the rendered voice lines (build/voice/<line id>.ogg, from tools/voice/render.py) into the Unity project, where VoicePlayer loads them by line id
(unity/Assets/Audio/Voice/Resources/Voice/<line id>.ogg, p0-voice-audio-s03). The copies are git-ignored like build/voice: whether audio is committed is EXP-FND-1.
A line with no rendered file is reported; in the headset it is shown as a subtitle only.

Usage: uv run --project tools tools/voice/copy_to_unity.py [--check]
--check only reports what would be copied and which catalogue lines have no audio (never fails: audio is optional for the build).
"""
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "build" / "voice"
TARGET = ROOT / "unity" / "Assets" / "Audio" / "Voice" / "Resources" / "Voice"
CATALOGUE = ROOT / "content" / "voice" / "catalogue.json"


def main(argv: list[str]) -> int:
    check = "--check" in argv
    lines = json.loads(CATALOGUE.read_text(encoding="utf-8"))["lines"]
    ids = [line["id"] for line in lines if "template" not in line]
    have = [i for i in ids if (SOURCE / (i + ".ogg")).exists()]
    missing = [i for i in ids if i not in have]
    if not check:
        TARGET.mkdir(parents=True, exist_ok=True)
        for i in have:
            shutil.copy2(SOURCE / (i + ".ogg"), TARGET / (i + ".ogg"))
    verb = "would copy" if check else "copied"
    print(f"{verb} {len(have)} of {len(ids)} voice lines into {TARGET.relative_to(ROOT)}; {len(missing)} have no rendered audio (subtitle only)")
    if not SOURCE.exists():
        print("build/voice does not exist: run task voice-render first (Piper and ffmpeg, see tools/voice/README.md)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
