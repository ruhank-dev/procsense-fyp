"""Run one Unity batch check safely: one Unity at a time (D:\\ProcSenseWork\\unity.lock), hidden window, temp and Gradle on the big drive.

    python tools/ci/unity_check.py compile
    python tools/ci/unity_check.py tests --platform PlayMode [--filter "A;B"] [--env KEY=VALUE ...] [--frames-dir NAME]

Exit 0 only when Unity ran and every selected test passed (a missing result file is a failure, never a pass). Never steals focus.
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "unity"))
import run_unity  # noqa: E402  (find_unity, PROJECT)

WORK = Path(os.environ.get("PROCSENSE_WORK") or ("D:/ProcSenseWork" if os.name == "nt" and Path("D:/").exists() else str(Path.home() / "ProcSenseWork")))
LOCK = WORK / "unity.lock"


def take_lock(owner: str, wait_minutes: int) -> None:
    WORK.mkdir(parents=True, exist_ok=True)
    until = time.time() + wait_minutes * 60
    while True:
        try:
            fd = os.open(LOCK, os.O_CREAT | os.O_EXCL | os.O_WRONLY)   # atomic: exactly one holder
            os.write(fd, owner.encode()); os.close(fd)
            return
        except FileExistsError:
            if time.time() > until:
                sys.exit(f"unity.lock held by '{LOCK.read_text(errors='replace').strip()}' for {wait_minutes} min; gave up (nothing was run)")
            time.sleep(15)


def release_lock() -> None:
    try:
        LOCK.unlink()
    except OSError:
        pass


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["compile", "tests"])
    ap.add_argument("--platform", default="PlayMode"); ap.add_argument("--filter", default="")
    ap.add_argument("--env", action="append", default=[]); ap.add_argument("--frames-dir", default="")
    ap.add_argument("--wait-minutes", type=int, default=240)
    a = ap.parse_args(argv)
    tag = f"ci_{a.mode}_{a.platform if a.mode == 'tests' else 'compile'}"
    out = run_unity.PROJECT / "Logs" / "ci"; out.mkdir(parents=True, exist_ok=True)
    log, result = out / f"{tag}.log", out / f"{tag}.xml"
    result.unlink(missing_ok=True)   # a stale result must never be read as this run's outcome
    env = dict(os.environ)
    for sub in ("tmp", "gradle"):
        (WORK / sub).mkdir(parents=True, exist_ok=True)
    env.update(TEMP=str(WORK / "tmp"), TMP=str(WORK / "tmp"), GRADLE_USER_HOME=str(WORK / "gradle"))
    for kv in a.env:
        k, _, v = kv.partition("="); env[k] = v
    if a.frames_dir:
        d = WORK / "frames" / f"ci_{a.frames_dir}"; d.mkdir(parents=True, exist_ok=True); env["BENCH_CLIP_DIR"] = str(d)
    cmd = [str(run_unity.find_unity()), "-batchmode", "-projectPath", str(run_unity.PROJECT), "-logFile", str(log)]
    cmd += ["-quit", "-nographics"] if a.mode == "compile" else ["-runTests", "-testPlatform", a.platform, "-testResults", str(result)] + (["-testFilter", a.filter] if a.filter else [])
    take_lock(f"ci-{tag}", a.wait_minutes)
    try:
        code = subprocess.call(cmd, env=env, creationflags=0x08000000 if os.name == "nt" else 0)   # CREATE_NO_WINDOW
    finally:
        release_lock()
    if a.mode == "compile":
        print(f"unity compile: exit {code} (log {log})")
        return 0 if code == 0 else 1
    if not result.exists():
        print(f"no result file (exit {code}); see {log}")
        return 1
    root = ET.parse(result).getroot()
    passed, failed = int(root.get("passed", 0)), int(root.get("failed", 1))
    print(f"unity {a.platform} {a.filter or 'all'}: {passed} passed, {failed} failed (exit {code})")
    return 0 if failed == 0 and passed > 0 and code in (0, 2) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
