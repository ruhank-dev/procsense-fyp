"""Reading a session file the way the engine writes it (p1-backend-s02): one header line `{"header":{...}}` and then one JSON line per logged action.

The shapes are strict (unknown fields are refused, like the engine's own reader) and every line is checked on its own, so an error names the line. The header's `log_hash` must be the SHA-256 of the log text
(everything after the first line, lines ending in a line feed): that proves the file arrived whole and is the hash the replay will have to reproduce. Header version 2 adds the session id, the content version, the engine version and the
scenario package hash (p1-backend-s09); version 1 files (what the engine writes today) are only accepted when the caller says so.
"""
from __future__ import annotations

import hashlib
import json
import uuid
from dataclasses import dataclass
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field, ValidationError

MAX_ENTRIES = 20000
HEX64 = r"^[0-9a-f]{64}$"


class SessionFileError(Exception):
    """A problem with the file. `code` is short and fixed; `line` is 1-based (0 = the file as a whole); the message never contains the content of the line."""
    def __init__(self, code: str, line: int = 0, detail: str = ""):
        super().__init__(f"{code} (line {line})" if line else code)
        self.code, self.line, self.detail = code, line, detail


class Header(BaseModel):
    model_config = ConfigDict(extra="forbid")
    case: str = Field(pattern=r"^[A-Za-z0-9._-]{1,40}$")
    seed: str = Field(pattern=r"^[0-9]{1,20}$")
    mode: Literal["practice", "assessment"]
    log_hash: str = Field(pattern=HEX64)
    # version 2 (p1-backend-s09): given by the host, outside the replay hash
    session_id: uuid.UUID | None = None
    content_version: str | None = Field(default=None, min_length=1, max_length=64)
    engine_version: str | None = Field(default=None, min_length=1, max_length=40)
    scenario_package_hash: str | None = Field(default=None, pattern=r"^$|^[0-9a-f]{64}$")


class Entry(BaseModel):
    model_config = ConfigDict(extra="forbid")
    seq: int = Field(ge=0)
    step: int = Field(ge=0)
    verb: str = Field(pattern=r"^[a-z][a-z0-9_]{0,63}$")
    target: str = Field(max_length=64)
    channel: Literal["vr", "tablet", "cli", "system"]
    params: dict[str, str] = Field(max_length=24)
    accepted: bool
    reject: str = Field(max_length=64)
    effects: list[str] = Field(max_length=64)


@dataclass(frozen=True)
class ParsedSession:
    header: Header
    entries: list[Entry]
    log_text: str   # exactly what the hash covers
    is_v2: bool


def _short(error: ValidationError) -> str:
    first = error.errors()[0]
    return ".".join(str(p) for p in first["loc"]) + ": " + first["type"]   # the field and the kind of mistake, never the value


def parse_session_file(text: str, require_v2: bool = True) -> ParsedSession:
    text = text.replace("\r\n", "\n")
    if not text.strip():
        raise SessionFileError("EMPTY_FILE")
    head_line, _, log_text = text.partition("\n")
    try:
        raw = json.loads(head_line)
    except ValueError:
        raise SessionFileError("HEADER_NOT_JSON", 1) from None
    if not isinstance(raw, dict) or set(raw) != {"header"} or not isinstance(raw["header"], dict):
        raise SessionFileError("HEADER_SHAPE", 1)
    try:
        header = Header(**raw["header"])
    except ValidationError as e:
        raise SessionFileError("HEADER_INVALID", 1, _short(e)) from None
    is_v2 = all(v is not None for v in (header.session_id, header.content_version, header.engine_version, header.scenario_package_hash))
    if require_v2 and not is_v2:
        raise SessionFileError("HEADER_NEEDS_VERSION_2", 1, "session_id, content_version, engine_version and scenario_package_hash are required")
    if not is_v2 and any(v is not None for v in (header.session_id, header.content_version, header.engine_version, header.scenario_package_hash)):
        raise SessionFileError("HEADER_PARTLY_VERSION_2", 1)

    if hashlib.sha256(log_text.encode("utf-8")).hexdigest() != header.log_hash:
        raise SessionFileError("LOG_HASH_MISMATCH", 0, "the log text does not match the hash in the header")

    entries: list[Entry] = []
    previous_step = 0
    for number, line in enumerate(log_text.split("\n"), start=2):
        if line == "":
            continue
        if len(entries) >= MAX_ENTRIES:
            raise SessionFileError("TOO_MANY_ENTRIES", number)
        try:
            decoded = json.loads(line)
        except ValueError:
            raise SessionFileError("LINE_NOT_JSON", number) from None
        if not isinstance(decoded, dict):
            raise SessionFileError("LINE_INVALID", number, "not an object")
        try:
            entry = Entry(**decoded)
        except ValidationError as e:   # (pydantic's ValidationError is a ValueError, so it is caught on its own, not with the JSON errors)
            raise SessionFileError("LINE_INVALID", number, _short(e)) from None
        if entry.seq != len(entries):
            raise SessionFileError("LOG_NOT_SEQUENTIAL", number, f"expected seq {len(entries)}")
        if entry.step < previous_step:
            raise SessionFileError("LOG_TIME_GOES_BACKWARDS", number)
        previous_step = entry.step
        entries.append(entry)
    if not entries:
        raise SessionFileError("NO_ENTRIES")
    return ParsedSession(header, entries, log_text, is_v2)
