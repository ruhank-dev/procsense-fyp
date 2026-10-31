"""Runs Unity in batch mode for the project's tests and the Quest build.

    uv run --project tools tools/unity/run_unity.py test    # EditMode + PlayMode tests
    uv run --project tools tools/unity/run_unity.py build [dev|demo|study]   # Quest APK -> artifacts/unity/ProcSense-<variant>.apk (+ .sha256)

Unity is found through UNITY_EXE, or in the default Unity Hub folder for the editor version in unity/ProjectSettings/ProjectVersion.txt.
CI does not run this (the free GitHub runners have no Unity licence); it is the local check before a hand-off that touches unity/.
"""
from __future__ import annotations

import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
PROJECT = REPO / "unity"
OUT = REPO / "artifacts" / "unity"


def find_unity() -> Path:
    env = os.environ.get("UNITY_EXE")
    if env:
        return Path(env)
    version = re.search(r"m_EditorVersion: (\S+)", (PROJECT / "ProjectSettings" / "ProjectVersion.txt").read_text()).group(1)
    for root in (Path("C:/Program Files/Unity/Hub/Editor"), Path.home() / "Unity/Hub/Editor", Path("/Applications/Unity/Hub/Editor")):
        for exe in (root / version / "Editor" / "Unity.exe", root / version / "Unity.app" / "Contents" / "MacOS" / "Unity", root / version / "Editor" / "Unity"):
            if exe.exists():
                return exe
    sys.exit(f"Unity {version} not found. Install it with Unity Hub or set UNITY_EXE.")


def run(args: list[str], log: Path) -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    cmd = [str(find_unity()), "-batchmode", "-projectPath", str(PROJECT), "-logFile", str(log), *args]
    print(" ".join(cmd))
    return subprocess.call(cmd)


def test() -> int:
    failed = 0
    for platform in ("EditMode", "PlayMode"):
        result = OUT / f"{platform}.xml"
        code = run(["-runTests", "-testPlatform", platform, "-testResults", str(result)], OUT / f"{platform}.log")
        if not result.exists():
            print(f"{platform}: no result file, see {OUT / (platform + '.log')}")
            failed += 1
            continue
        root = ET.parse(result).getroot()
        print(f"{platform}: {root.get('passed')} passed, {root.get('failed')} failed (exit {code})")
        failed += int(root.get("failed", "1")) + (1 if code not in (0, 2) else 0)
    return 1 if failed else 0


VARIANTS = ("dev", "demo", "study")


def build(variant: str = "dev") -> int:
    code = run(["-nographics", "-executeMethod", "ProcSense.EditorTools.BuildScript.BuildQuest", "-variant", variant], OUT / f"build-{variant}.log")
    apk = OUT / f"ProcSense-{variant}.apk"
    print("APK:", apk if apk.exists() else f"not produced, see build-{variant}.log")
    return code


if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in ("test", "build") or (sys.argv[1] == "build" and len(sys.argv) > 2 and sys.argv[2] not in VARIANTS):
        sys.exit(__doc__)
    sys.exit(test() if sys.argv[1] == "test" else build(sys.argv[2] if len(sys.argv) > 2 else "dev"))
