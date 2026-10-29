"""Pull the session logs off the Quest and replay each one headless (p0-evaluation-performance-s01, EXP-CORE-1: the headset and the PC engine agree).

    uv run --project tools tools/unity/quest_sessions.py pull [--dest artifacts/quest-sessions]      # adb pull the app's sessions folder
    uv run --project tools tools/unity/quest_sessions.py replay [--dir artifacts/quest-sessions] [--engine <commit>] [--out table.md]

A log is replayed with the engine it was recorded by: the header's content version names the content, and the replay is run in a temporary checkout of the newest
commit with that content (or the commit given with --engine), so later engine changes do not show up as disagreements. The table lists each log's verdict.
Standard library only; needs adb (Android platform tools) for pull and git and dotnet for replay.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGE = "com.procsense.fyp"
REMOTE = f"/sdcard/Android/data/{PACKAGE}/files/sessions"


def run(cmd: list[str], cwd: Path = ROOT) -> tuple[int, str]:
    env = dict(os.environ, MSYS_NO_PATHCONV="1")
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
    return p.returncode, (p.stdout or "") + (p.stderr or "")


def header(path: Path) -> dict:
    with path.open(encoding="utf-8") as f:
        return json.loads(f.readline())["header"]


def commit_for(content_version: str) -> str | None:
    """The newest commit whose content manifest hash starts with this version."""
    code, out = run(["git", "log", "--format=%H", "--", "content/manifest.json"])
    if code != 0:
        return None
    for c in out.split():
        code, text = run(["git", "show", f"{c}:content/manifest.json"])
        if code == 0 and json.loads(text).get("content_hash", "").startswith(content_version):
            return c
    return None


def replay_all(logs: list[Path], engine: str | None) -> list[tuple[str, str, str]]:
    """[(file, commit, verdict)]"""
    by_commit: dict[str, list[Path]] = {}
    rows = []
    for log in logs:
        c = engine or commit_for(header(log).get("content_version", ""))
        if c is None:
            rows.append((log.name, "-", "no commit has this content version"))
            continue
        by_commit.setdefault(c, []).append(log)
    for c, group in by_commit.items():
        tmp = Path(tempfile.mkdtemp(prefix="replay-"))
        try:
            run(["git", "worktree", "add", "--detach", str(tmp), c])
            code, out = run(["dotnet", "build", "src/ProcSense.Cli", "-c", "Release"], cwd=tmp)
            if code != 0:
                rows += [(g.name, c[:8], "engine did not build") for g in group]
                continue
            for g in group:
                _, out = run(["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--", "replay", "--log", str(g.resolve())], cwd=tmp)
                last = out.strip().splitlines()[-1] if out.strip() else "no output"
                rows.append((g.name, c[:8], last))
        finally:
            run(["dotnet", "build-server", "shutdown"], cwd=tmp)
            run(["git", "worktree", "remove", "--force", str(tmp)])
            shutil.rmtree(tmp, ignore_errors=True)
    return sorted(rows)


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("what", choices=["pull", "replay"])
    ap.add_argument("--dest", type=Path, default=ROOT / "artifacts" / "quest-sessions")
    ap.add_argument("--dir", type=Path, default=ROOT / "artifacts" / "quest-sessions")
    ap.add_argument("--engine")
    ap.add_argument("--out", type=Path)
    a = ap.parse_args(argv)
    if a.what == "pull":
        a.dest.mkdir(parents=True, exist_ok=True)
        code, out = run(["adb", "pull", REMOTE + "/.", str(a.dest)])
        print(out.strip())
        return code
    logs = sorted(a.dir.glob("*.jsonl"))
    if not logs:
        print(f"no session logs in {a.dir}")
        return 1
    rows = replay_all(logs, a.engine)
    table = "| Log | Engine | Replay |\n|---|---|---|\n" + "".join(f"| {f} | {c} | {v} |\n" for f, c, v in rows)
    ok = sum("hash match" in v for _, _, v in rows)
    table += f"\n{ok} of {len(rows)} logs replay to the hash the headset wrote.\n"
    if a.out:
        a.out.write_text(table, encoding="utf-8", newline="\n")
    print(table)
    return 0 if ok == len(rows) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
