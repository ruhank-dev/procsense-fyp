"""Calling a registered prompt with a schema, a validator, one retry and an audit record (p1-backend-s06, p1-ai-language-s02; ADR-0005 and ADR-0010).

    result = run(client, "compose.narrative", {"patient": "..."}, validate=my_checks)
    result.ok           True only if the model answered, the answer satisfies the prompt's JSON Schema, and `validate` found nothing wrong
    result.value        the parsed answer (only when ok)
    result.fallback     True when the caller must use its template: the model was not reachable, was too slow, or failed twice
    result.audit        one dict for the audit log: task, prompt id and version and hash, model and digest, input hash, latency, attempts, outcome, defects (hashes and reasons, never the filled prompt)

The rules: the answer is never trusted until it is checked (schema, then the caller's checks); one retry gives the model the defects as an avoid-hint; after that the caller falls back. Nothing is raised for an
ordinary model failure. The caller's `validate(value) -> list[str]` is where the grounding and Stage B style checks go.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass, field
from typing import Callable

import jsonschema

from . import client as client_mod, prompts


@dataclass
class Result:
    ok: bool
    value: object = None
    fallback: bool = False
    attempts: list[dict] = field(default_factory=list)
    audit: dict = field(default_factory=dict)


def _hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


def run(client: client_mod.OllamaClient, prompt_id: str, variables: dict, validate: Callable[[object], list[str]] | None = None, retries: int = 1, timeout: float | None = None,
        model: str | None = None, version: int | None = None, task: str | None = None, schema: dict | None = None) -> Result:
    """`schema` replaces the prompt's own schema for this call (used when the allowed values are known only at run time, such as the ids a proposal may choose from)."""
    prompt = prompts.get(prompt_id, version)
    system, user = prompt.render(**variables)
    schema = schema if schema is not None else prompt.schema
    chosen = model or (client_mod.LIVE_MODEL if prompt.role == "live" else client_mod.BATCH_MODEL)
    timeout = timeout if timeout is not None else (20.0 if prompt.role == "live" else 600.0)
    attempts: list[dict] = []
    avoid = ""
    for attempt in range(retries + 1):
        reply = client.chat(chosen, system, user + avoid, schema=schema, think=prompt.think, timeout=timeout, num_predict=prompt.num_predict)
        record = {"attempt": attempt, "ok": reply.ok, "latency_ms": reply.latency_ms, "error": reply.error, "digest": reply.digest, "output_tokens": reply.output_tokens}
        defects: list[str] = []
        if reply.ok and schema is not None:
            try:
                jsonschema.validate(reply.json, schema)
            except jsonschema.ValidationError as e:
                defects.append("SCHEMA: " + e.message[:160])
        if reply.ok and not defects and validate is not None:
            defects += validate(reply.json if schema is not None else reply.text)
        record["defects"] = defects
        attempts.append(record)
        if reply.ok and not defects:
            return Result(True, reply.json if schema is not None else reply.text, False, attempts, _audit(prompt, task, chosen, reply, system + user, attempts, "ok"))
        if reply.error in ("UNREACHABLE", "TIMEOUT", "MODEL_MISSING"):
            break                                                      # retrying a host that is down only makes the trainee wait
        avoid = "\n\nYour previous answer was rejected for these reasons; answer again and fix them:\n- " + "\n- ".join(defects or [reply.error + ": " + reply.detail])
    return Result(False, None, True, attempts, _audit(prompt, task, chosen, reply, system + user, attempts, "fallback"))


def _audit(prompt: prompts.Prompt, task: str | None, model: str, reply: client_mod.Reply, filled: str, attempts: list[dict], outcome: str) -> dict:
    return {"task": task or prompt.id, "prompt_id": prompt.id, "prompt_version": prompt.version, "prompt_sha256": prompt.sha256, "model": model, "model_digest": reply.digest,
            "input_hash": _hash(filled), "output_hash": _hash(reply.text) if reply.text else "", "latency_ms": sum(a["latency_ms"] for a in attempts), "attempts": len(attempts),
            "first_try_schema_valid": bool(attempts[0]["ok"]) and not any(d.startswith("SCHEMA") for d in attempts[0]["defects"]), "outcome": outcome, "defects": [d for a in attempts for d in a["defects"]], "error": reply.error, "at": reply.at}
