"""Verifies a developer machine and clone are ready: tools installed, git hook installed. Standard library only.

Run by `task setup` (after it installs the hook). Exit code 1 and an actionable message if anything is missing.
"""
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.stdout.reconfigure(encoding="utf-8")

INSTALL = {
    "git": "winget install --id Git.Git -e",
    "task": "winget install --id Task.Task -e",
    "uv": "winget install --id astral-sh.uv -e",
    "dotnet": "winget install --id Microsoft.DotNet.SDK.9 -e",
}


def out(*cmd: str) -> str:
    return subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()


def main() -> int:
    problems = []
    for tool, install in INSTALL.items():
        if shutil.which(tool):
            print(f"ok       {tool}")
        else:
            print(f"MISSING  {tool}   ->  {install}   (then restart the terminal)")
            problems.append(tool)
    if shutil.which("dotnet"):
        match = re.match(r"(\d+)\.", out("dotnet", "--version"))
        if not match or int(match.group(1)) < 9:
            print("TOO OLD  dotnet needs SDK 9 or newer (global.json)   ->  " + INSTALL["dotnet"])
            problems.append("dotnet")

    hooks = out("git", "config", "--get", "core.hooksPath")
    hook = ROOT / ".githooks" / "pre-push"
    if hooks != ".githooks":
        print("MISSING  git hook   ->  run: git config core.hooksPath .githooks   (task setup does this)")
        problems.append("hook")
    elif not hook.exists() or "task ci" not in hook.read_text(encoding="utf-8", errors="replace"):
        print("BROKEN   .githooks/pre-push is missing or does not run 'task ci'   ->  git pull, then run task setup again")
        problems.append("hook")
    else:
        print("ok       pre-push hook installed (core.hooksPath = .githooks)")

    print(f"\nbranch : {out('git', 'branch', '--show-current') or '(detached)'}")
    print(f"remote : {out('git', 'remote', 'get-url', 'origin')}")
    print(f"tree   : {'clean' if not out('git', 'status', '--porcelain') else 'has uncommitted changes (git status)'}")
    if problems:
        print(f"\nsetup incomplete: {', '.join(problems)}")
        return 1
    print("\nsetup complete. Next: task ci")
    return 0


if __name__ == "__main__":
    sys.exit(main())
