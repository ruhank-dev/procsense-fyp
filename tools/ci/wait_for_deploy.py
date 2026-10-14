"""Waits until the hosted service runs a given commit and its database answers (continuous delivery, p1-backend-s13). Standard library only.

    python tools/ci/wait_for_deploy.py --url https://procsense.onrender.com --sha <commit> [--timeout 900] [--interval 10]

A deploy takes a while: the host pulls the image, starts it, and a free service may also be asleep. So this keeps asking `/health` until its `git_sha` is the commit just deployed (a request that fails or still shows the old commit is
not an error yet), then asks `/health/db` once. It exits 0 when both are right, and 1 with a plain sentence when the time runs out or the database does not answer.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.request


def get_json(url: str, timeout: float = 30.0) -> tuple[int, dict]:
    """The status and JSON body of a GET; (0, {}) when nothing answered (offline, asleep, still starting)."""
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return response.status, json.loads(response.read().decode("utf-8") or "{}")
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read().decode("utf-8") or "{}")
        except ValueError:
            return e.code, {}
    except (urllib.error.URLError, OSError, ValueError):
        return 0, {}


def wait(url: str, sha: str, timeout: float = 900.0, interval: float = 10.0, say=print, sleep=time.sleep, clock=time.monotonic, fetch=get_json) -> tuple[bool, str]:
    base = url.rstrip("/")
    deadline = clock() + timeout
    seen = "nothing yet"
    while True:
        status, body = fetch(base + "/health")
        running = str(body.get("git_sha", "")) if status == 200 else ""
        if running and (running == sha or running.startswith(sha) or sha.startswith(running)):
            say(f"the service runs {running[:12]}")
            break
        seen = f"status {status}, running {running[:12] or 'unknown'}" if status else "no answer"
        say(f"waiting for {sha[:12]} ({seen})")
        if clock() + interval > deadline:
            return False, f"the service did not start running {sha[:12]} within {int(timeout)} seconds (last seen: {seen})"
        sleep(interval)
    status, body = fetch(base + "/health/db")
    if status == 200 and body.get("status") == "ok":
        return True, f"the service runs {sha[:12]} and its database answers ({body.get('latency_ms', '?')} ms)"
    return False, f"the service runs {sha[:12]} but its database does not answer (status {status}, {body.get('reason', 'no reason given')})"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--url", required=True)
    parser.add_argument("--sha", required=True)
    parser.add_argument("--timeout", type=float, default=900.0)
    parser.add_argument("--interval", type=float, default=10.0)
    args = parser.parse_args(argv)
    ok, message = wait(args.url, args.sha, args.timeout, args.interval)
    print(("OK: " if ok else "FAILED: ") + message)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
