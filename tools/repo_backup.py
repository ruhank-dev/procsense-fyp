"""A copy of the whole repository in one file, outside the repository (p0-foundation-s01).

    task repo-backup                      (default folder: <home>/Desktop/ProcSense_branch_backup)
    task repo-backup -- --out D:/backups --keep 10

Fetches from GitHub first (so the copy holds every branch and tag that is on GitHub, plus this clone's own branches), writes
procsense-repo-<UTC time>.bundle with `git bundle create --all`, checks it with `git bundle verify`, and keeps the newest --keep files.
To get the project back from a bundle: git clone <file>.bundle procsense-fyp   (then: git remote set-url origin <the GitHub address>).
Standard library only. The database has its own nightly backup (server/RUNBOOK.md, Backup and restore).
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUT = Path.home() / "Desktop" / "ProcSense_branch_backup"


def git(*args: str, cwd: Path = ROOT) -> str:
    r = subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {(r.stderr or r.stdout).strip()}")
    return r.stdout.strip()


def backup(out: Path, keep: int, fetch: bool = True, repo: Path = ROOT, now: datetime | None = None) -> tuple[Path, int]:
    if out.resolve().is_relative_to(repo.resolve()):
        raise RuntimeError("the backup folder must be outside the repository (a copy inside it would be lost with it)")
    if fetch and git("remote", cwd=repo):
        git("fetch", "--all", "--prune", "--tags", cwd=repo)
    out.mkdir(parents=True, exist_ok=True)
    stamp = (now or datetime.now(timezone.utc)).strftime("%Y%m%d-%H%M%S")
    target = out / f"procsense-repo-{stamp}.bundle"
    git("bundle", "create", str(target), "--all", cwd=repo)
    git("bundle", "verify", str(target), cwd=repo)
    refs = len([line for line in git("bundle", "list-heads", str(target), cwd=repo).splitlines() if line.strip()])
    for old in sorted(out.glob("procsense-repo-*.bundle"))[:-keep]:
        old.unlink()
    return target, refs


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--out", type=Path, default=DEFAULT_OUT, help="folder outside the repository")
    p.add_argument("--keep", type=int, default=5, help="how many backup files to keep (oldest are deleted)")
    p.add_argument("--no-fetch", action="store_true", help="do not fetch from GitHub first (offline)")
    a = p.parse_args(argv)
    try:
        target, refs = backup(a.out, max(1, a.keep), fetch=not a.no_fetch)
    except RuntimeError as e:
        print(f"repo backup failed: {e}")
        return 1
    print(f"repo backup written and verified: {target} ({target.stat().st_size / 1_000_000:.1f} MB, {refs} branches and tags)")
    print(f"restore: git clone \"{target}\" procsense-fyp")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
