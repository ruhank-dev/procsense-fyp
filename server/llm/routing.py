"""Which model answers which task (ADR-0026): a per-task route read from `content/ai/model_routes.json`, a switch for cloud routing, a privacy gate for the data a route may send, and the local fallback.

    route = routing.resolve("dialogue_patient", "dialogue.patient", "live")
    route.model, route.cloud, route.reason, route.fallback_model

A route names a model and the class of data the task sends: `synthetic` (authored content only: composition, critic and judge of generated scenarios) or `trainee` (anything that carries what a trainee said or did:
dialogue, debrief, the explanation of a result). A cloud model is chosen only when ALL of these hold: cloud routing is switched on (`CLOUD_ROUTING_ENABLED`, off by default), the route names a cloud model, and
the data class is allowed (`synthetic` always; `trainee` only when `CLOUD_TRAINEE_DATA_APPROVED` is set, which waits for the privacy decision DT-248). Otherwise the task uses the local model, as before this module
existed. The module decides nothing about clinical content, credit or scoring: a reply from any model passes the same schema and grounding validators.
"""
from __future__ import annotations

import json
import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path

from . import client as client_mod

ROUTES_FILE = Path(__file__).resolve().parents[2] / "content" / "ai" / "model_routes.json"
THINKING_BUDGET = 1500          # models that always think (gpt-oss) spend output tokens on it; the budget is added to the prompt's own

# Cloud replies are plain text: the platform does not enforce a schema (ADR-0024), and models wrap JSON in a code fence unless told not to. The instruction asks; the strict parser still rejects a fence.
FORMAT_INSTRUCTION = ("\n\nFormat: reply with exactly one JSON object that follows this schema, as plain text. Do not use a code fence or markdown, and write nothing before or after the object.\nSchema: ")


def is_cloud(model: str) -> bool:
    return model.endswith("-cloud") or ":cloud" in model


def always_thinks(model: str) -> bool:
    return model.startswith("gpt-oss")


@lru_cache
def _table() -> dict:
    return json.loads(ROUTES_FILE.read_text(encoding="utf-8"))


def reload() -> None:
    _table.cache_clear()


def _flag(name: str) -> bool:
    return os.environ.get(name, "").strip().lower() in ("1", "true", "yes", "on")


def _setting(name: str) -> bool:
    """The same switch from the service's settings (.env), for when the process environment does not carry it."""
    try:
        try:
            from ..config import get_settings
        except ImportError:
            from config import get_settings                    # the tools put server/ on the path
        return bool(getattr(get_settings(), name))
    except Exception:                                          # settings that cannot be built (a tool without the service's environment) mean "off"
        return False


def enabled() -> bool:
    return _flag("CLOUD_ROUTING_ENABLED") or _setting("cloud_routing_enabled")


def trainee_data_approved() -> bool:
    return _flag("CLOUD_TRAINEE_DATA_APPROVED") or _setting("cloud_trainee_data_approved")


@dataclass(frozen=True)
class Route:
    model: str
    cloud: bool
    reason: str                      # why this model: "default", "route", "switch_off", "privacy_gate", "no_route"
    fallback_model: str              # the local model a cloud route falls back to on an outage; "" when the route is already local
    data_class: str = ""


def default_model(role: str) -> str:
    return client_mod.LIVE_MODEL if role == "live" else client_mod.BATCH_MODEL


def resolve(task: str | None, prompt_id: str, role: str) -> Route:
    local = default_model(role)
    entry = _table()["routes"].get(task or "") or _table()["routes"].get(prompt_id)
    if entry is None:
        return Route(local, False, "no_route", "")
    model = entry["model"]
    data_class = entry["data_class"]
    if not is_cloud(model):
        return Route(model, False, "route", "", data_class)
    if not enabled():
        return Route(local, False, "switch_off", "", data_class)
    if data_class != "synthetic" and not trainee_data_approved():
        return Route(local, False, "privacy_gate", "", data_class)
    return Route(model, True, "route", entry.get("fallback", local), data_class)


def prompt_for(model: str, user: str, schema: dict | None) -> str:
    """The user text for a model: unchanged for a local model (which enforces the schema itself), with the format instruction for a cloud one."""
    if schema is not None and is_cloud(model):
        return user + FORMAT_INSTRUCTION + json.dumps(schema, separators=(",", ":"))
    return user


def tokens_for(model: str, num_predict: int) -> int:
    return num_predict + THINKING_BUDGET if always_thinks(model) else num_predict
