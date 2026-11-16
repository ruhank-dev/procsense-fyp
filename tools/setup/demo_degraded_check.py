"""The degraded demonstration check (P3-VOX-11, owner decision D2).

  uv run --project server tools/setup/demo_degraded_check.py              # the fallback tests (needs PostgreSQL: TEST_DATABASE_URL, or Docker for the throwaway one)
  uv run --project server tools/setup/demo_degraded_check.py --live       # also look at this laptop: is the cloud reachable, is the local server up

Cloud unreachable or out of quota: the laptop answers with the local fallbacks (grammar orders, no spoken action so the tablet stays, the approved patient line) and says why
(server/tests/test_demo_degraded.py). Laptop unreachable: the headset sees a refused connection and plays in menu mode
(server/tests/test_lan_integration.py::test_reconnect_after_server_restart_and_laptop_down). Run it once with the laptop's internet unplugged and once with its Wi-Fi off (plan p3-05)."""
from __future__ import annotations

import os
import subprocess
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TESTS = ["tests/test_demo_degraded.py", "tests/test_lan_integration.py::test_reconnect_after_server_restart_and_laptop_down"]   # relative to server/, where its pytest settings live
PORT = int(os.environ.get("PROCSENSE_PORT", "8000"))


def reachable(url: str, timeout: float = 3.0) -> bool:
    try:
        with urllib.request.urlopen(url, timeout=timeout):
            return True
    except Exception:            # any refusal, timeout or HTTP error means "not usable now"
        return False


def live_report() -> list[str]:
    cloud = reachable("https://ollama.com/api/tags")
    server = reachable(f"http://127.0.0.1:{PORT}/health")
    return [f"cloud (ollama.com): {'reachable' if cloud else 'NOT reachable: the laptop answers with the local fallbacks'}",
            f"laptop server on port {PORT}: {'up' if server else 'NOT running: the headset plays in menu mode'}"]


def test_env() -> dict[str, str] | None:
    env = dict(os.environ)
    if env.get("TEST_DATABASE_URL"):
        return env
    sys.path.insert(0, str(ROOT / "tools" / "ci"))
    import server_tests
    if server_tests.docker_ready() and server_tests.ensure_container():
        env["TEST_DATABASE_URL"] = server_tests.LOCAL_URL
        return env
    return None


def main(argv: list[str]) -> int:
    if "--live" in argv:
        for line in live_report():
            print(line)
    env = test_env()
    if env is None:
        print("FAIL  no PostgreSQL: set TEST_DATABASE_URL or start Docker (task server-db-up)")
        return 1
    env["REQUIRE_DB"] = "1"
    code = subprocess.call([sys.executable, "-m", "pytest", "-q", "-p", "no:cacheprovider", *TESTS], cwd=ROOT / "server", env=env)
    print("demo-degraded-check: " + ("PASS" if code == 0 else "FAIL"))
    return code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
