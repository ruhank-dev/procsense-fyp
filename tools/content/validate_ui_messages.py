"""Validator for content/ui/ui_messages.json (P3-API-07): the player-facing message for every sign-in, LAN, pairing and compatibility error code.

    uv run --project tools tools/content/validate_ui_messages.py

The codes are read from the server's own source (the `detail` words of the pairing, sign-in, LAN guard, headset notice and compatibility code), so a new error code without a message fails here. Every code needs one
entry with a known group, a title, a message and an action; no code may appear twice or be unknown to both the server and the client list; no dose or measurement appears. Exits 1 with the list of problems.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TABLE = ROOT / "content" / "ui" / "ui_messages.json"
SERVER = ROOT / "server"
SOURCES = ["routers/pairing.py", "routers/device_auth.py", "auth/dependencies.py", "middleware/lan_pairing.py", "lan/compat.py", "lan/notices.py"]
GROUPS = {"sign_in", "lan", "pairing", "compatibility"}
MEASURE = re.compile(r"\d+(\.\d+)?\s*(mg|mcg|ml|mmol|mmhg|kg|%|bpm|units?)\b", re.I)
PATTERNS = [
    re.compile(r'detail="([a-z_]+)"'),                    # raise HTTPException(..., detail="wrong_code")
    re.compile(r'reason = \d+, "([a-z_]+)"'),             # the LAN guard: status, reason = 401, "pairing_required"
    re.compile(r'return f"([a-z_]+):'),                   # compat.check: "engine_version_unsupported:<version>"
]
OAUTH = re.compile(r'\.get\(outcome, "([a-z_]+)"\)')      # the headset sign-in poll: {...: "authorization_pending", ...}.get(outcome, "expired_token")


def server_codes(root: Path = SERVER) -> set[str]:
    codes: set[str] = set()
    for rel in SOURCES:
        path = root / rel
        if not path.exists():
            continue
        for line in path.read_text(encoding="utf-8").splitlines():
            for p in PATTERNS:
                codes.update(p.findall(line))
            if OAUTH.search(line):
                codes.update(re.findall(r':\s*"([a-z_]+)"', line))
                codes.update(OAUTH.findall(line))
    return codes


def problems(table: dict | None = None, codes: set[str] | None = None) -> list[str]:
    t = table if table is not None else json.loads(TABLE.read_text(encoding="utf-8"))
    need = server_codes() if codes is None else codes
    p: list[str] = []
    if set(t.get("groups", [])) != GROUPS:
        p.append(f"ui_messages: groups must be {sorted(GROUPS)}")
    client = set(t.get("client_codes", []))
    listed = [m.get("code", "") for m in t.get("messages", [])]
    for c in sorted({x for x in listed if listed.count(x) > 1}):
        p.append(f"ui_messages: {c} appears twice")
    for c in sorted((need | client) - set(listed)):
        p.append(f"ui_messages: no message for code {c}")
    for c in sorted(set(listed) - need - client):
        p.append(f"ui_messages: {c} is neither a server code nor a client code")
    for m in t.get("messages", []):
        where = "ui_messages." + m.get("code", "?")
        if m.get("group") not in GROUPS:
            p.append(f"{where}: unknown group {m.get('group')!r}")
        for key in ("title", "message", "action"):
            if not str(m.get(key, "")).strip():
                p.append(f"{where}: {key} is empty")
        if MEASURE.search(json.dumps(m, ensure_ascii=False)):
            p.append(f"{where}: contains a dose or measurement")
    return p


def main() -> int:
    probs = problems()
    print("ui messages: " + ("all checks pass" if not probs else f"{len(probs)} problem(s)"))
    for x in probs:
        print("  - " + x)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main())
