"""The contracts of live scenario generation (p1-live-scenario-generation-s01, stage LG-01): SessionRequest v1, Objectives v1 and ScenarioPackage v1.

    from generation.contracts import accept_request, validate
    request = accept_request(document, draw_seed=secrets.randbelow)     # a request with a seed and a request id, or RequestRefused

The JSON schemas live in server/schemas. A request is accepted with a logged seed or refused with a reason; no request goes on without a seed. Assessment mode is refused with a 409:
assessment keeps fixed cases, so the caller serves one of those instead.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Callable

import jsonschema

SCHEMAS = Path(__file__).resolve().parents[1] / "schemas"
NAMES = {"session_request": "session_request.json", "objectives": "objectives.json", "scenario_package": "scenario_package.json"}


class RequestRefused(Exception):
    """A request that cannot be served. `status` is the HTTP status the gateway answers with; `errors` name the fields."""

    def __init__(self, status: int, code: str, message: str, errors: list[str] | None = None):
        super().__init__(message)
        self.status, self.code, self.errors = status, code, errors or []


def schema(name: str) -> dict:
    return json.loads((SCHEMAS / NAMES[name]).read_text(encoding="utf-8"))


def errors(name: str, document: object) -> list[str]:
    """Every way the document breaks the schema, as 'path: message' lines (empty if it is valid)."""
    validator = jsonschema.Draft202012Validator(schema(name))
    return [f"{'/'.join(str(p) for p in e.absolute_path) or '(request)'}: {e.message[:160]}" for e in sorted(validator.iter_errors(document), key=lambda e: list(map(str, e.absolute_path)))]


def validate(name: str, document: object) -> None:
    found = errors(name, document)
    if found:
        raise ValueError(f"{name} is not valid: " + "; ".join(found[:5]))


def request_id(request: dict) -> str:
    """Same request (trainee, mode, scenario, level, seed, focus), same id."""
    key = json.dumps([request["trainee"], request["mode"], request["scenario"], request["level"], request["seed"], sorted(request.get("focus", []))], sort_keys=True)
    return hashlib.sha256(key.encode("utf-8")).hexdigest()[:16]


def accept_request(document: object, draw_seed: Callable[[int], int]) -> dict:
    """The request with its seed filled in (`draw_seed(2**31)` is called only when the request has none) and `seed_source`, or RequestRefused (400 for a bad request, 409 for assessment mode)."""
    found = errors("session_request", document)
    if found:
        raise RequestRefused(400, "INVALID_REQUEST", "the request is not valid", found)
    request = dict(document)  # type: ignore[arg-type]
    if request["mode"] != "practice":
        raise RequestRefused(409, "ASSESSMENT_NOT_GENERATED", "assessment mode uses fixed cases; serve a fixed assessment case instead")
    request["seed_source"] = "given" if "seed" in request else "drawn"
    if "seed" not in request:
        request["seed"] = int(draw_seed(2**31))
    request["request_id"] = request_id(request)
    return request
