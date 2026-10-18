"""What GitHub Actions did for the latest commits, kept apart from what the local CI did.

    uv run --project tools tools/ci/remote_status.py [--limit 12]

A workflow run is classified as one of
  passed         every job ran and succeeded
  failed         at least one job ran and failed
  never_started  GitHub refused to start any job (no steps ran): the account's billing or spending limit, not the code
  skipped        the workflow's own condition skipped it
  other          running, cancelled, or anything else
A "never_started" run says nothing about the code, so it is never reported as a pass or as a failure of the code.
The local record (.local-ci/last-run.json, written by `task ci`) is printed separately.
"""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def classify(conclusion: str, jobs: list[dict]) -> str:
    if conclusion == "skipped":
        return "skipped"
    if conclusion == "success" and jobs and all(j.get("conclusion") in ("success", "skipped") for j in jobs):
        return "passed"
    if conclusion == "failure":
        if not jobs or all(len(j.get("steps") or []) == 0 for j in jobs):
            return "never_started"
        return "failed"
    return "other"


def _gh(args: list[str]):
    p = subprocess.run(["gh", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return json.loads(p.stdout) if p.returncode == 0 and p.stdout.strip() else None


def remote(limit: int) -> list[dict]:
    runs = _gh(["run", "list", "--limit", str(limit), "--json", "databaseId,name,conclusion,headSha,event,createdAt"])
    if runs is None:
        return []
    out = []
    for r in runs:
        info = _gh(["run", "view", str(r["databaseId"]), "--json", "jobs"]) or {"jobs": []}
        out.append({**r, "state": classify(r["conclusion"], info["jobs"])})
    return out


def local() -> dict | None:
    f = ROOT / ".local-ci" / "last-run.json"
    return json.loads(f.read_text(encoding="utf-8")) if f.exists() else None


def main(argv: list[str]) -> int:
    limit = int(argv[argv.index("--limit") + 1]) if "--limit" in argv else 12
    rec = local()
    print("LOCAL (task ci):", f"{rec['passed']}/{rec['total']} passed on {rec['commit'][:7]}{' (+uncommitted changes)' if rec['dirty'] else ''} at {rec['utc']}" if rec else "no local record on this machine")
    runs = remote(limit)
    if not runs:
        print("REMOTE (GitHub Actions): could not be read (gh missing, not signed in or offline)")
        return 0
    print("REMOTE (GitHub Actions):")
    for r in runs:
        print(f"  {r['state']:<14} {r['name'][:44]:<44} {r['headSha'][:7]} {r['event']:<12} {r['createdAt']}")
    if any(r["state"] == "never_started" for r in runs):
        print("  never_started = GitHub did not run any job (billing/spending limit). That is not a pass and not a code failure; the local result above is the validation.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
