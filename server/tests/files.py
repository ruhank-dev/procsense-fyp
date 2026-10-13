"""Session files for the tests, made from a real file the engine wrote (tests/fixtures/golden_session_v1.ndjson: the golden correct run, 78 actions)."""
from __future__ import annotations

import hashlib
import json
import uuid
from pathlib import Path

GOLDEN = Path(__file__).resolve().parent / "fixtures" / "golden_session_v1.ndjson"


def golden_text() -> str:
    return GOLDEN.read_text(encoding="utf-8")


def golden_log_lines() -> list[str]:
    return [line for line in golden_text().split("\n")[1:] if line]


def make_file(*, session_id: uuid.UUID | None = None, mode: str = "practice", package: str = "", content_version: str = "0123456789ab-practice", engine_version: str = "0.1.0",
              lines: list[str] | None = None, v2: bool = True, header_extra: dict | None = None) -> str:
    """A well-formed session file: the golden log (or `lines`), a header with the right hash, and (by default) the version 2 header fields."""
    lines = list(golden_log_lines() if lines is None else lines)
    log_text = "".join(line + "\n" for line in lines)
    header = {"case": "VAR-S1-A", "seed": "42", "mode": mode, "log_hash": hashlib.sha256(log_text.encode("utf-8")).hexdigest()}
    if v2:
        header.update(session_id=str(session_id or uuid.uuid4()), content_version=content_version, engine_version=engine_version, scenario_package_hash=package)
    header.update(header_extra or {})
    return json.dumps({"header": header}, separators=(",", ":")) + "\n" + log_text


def changed_log(edit) -> list[str]:
    """The golden log with one change made by edit(entries) (a list of dicts), written back in the engine's canonical key order."""
    entries = [json.loads(line) for line in golden_log_lines()]
    edit(entries)
    return [json.dumps(e, separators=(",", ":")) for e in entries]
