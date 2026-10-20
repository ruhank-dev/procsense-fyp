"""Strict validation of a semantic reader's reply. A reply is either exactly a valid `Reading` or it is REJECTED with a code; nothing is repaired, stripped, coerced or filled in (no fence removal, no enum correction, no invented field), because a repair could turn a failed reply into credit.
Rejected: empty output, a reply that stopped for any reason but "stop" (truncated), a code fence, text before or after the object, not a JSON object, duplicate keys, a schema violation (missing or unexpected field, wrong type, value outside an enum), an objective that is not an approved identifier, the same objective twice,
SUPPORTED without evidence that is a verbatim part of the utterance, and SUPPORTED that is not the trainee acting now. Valid JSON is never treated as proof that the reading is right."""
from __future__ import annotations

import json
import re

from pydantic import ValidationError

from server.interpret.llm.schema import Reading


class Invalid(Exception):
    def __init__(self, code: str, detail: str = ""):
        super().__init__(code)
        self.code, self.detail = code, detail


def norm(t: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9' ]+", " ", t.lower().replace("’", "'")).split())


def _no_duplicates(pairs):
    keys = [k for k, _ in pairs]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate key")
    return dict(pairs)


def parse(text: str, done_reason: str | None, utterance: str, objective_ids: list[str] | set[str]) -> Reading:
    if text is None or not text.strip():
        raise Invalid("EMPTY")
    if done_reason is not None and done_reason != "stop":
        raise Invalid("TRUNCATED", str(done_reason))
    body = text.strip()
    if body.startswith("```") or body.endswith("```"):
        raise Invalid("FENCED")
    if not body.startswith("{"):
        raise Invalid("NOT_JSON")
    try:
        doc, end = json.JSONDecoder(object_pairs_hook=_no_duplicates).raw_decode(body)
    except ValueError as e:
        raise Invalid("NOT_JSON" if "duplicate" not in str(e) else "DUPLICATE_KEY", str(e)[:80])
    if body[end:].strip():
        raise Invalid("SURROUNDING_TEXT")
    try:
        reading = Reading.model_validate(doc)
    except ValidationError as e:
        raise Invalid("SCHEMA", e.errors()[0]["type"])
    ids, seen, nu = set(objective_ids), set(), norm(utterance)
    for f in reading.findings:
        if f.objective not in ids:
            raise Invalid("UNKNOWN_OBJECTIVE")
        if f.objective in seen:
            raise Invalid("DUPLICATE_OBJECTIVE")
        seen.add(f.objective)
        if f.verdict == "SUPPORTED":
            if len(norm(f.evidence)) < 2 or norm(f.evidence) not in nu:
                raise Invalid("EVIDENCE_NOT_IN_UTTERANCE")
            if f.actor != "trainee" or f.modality != "actual":
                raise Invalid("SUPPORTED_NOT_ACTUAL_TRAINEE")
    return reading
