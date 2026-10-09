"""Run every check CI runs, on your machine, in the same order. Standard library only.

Usage (from repo root):  task ci     or     python tools/ci/run_all.py [--quick]
--quick skips the slow steps (build/test) when only docs or content changed.
Keep this list in step with .github/workflows/ci.yml and docs/CI.md (INSTRUCTIONS.md section 6b).
"""
import os
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.stdout.reconfigure(encoding="utf-8")
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def run(cmd: list[str], fail_if_in_output: str | None = None) -> tuple[bool, str]:
    p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (p.stdout or "") + (p.stderr or "")
    ok = p.returncode == 0 and not (fail_if_in_output and fail_if_in_output in out)
    return ok, out


def replay_round_trip() -> tuple[bool, str]:
    with tempfile.TemporaryDirectory() as tmp:
        out = str(Path(tmp) / "run.json")
        ok, text = run(CLI + ["run", "--case", "VAR-S1-A", "--seed", "42", "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", out])
        if not ok:
            return False, text
        return run(CLI + ["replay", "--log", str(Path(tmp) / "run.session.jsonl")])


def main(argv: list[str]) -> int:
    quick = "--quick" in argv
    steps: list[tuple[str, callable]] = []
    if not quick:
        steps += [
            ("build (warnings are errors)", lambda: run(["dotnet", "build", "ProcSense.sln", "-c", "Release", "-warnaserror"])),
            ("unit tests", lambda: run(["dotnet", "test", "ProcSense.sln", "--no-build", "-c", "Release"])),
            ("golden runs", lambda: run(CLI + ["golden", "--dir", "tests/golden"])),
            ("replay round trip", replay_round_trip),
        ]
        if os.name != "nt":  # Windows checkouts use CRLF; CI (Linux, LF) is the authority for formatting
            steps.append(("code format", lambda: run(["dotnet", "format", "ProcSense.sln", "--verify-no-changes"])))
        steps.append(("no vulnerable packages", lambda: run(["dotnet", "list", "ProcSense.sln", "package", "--vulnerable", "--include-transitive"],
                                                          fail_if_in_output="has the following vulnerable packages")))
    steps += [
        ("content + source register", lambda: run(["uv", "run", "--project", "tools", "tools/content/validate.py"])),
        ("plan index up to date", lambda: run(["uv", "run", "--project", "tools", "tools/plans/milestones.py", "--check"])),
        ("project rules", lambda: run(["uv", "run", "--project", "tools", "tools/ci/repo_checks.py"])),
    ]
    results = []
    for name, step in steps:
        start = time.time()
        ok, out = step()
        results.append((name, ok))
        print(f"{'PASS' if ok else 'FAIL'}  {name}  ({time.time() - start:.0f}s)")
        if not ok:
            print("\n".join("        " + line for line in out.strip().splitlines()[-25:]))
    failed = [n for n, ok in results if not ok]
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed" + (f"; failed: {', '.join(failed)}" if failed else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
