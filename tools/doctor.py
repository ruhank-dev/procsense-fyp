"""Prints every tool the project uses, the version it wants and the version found, and flags what is missing (p0-foundation-s04).

    task doctor            (or: python tools/doctor.py)

Required tools (exit code 1 if one is missing or too old): Git, .NET SDK 9, uv, Task.
Needed only for some work (shown as MISSING, never an error): Git LFS, Docker (backend tests and the service image),
Unity with Android Build Support (the VR app), Blender (equipment scripts), adb (installing on the Quest headset).
The wanted versions are read from the repository, never typed here: global.json (.NET), unity/ProjectSettings/ProjectVersion.txt (Unity),
tools/blender/VERSION (Blender). Standard library only: without uv it also runs as `python tools/doctor.py`.
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def run(*cmd: str) -> str:
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
        return (r.stdout or r.stderr).strip()
    except (OSError, subprocess.TimeoutExpired):
        return ""


def first_version(text: str) -> str:
    m = re.search(r"(\d+\.\d+(?:\.\d+)?)", text)
    return m.group(1) if m else ""


def major(v: str) -> int:
    return int(v.split(".")[0]) if v and v.split(".")[0].isdigit() else -1


def wanted_dotnet() -> str:
    return json.loads((ROOT / "global.json").read_text(encoding="utf-8"))["sdk"]["version"]


def wanted_unity() -> str:
    m = re.search(r"m_EditorVersion: (\S+)", (ROOT / "unity" / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"))
    return m.group(1) if m else "?"


def wanted_blender() -> str:
    return (ROOT / "tools" / "blender" / "VERSION").read_text(encoding="utf-8").strip()


def find_unity(version: str) -> Path | None:
    env = os.environ.get("UNITY_EXE")
    if env and Path(env).exists():
        return Path(env)
    for root in (Path("C:/Program Files/Unity/Hub/Editor"), Path.home() / "Unity/Hub/Editor", Path("/Applications/Unity/Hub/Editor")):
        for exe in (root / version / "Editor" / "Unity.exe", root / version / "Unity.app" / "Contents" / "MacOS" / "Unity", root / version / "Editor" / "Unity"):
            if exe.exists():
                return exe
    return None


def find_blender(version: str) -> Path | None:
    short = ".".join(version.split(".")[:2])
    for c in (os.environ.get("BLENDER_EXE"), shutil.which("blender"),
              ROOT / "build" / "blender" / f"blender-{version}-windows-x64" / "blender.exe",
              Path(f"C:/Program Files/Blender Foundation/Blender {short}/blender.exe")):
        if c and Path(c).exists():
            return Path(c)
    return None


def check() -> tuple[list[tuple[str, str, str, str, str, str]], bool]:
    """Rows of (status, tool, needed for, wanted, found, how to fix) and whether every required tool is there."""
    rows, ok = [], True

    def add(status, tool, need, want, found, fix, required):
        nonlocal ok
        rows.append((status, tool, need, want, found, fix if status != "OK" else ""))
        if required and status != "OK":
            ok = False

    git = first_version(run("git", "--version")) if shutil.which("git") else ""
    add("OK" if git else "MISSING", "Git", "everything", "2.40+", git or "-", "winget install --id Git.Git -e", True)

    dn_want = wanted_dotnet()
    dn = first_version(run("dotnet", "--version")) if shutil.which("dotnet") else ""
    add("OK" if dn and major(dn) >= major(dn_want) else ("OLD" if dn else "MISSING"), ".NET SDK", "engine, CLI, tests",
        f"{major(dn_want)}.x (global.json {dn_want})", dn or "-", "winget install --id Microsoft.DotNet.SDK.9 -e", True)

    uv = first_version(run("uv", "--version")) if shutil.which("uv") else ""
    add("OK" if uv else "MISSING", "uv", "Python tools, backend", "0.5+", uv or "-", "winget install --id astral-sh.uv -e", True)

    task = first_version(run("task", "--version")) if shutil.which("task") else ""
    add("OK" if task else "MISSING", "Task", "task commands", "3.x", task or "-", "winget install --id Task.Task -e", True)

    lfs = first_version(run("git", "lfs", "version")) if git else ""
    add("OK" if lfs else "MISSING", "Git LFS", "large binary assets", "3.x", lfs or "-", "comes with Git for Windows; then: git lfs install", False)

    docker = first_version(run("docker", "--version")) if shutil.which("docker") else ""
    running = bool(docker) and bool(run("docker", "info", "--format", "{{.ServerVersion}}"))
    add("OK" if running else ("STOPPED" if docker else "MISSING"), "Docker", "backend tests, service image, backup drill", "any",
        (docker + ("" if running else " (engine not running)")) if docker else "-",
        "start Docker Desktop" if docker else "winget install --id Docker.DockerDesktop -e", False)

    u_want = wanted_unity()
    unity = find_unity(u_want)
    android = bool(unity) and (unity.parent / "Data" / "PlaybackEngines" / "AndroidPlayer").exists()
    add("OK" if unity else "MISSING", "Unity", "the VR app", u_want, str(unity.parent.parent.name) if unity else "-",
        f"Unity Hub > Installs > {u_want} (or set UNITY_EXE)", False)
    if unity:
        add("OK" if android else "MISSING", "Unity Android Build Support", "Quest builds", f"module of {u_want}", "installed" if android else "-",
            f"Unity Hub > Installs > {u_want} > Add modules > Android Build Support (with OpenJDK and SDK/NDK)", False)

    b_want = wanted_blender()
    blender = find_blender(b_want)
    b_found = first_version(run(str(blender), "--version")) if blender else ""
    add("OK" if b_found == b_want else ("OTHER" if blender else "MISSING"), "Blender", "equipment scripts", b_want, b_found or "-",
        f"install Blender {b_want} or set BLENDER_EXE (tools/blender/README.md)", False)

    adb = first_version(run("adb", "version")) if shutil.which("adb") else ""
    devices = [l.split()[0] for l in run("adb", "devices").splitlines()[1:] if l.strip().endswith("device")] if adb else []
    add("OK" if adb else "MISSING", "adb", "installing on the Quest", "any", (adb + (f", {len(devices)} headset(s) connected" if devices else ", no headset connected")) if adb else "-",
        "winget install --id Google.PlatformTools -e; headset: Meta Horizon app > Devices > Developer mode on", False)

    hooks = run("git", "-C", str(ROOT), "config", "--get", "core.hooksPath")
    add("OK" if hooks == ".githooks" else "MISSING", "pre-push hook", "pushing to main", ".githooks", hooks or "-", "task setup", True)
    return rows, ok


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    rows, ok = check()
    widths = [max(len(r[i]) for r in rows + [("STATUS", "TOOL", "NEEDED FOR", "WANTED", "FOUND", "")]) for i in range(5)]
    head = ("STATUS", "TOOL", "NEEDED FOR", "WANTED", "FOUND")
    print("  ".join(h.ljust(w) for h, w in zip(head, widths)))
    for r in rows:
        print("  ".join(c.ljust(w) for c, w in zip(r[:5], widths)) + (f"  -> {r[5]}" if r[5] else ""))
    print("\nrequired tools: " + ("all present" if ok else "something is missing (see above); open a new terminal after installing"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
