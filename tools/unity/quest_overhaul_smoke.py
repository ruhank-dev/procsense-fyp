"""Quest 2 GPU overhaul: targeted smoke tests and the final build sequence (owner decisions of 14 Nov 2026).

  uv run --project tools tools/unity/quest_overhaul_smoke.py smoke [--target Standalone|Android]
      The overhaul checks only: EditMode QuestOverhaulSceneTests (baked light only, no realtime shadow casters, MSAA off, reduced eye buffer,
      lightmaps and probes after the bake, no missing scripts) and the PlayMode smoke set (Boot loads clean, a door opens and closes, the quest profile is aggressive,
      no skinned mesh skins to NaN at two bones, modeled doors, exterior visibility). One Unity at a time: it holds D:/ProcSenseWork/unity.lock for the whole run.
  uv run --project tools tools/unity/quest_overhaul_smoke.py final [--wait]
      Compile and the smoke set, then the production APK (demo) and ONE diagnostic APK (diag); each is verified (zip test, aapt badging, size, SHA-256) and copied with its
      checksum into D:/ProcSenseWork/apk/overhaul. --wait polls for the flag file D:/ProcSenseWork/overhaul_ready.flag, which the coordinator creates when the lighting bake and every
      lane's work are done (a build must never start before the bake has finished).

Background only (no window); temporary and Gradle files go to D:/ProcSenseWork; the lock file is removed even when a step fails.
"""
from __future__ import annotations

import hashlib
import os
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import run_unity  # noqa: E402  (the project's own Unity launcher: find_unity, run, build, OUT)

WORK = Path(os.environ.get("PROCSENSE_WORK", "D:/ProcSenseWork"))
LOCK = WORK / "unity.lock"
FLAG = WORK / "overhaul_ready.flag"
APK_OUT = WORK / "apk" / "overhaul"
EDIT_FILTER = "QuestOverhaulSceneTests"
PLAY_FILTER = ";".join(["QuestOverhaulSmokeTests", "SkinWeightTests", "ModeledDoorTests", "ExteriorVisibilityTests", "CharacterPerfCullTests", "QuestRuntimeSettingsTests"])


def acquire() -> None:
    WORK.mkdir(parents=True, exist_ok=True)
    try:
        fd = os.open(LOCK, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        sys.exit(f"another Unity job holds {LOCK} ({LOCK.read_text(errors='ignore').strip()}); wait for it, never run two Editors")
    os.write(fd, b"agent-G overhaul")
    os.close(fd)


def release() -> None:
    LOCK.unlink(missing_ok=True)


def tests(platform: str, filt: str, target: str) -> tuple[int, int]:
    """One test launch; returns (passed, failed). A missing result file counts as a failure (never a stale XML)."""
    result = run_unity.OUT / f"overhaul_{platform}.xml"
    result.unlink(missing_ok=True)
    args = ["-runTests", "-testPlatform", platform, "-testFilter", filt, "-testResults", str(result)]
    if target:
        args = ["-buildTarget", target] + args
    run_unity.run(args, run_unity.OUT / f"overhaul_{platform}.log")
    if not result.exists():
        print(f"{platform}: no result file, see overhaul_{platform}.log")
        return 0, 1
    root = ET.parse(result).getroot()
    passed, failed = int(root.get("passed", "0")), int(root.get("failed", "1"))
    print(f"{platform}: {passed} passed, {failed} failed")
    for case in root.iter("test-case"):
        if case.get("result") == "Failed":
            msg = case.find(".//message")
            print("  FAIL", case.get("fullname"), "-", ((msg.text or "") if msg is not None else "").strip().splitlines()[:1])
    return passed, failed


def smoke(target: str) -> int:
    failed = 0
    for platform, filt in (("EditMode", EDIT_FILTER), ("PlayMode", PLAY_FILTER)):
        failed += tests(platform, filt, target)[1]
    return 1 if failed else 0


def aapt() -> Path | None:
    editor = run_unity.find_unity().parent
    hits = sorted((editor / "Data/PlaybackEngines/AndroidPlayer/SDK/build-tools").glob("*/aapt.exe"))
    return hits[-1] if hits else None


def verify_apk(apk: Path, variant: str) -> dict:
    if not apk.exists():
        sys.exit(f"{variant}: APK not produced")
    with zipfile.ZipFile(apk) as z:
        if z.testzip() is not None:
            sys.exit(f"{variant}: APK zip is corrupt")
        names = z.namelist()
    for need in ("AndroidManifest.xml", "lib/arm64-v8a/libil2cpp.so", "lib/arm64-v8a/libUnityOpenXR.so"):
        if need not in names:
            sys.exit(f"{variant}: {need} is missing from the APK")
    badging = ""
    tool = aapt()
    if tool:
        badging = subprocess.run([str(tool), "dump", "badging", str(apk)], capture_output=True, text=True).stdout
        if "package: name='com.procsense.fyp'" not in badging:
            sys.exit(f"{variant}: unexpected package name")
    sha = hashlib.sha256(apk.read_bytes()).hexdigest()
    APK_OUT.mkdir(parents=True, exist_ok=True)
    dest = APK_OUT / apk.name
    shutil.copy2(apk, dest)
    (APK_OUT / (apk.name + ".sha256")).write_text(f"{sha}  {apk.name}\n")
    info = {"variant": variant, "apk": str(dest), "bytes": apk.stat().st_size, "sha256": sha, "entries": len(names)}
    print(info)
    return info


def final(wait: bool) -> int:
    if wait:
        print(f"waiting for {FLAG} (created by the coordinator when the bake and all lanes are done)")
        while not FLAG.exists():
            time.sleep(60)
    elif not FLAG.exists():
        sys.exit(f"{FLAG} does not exist: the lighting bake and the other lanes are not marked done, so no build starts")
    acquire()
    try:
        if smoke("") != 0:
            print("smoke tests failed: no APK is built from a failing tree")
            return 1
        for variant in ("demo", "diag"):
            code = run_unity.build(variant)
            if code != 0:
                print(f"{variant} build failed (exit {code}), see build-{variant}.log")
                return 1
            verify_apk(run_unity.OUT / f"ProcSense-{variant}.apk", variant)
        dirty = subprocess.run(["git", "status", "--porcelain", "--untracked-files=no"], cwd=ROOT, capture_output=True, text=True).stdout.strip()
        print("working tree after the builds:", "clean" if not dirty else "MODIFIED (a build rewrote tracked files, review and commit them):\n" + dirty)
        return 0
    finally:
        release()


def main(argv: list[str]) -> int:
    if len(argv) < 2 or argv[1] not in ("smoke", "final"):
        print(__doc__)
        return 2
    if argv[1] == "final":
        return final("--wait" in argv)
    target = argv[argv.index("--target") + 1] if "--target" in argv else ""
    target = {"Standalone": "StandaloneWindows64", "": ""}.get(target, target)
    acquire()
    try:
        return smoke(target)
    finally:
        release()


if __name__ == "__main__":
    sys.exit(main(sys.argv))
