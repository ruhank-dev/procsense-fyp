"""P3-SES-04: every action of the app reaches the engine through the action gateway.

    uv run --project tools tools/ci/check_action_gateway.py

Fails when a Unity script (unity/Assets/Scripts) calls SimulationHost.Apply directly: <name>.Apply(new SimAction ...), host.Apply(...), _host.Apply(...),
Host.Apply(...) or SimulationHost.Instance.Apply(...). Only the gateway itself (Bridge/ActionGateway.cs) and the host (Bridge/SimulationHost.cs) may. A session's own
Session.Apply (an offline copy, the replay, Core) is not a host call and is allowed. Tests may call the host directly.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "unity" / "Assets" / "Scripts"
ALLOWED = {"Bridge/ActionGateway.cs", "Bridge/SimulationHost.cs"}
DIRECT = re.compile(r"(\bSimulationHost\.Instance|\b_?[Hh]ost)\??\.Apply\(")


def problems() -> list[str]:
    found = []
    for path in sorted(SCRIPTS.rglob("*.cs")):
        rel = path.relative_to(SCRIPTS).as_posix()
        if rel in ALLOWED:
            continue
        for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            code = line.split("//", 1)[0]
            if DIRECT.search(code):
                found.append(f"{rel}:{n}: direct SimulationHost.Apply; send it through ActionGateway.Apply(host, action, source)")
    return found


def main() -> int:
    p = problems()
    for x in p:
        print("FAIL  " + x)
    if not p:
        print("PASS  every app action goes through the action gateway")
    return 1 if p else 0


if __name__ == "__main__":
    sys.exit(main())
