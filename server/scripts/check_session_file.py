"""Checks that a session file is one the service would read (p1-backend-s02): the contract between the engine and the server, usable on any file the engine wrote.

    uv run --project server python -m server.scripts.check_session_file path/to/file.session.jsonl [--v1]

--v1 accepts the header the engine writes today (without the version 2 fields); without it the file must be version 2 (the form the service requires, `p1-backend-s09`).
"""
from __future__ import annotations

import sys
from pathlib import Path

from server.sessionfile import SessionFileError, parse_session_file


def main(argv: list[str]) -> int:
    paths = [a for a in argv if not a.startswith("--")]
    if len(paths) != 1:
        print(__doc__)
        return 2
    try:
        parsed = parse_session_file(Path(paths[0]).read_text(encoding="utf-8"), require_v2="--v1" not in argv)
    except SessionFileError as e:
        print(f"REFUSED: {e.code} at line {e.line} {e.detail}".strip())
        return 1
    print(f"ok: {len(parsed.entries)} actions, case {parsed.header.case}, mode {parsed.header.mode}, header version {'2' if parsed.is_v2 else '1'}, log hash matches")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
