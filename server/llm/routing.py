"""Which model answers which task (ADR-0026): a per-task route read from `content/ai/model_routes.json`, a switch for cloud routing, a privacy gate for the data a route may send, and the local fallback.

    route = routing.resolve("dialogue_patient", "dialogue.patient", "live")
    route.model, route.cloud, route.reason, route.fallback_model

A route names a model and the class of data the task sends: `synthetic` (authored content only: composition, critic and judge of generated scenarios) or `trainee` (anything that carries what a trainee said or did:
dialogue, debrief, the explanation of a result). A cloud model is chosen only when ALL of these hold: cloud routing is switched on (`CLOUD_ROUTING_ENABLED`, off by default), the route names a cloud model, and
the data class is allowed (`synthetic` always; `trainee` only when `CLOUD_TRAINEE_DATA_APPROVED` is set, which waits for the privacy decision DT-248). Otherwise the task uses the local model, as before this module
existed. The module decides nothing about clinical content, credit or scoring: a reply from any model passes the same schema and grounding validators.
"""
from __future__ import annotations

import contextlib
import contextvars
import json
import os
import threading
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


def _switch(env_name: str, setting: str) -> bool:
    """A switch: the process environment when it names the variable (so a test or a one-off run can force either way), otherwise the service's settings (.env), otherwise off."""
    raw = os.environ.get(env_name)
    if raw is not None and raw.strip() != "":
        return raw.strip().lower() in ("1", "true", "yes", "on")
    try:
        try:
            from ..config import get_settings
        except ImportError:
            from config import get_settings                    # the tools put server/ on the path
        return bool(getattr(get_settings(), setting))
    except Exception:                                          # settings that cannot be built (a tool without the service's environment) mean "off"
        return False


def enabled() -> bool:
    return _switch("CLOUD_ROUTING_ENABLED", "cloud_routing_enabled")


def trainee_data_approved() -> bool:
    """The privacy decision (DT-248, DT-127) is recorded: real trainee data may go to the cloud."""
    return _switch("CLOUD_TRAINEE_DATA_APPROVED", "cloud_trainee_data_approved")


def synthetic_deployment() -> bool:
    """The owner's declaration that THIS deployment handles synthetic data only (development, demonstrations, tests with invented people). It releases the trainee-class routes for such a deployment; it is not the
    privacy approval and must never be set on a deployment that real trainees use."""
    return _switch("CLOUD_SYNTHETIC_DEPLOYMENT", "cloud_synthetic_deployment")


_CONSENT: contextvars.ContextVar = contextvars.ContextVar("cloud_consent", default=None)


@contextlib.contextmanager
def consent(granted: bool):
    """Inside the block the person whose words are being processed has (True) or has not (False) given the optional cloud AI consent, current text, not withdrawn. A route sets it from the stored answer; outside any
    block there is no consent. Real trainee data reaches the cloud only with it (and the institutional approval, below)."""
    token = _CONSENT.set(bool(granted))
    try:
        yield
    finally:
        _CONSENT.reset(token)


def has_consent() -> bool:
    return bool(_CONSENT.get())


def trainee_cloud_allowed() -> bool:
    """May the words of the person being processed go to the cloud? Yes for a deployment declared synthetic (invented data). Otherwise only when the applicable institutional and privacy requirements are recorded
    (`CLOUD_TRAINEE_DATA_APPROVED`, set by the operator) AND this person has given the optional cloud consent: accepting the Terms is not enough."""
    return synthetic_deployment() or (trainee_data_approved() and has_consent())


def status() -> dict:
    """What the routing does right now, for /health and the operator: no secrets."""
    return {"cloud_routing": enabled(), "synthetic_deployment": synthetic_deployment(), "trainee_data_approved": trainee_data_approved(),
            "trainee_routes_use_cloud": enabled() and (synthetic_deployment() or trainee_data_approved()), "needs_personal_consent": enabled() and not synthetic_deployment() and trainee_data_approved(),
            "spoken_orders": spoken_orders_status()}


def spoken_orders_status() -> dict:
    """Which system reads a spoken order right now, before any call: Gemma 4 31B Cloud is the preferred reader; the offline grammar answers when the cloud is not used or does not answer. Per call the answer says which one it was."""
    preferred = _table()["routes"].get("orders.interpret", {}).get("model", "")
    if not enabled():
        who, why = "offline_grammar", "CLOUD_ROUTING_OFF"
    elif synthetic_deployment():
        who, why = "gemma_cloud", ""
    elif trainee_data_approved():
        who, why = "gemma_cloud_with_personal_consent", "NO_CONSENT_UNTIL_THE_PERSON_AGREES"
    else:
        who, why = "offline_grammar", "PRIVACY_GATE"
    return {"preferred": preferred, "active": who, "grammar_fallback_reason": why, "profile": "synthetic_demonstration" if synthetic_deployment() else "standard"}


# The free plan allows one cloud request at a time. A task takes the slot without waiting longer than a moment (a live task must not queue behind another: it answers on the local model instead); an offline task may wait.
_CLOUD_SLOT = threading.Semaphore(1)
LIVE_WAIT_SECONDS = 0.25
BATCH_WAIT_SECONDS = 120.0


def acquire_cloud(role: str) -> bool:
    return _CLOUD_SLOT.acquire(timeout=LIVE_WAIT_SECONDS if role == "live" else BATCH_WAIT_SECONDS)


def release_cloud() -> None:
    _CLOUD_SLOT.release()


def cloud_timeout(role: str) -> float:
    return float(_table().get("cloud_timeout_seconds", {}).get(role, 4.0 if role == "live" else 120.0))


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
    if data_class != "synthetic" and not trainee_cloud_allowed():
        return Route(local, False, "no_consent" if trainee_data_approved() else "privacy_gate", "", data_class)
    return Route(model, True, "route", entry.get("fallback", local), data_class)


def prompt_for(model: str, user: str, schema: dict | None) -> str:
    """The user text for a model: unchanged for a local model (which enforces the schema itself), with the format instruction for a cloud one."""
    if schema is not None and is_cloud(model):
        return user + FORMAT_INSTRUCTION + json.dumps(schema, separators=(",", ":"))
    return user


def tokens_for(model: str, num_predict: int) -> int:
    return num_predict + THINKING_BUDGET if always_thinks(model) else num_predict
