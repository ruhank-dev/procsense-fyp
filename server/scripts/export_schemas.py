"""Writes content/schema/session_log.schema.json from the server's own session-file models, so the published schema and the code that enforces it cannot differ (p1-backend-s02).

    uv run --project server python -m server.scripts.export_schemas            # write
    uv run --project server python -m server.scripts.export_schemas --check    # fail if the file is out of date (also tested in server/tests)

The schema is what the engine's session file must look like (header and one entry per logged action). The tools tests validate a file the engine really wrote against it with an independent validator.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

from server.sessionfile import Entry, Header

TARGET = Path(__file__).resolve().parents[2] / "content" / "schema" / "session_log.schema.json"


def build() -> dict:
    return {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://procsense.local/schema/session_log.schema.json",
        "title": "ProcSense session file",
        "description": "The first line of a session file is {\"header\": <header>}; every other line is one <entry>, one per logged action, with seq counting up from 0 and step never going back. "
                       "header.log_hash is the SHA-256 of all the lines after the first. Header version 2 (the session id, content version, engine version and scenario package hash) is what the service requires. "
                       "Generated from server/sessionfile.py by server/scripts/export_schemas.py; do not edit by hand.",
        "$defs": {"header": Header.model_json_schema(), "entry": Entry.model_json_schema()},
    }


def text() -> str:
    return json.dumps(build(), indent=2, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    if "--check" in argv:
        current = TARGET.read_text(encoding="utf-8").replace("\r\n", "\n") if TARGET.exists() else ""
        if current != text():
            print("content/schema/session_log.schema.json is out of date: run `uv run --project server python -m server.scripts.export_schemas`")
            return 1
        print("content/schema/session_log.schema.json is up to date")
        return 0
    TARGET.write_text(text(), encoding="utf-8", newline="\n")
    print(f"wrote {TARGET}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
