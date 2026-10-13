"""Runs the backend tests (server/tests) against a real PostgreSQL (p1-backend-s01).

    uv run --project tools tools/ci/server_tests.py          (also `task server-test`, and part of `task ci`)

Where the database comes from, in this order:
  1. TEST_DATABASE_URL, if it is set (CI sets it to the PostgreSQL service container and REQUIRE_DB=1).
  2. A throwaway PostgreSQL 17 in Docker (container `procsense-test-pg`, started if it is not running and left running for the next run; `task server-db-down` removes it).
  3. Neither: the tests that need a database are skipped and this says so loudly. That is allowed on a laptop without Docker and never in CI (REQUIRE_DB=1 turns it into a failure).
Standard library only.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTAINER = "procsense-test-pg"
LOCAL_URL = "postgresql://postgres:postgres@localhost:54329/procsense_test"


def docker(*args: str, check: bool = False) -> subprocess.CompletedProcess:
    return subprocess.run(["docker", *args], capture_output=True, text=True, check=check)


def docker_ready() -> bool:
    try:
        return docker("info").returncode == 0
    except FileNotFoundError:
        return False


def ensure_container() -> bool:
    """Starts the throwaway PostgreSQL if needed and waits until it accepts connections."""
    running = docker("ps", "-q", "-f", f"name={CONTAINER}").stdout.strip()
    if not running:
        docker("rm", "-f", CONTAINER)
        started = docker("run", "-d", "--name", CONTAINER, "-e", "POSTGRES_PASSWORD=postgres", "-e", "POSTGRES_DB=procsense_test", "-p", "54329:5432", "postgres:17")
        if started.returncode != 0:
            print("could not start PostgreSQL in Docker: " + started.stderr.strip()[-300:])
            return False
    for _ in range(60):
        if docker("exec", CONTAINER, "pg_isready", "-U", "postgres", "-d", "procsense_test").returncode == 0:
            time.sleep(1)  # the first start restarts once after initialising
            if docker("exec", CONTAINER, "pg_isready", "-U", "postgres", "-d", "procsense_test").returncode == 0:
                return True
        time.sleep(1)
    print("PostgreSQL in Docker did not become ready")
    return False


def main() -> int:
    if "--db-only" in sys.argv:
        if not (docker_ready() and ensure_container()):
            print("Docker is not running: start Docker Desktop first")
            return 1
        print("PostgreSQL is ready. Use:  TEST_DATABASE_URL=" + LOCAL_URL)
        return 0
    env = dict(os.environ)
    if not env.get("TEST_DATABASE_URL"):
        if docker_ready() and ensure_container():
            env["TEST_DATABASE_URL"] = LOCAL_URL
            env.setdefault("REQUIRE_DB", "1")
        else:
            if env.get("REQUIRE_DB") == "1":
                print("FAIL  no PostgreSQL for the backend tests and REQUIRE_DB=1")
                return 1
            print("NOTICE  no PostgreSQL available (no TEST_DATABASE_URL and no Docker): the database tests are SKIPPED. CI runs them; start Docker to run them here.")
    dll = ROOT / "artifacts" / "bin" / "ProcSense.Cli" / "Release" / "net9.0" / "procsense-sim.dll"
    if not env.get("ENGINE_CLI") and dll.exists():
        env["ENGINE_CLI"] = json.dumps(["dotnet", str(dll).replace("\\", "/")])   # the engine just built by this run: the verification tests use it as well as the stand-in
    if os.name == "nt" and not env.get("BROWSER_CHANNEL"):
        env["BROWSER_CHANNEL"] = "msedge"   # the web page test drives the Edge that Windows already has; elsewhere it uses Playwright's own Chromium or is skipped
    cmd = ["uv", "run", "--project", "server", "pytest", "server/tests", "-q", "-rs", "-p", "no:cacheprovider"]
    return subprocess.run(cmd, cwd=ROOT, env=env).returncode


if __name__ == "__main__":
    sys.exit(main())
