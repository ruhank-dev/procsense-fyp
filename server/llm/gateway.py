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

from . import client as client_mod, prompts, routing


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
        model: str | None = None, version: int | None = None, task: str | None = None, schema: dict | None = None, cloud_only: bool = False) -> Result:
    """`schema` replaces the prompt's own schema for this call (used when the allowed values are known only at run time, such as the ids a proposal may choose from).
    `cloud_only`: the task has no use for a local model's answer (the spoken-order interpreter: only the evaluated cloud model counts, the grammar is its fallback). A route that is not cloud (switch off, no approval, no consent) makes no model call at all, and a cloud leg that fails has no local second leg;
    the result is then a failure whose first attempt says why (`route:<reason>`, or the cloud error)."""
    prompt = prompts.get(prompt_id, version)                         # the latest version: it fixes the role; each model then gets the version written for it (a local model skips a cloud-scope version)
    caller_schema = schema
    if model is None:
        route = routing.resolve(task, prompt.id, prompt.role)
    else:                                                              # a caller that names a model (a benchmark, a test) gets exactly it; a cloud one still falls back to the local default
        local = routing.default_model(prompt.role)
        route = routing.Route(model, routing.is_cloud(model), "explicit", local if routing.is_cloud(model) else "")
    timeout = timeout if timeout is not None else (20.0 if prompt.role == "live" else 600.0)
    if cloud_only and not route.cloud:
        why = [{"attempt": 0, "model": "", "ok": False, "latency_ms": 0, "error": "route:" + route.reason, "digest": "", "output_tokens": 0, "defects": []}]
        return Result(False, None, True, why, _audit(prompt, task, route.model, client_mod.Reply(False, error="route:" + route.reason), "", why, "fallback", route, ""))
    legs = [route.model] + ([route.fallback_model] if route.cloud and route.fallback_model and not cloud_only else [])
    attempts: list[dict] = []
    reply = client_mod.Reply(False, error="NOT_CALLED")
    system = user = ""
    for chosen in legs:
        cloud_leg = routing.is_cloud(chosen)
        prompt = prompts.get(prompt_id, version, cloud=cloud_leg)
        if cloud_leg and version is not None:                                  # a cloud leg uses the newest prompt written for the cloud model even when the caller pins a version for the local one
            newest = prompts.get(prompt_id, None, cloud=True)
            if newest.scope == "cloud" and newest.version > prompt.version:
                prompt = newest
        system, user = prompt.render(**variables)
        schema = caller_schema if caller_schema is not None else prompt.schema
        if cloud_leg and not routing.acquire_cloud(prompt.role):          # the plan allows one cloud request at a time: a live task does not queue behind another, it answers locally
            attempts.append({"attempt": len(attempts), "model": chosen, "ok": False, "latency_ms": 0, "error": "CLOUD_BUSY", "digest": "", "output_tokens": 0, "defects": []})
            continue
        try:
            leg_timeout = min(timeout, routing.cloud_timeout(prompt.role)) if cloud_leg else timeout
            leg_retries = 0 if cloud_leg and prompt.role == "live" else retries   # a slow or invalid live cloud answer goes to the local model, not to a second wait
            value, reply, ok = _attempts(client, prompt, system, user, schema, validate, chosen, leg_retries, leg_timeout, attempts)
        finally:
            if cloud_leg:
                routing.release_cloud()
        if ok:
            return Result(True, value, False, attempts, _audit(prompt, task, chosen, reply, system + user, attempts, "ok", route, legs[0] if chosen != legs[0] else ""))
    return Result(False, None, True, attempts, _audit(prompt, task, chosen, reply, system + user, attempts, "fallback", route, legs[0] if chosen != legs[0] else ""))


def _attempts(client, prompt, system: str, user: str, schema, validate, chosen: str, retries: int, timeout: float, attempts: list[dict]):
    """One model, up to retries + 1 tries; appends a record per try to `attempts`. Returns (value, last reply, ok)."""
    avoid = ""
    for attempt in range(retries + 1):
        reply = client.chat(chosen, system, routing.prompt_for(chosen, user, schema) + avoid, schema=schema, think=prompt.think, timeout=timeout, num_predict=routing.tokens_for(chosen, prompt.num_predict))
        record = {"attempt": len(attempts), "model": chosen, "ok": reply.ok, "latency_ms": reply.latency_ms, "error": reply.error, "digest": reply.digest, "output_tokens": reply.output_tokens, "reported_model": reply.host_model}
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
            return (reply.json if schema is not None else reply.text), reply, True
        if reply.error in ("UNREACHABLE", "TIMEOUT", "MODEL_MISSING") or (reply.error == "HTTP_ERROR" and routing.is_cloud(chosen)):
            break                                                      # retrying a host that is down (or a cloud usage limit, HTTP 402 or 429) only makes the trainee wait
        avoid = "\n\nYour previous answer was rejected for these reasons; answer again and fix them:\n- " + "\n- ".join(defects or [reply.error + ": " + reply.detail])
    return None, reply, False


def _audit(prompt: prompts.Prompt, task: str | None, model: str, reply: client_mod.Reply, filled: str, attempts: list[dict], outcome: str, route: routing.Route | None = None, fell_back_from: str = "") -> dict:
    return {"task": task or prompt.id, "prompt_id": prompt.id, "prompt_version": prompt.version, "prompt_sha256": prompt.sha256, "model": model, "model_digest": reply.digest,
            "route": route.reason if route else "", "data_class": route.data_class if route else "", "cloud": routing.is_cloud(model), "fell_back_from": fell_back_from,
            "input_hash": _hash(filled), "output_hash": _hash(reply.text) if reply.text else "", "latency_ms": sum(a["latency_ms"] for a in attempts), "attempts": len(attempts),
            "first_try_schema_valid": bool(attempts[0]["ok"]) and not any(d.startswith("SCHEMA") for d in attempts[0]["defects"]), "outcome": outcome, "defects": [d for a in attempts for d in a["defects"]], "error": reply.error, "at": reply.at}
