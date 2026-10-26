"""What a spoken order still needs before the engine can take it, and how the trainee supplies it (ADR-0028, Unity integration of 27 Oct 2026).

The interpreter names an item ("oxygen", "take her to theatre"); the engine's verbs also need details the sentence usually does not carry (the oxygen device, how the patient is moved). Nothing is guessed: `needs_for(action)` lists the missing
required parameters with the closed list of values the content allows, the client shows them as buttons, and `apply_choices` puts the chosen values into the action. The route (`POST /orders/.../details`) does this on the SIGNED action of a ticket and issues a new
single-use ticket for the completed action, so the trainee still confirms exactly what will be sent and the engine still validates it. A value that is not in the closed list is refused.
"""
from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


@lru_cache(maxsize=1)
def _treatments() -> dict:
    return json.loads((ROOT / "content" / "treatments.json").read_text(encoding="utf-8"))


@lru_cache(maxsize=1)
def _transport() -> dict:
    return json.loads((ROOT / "content" / "locations" / "locations.json").read_text(encoding="utf-8"))["transport"]


def treatment_kind(treatment_id: str) -> str:
    """medicine | fluid | oxygen | procedure, or "" for an id that is not a treatment."""
    return next((t["kind"] for t in _treatments()["treatments"] if t["id"] == treatment_id), "")


def _options(param: str) -> list[dict]:
    if param == "device":
        return [{"id": d["id"], "label": d["label"]} for d in _treatments()["oxygen_devices"]]
    if param == "mode":
        return [{"id": m["id"], "label": m["id"].replace("_", " ").capitalize()} for m in _transport()["modes"]]
    return []


REQUIRED = {"request_oxygen": ("device", "Which oxygen device?"), "request_transport": ("mode", "How should the patient be moved?")}


def needs_for(action: dict | None) -> list[dict]:
    """The required parameters the action lacks, each with its question and its closed list of values."""
    if not action or action.get("verb") not in REQUIRED:
        return []
    param, question = REQUIRED[action["verb"]]
    if (action.get("params") or {}).get(param):
        return []
    return [{"param": param, "question": question, "options": _options(param)}]


def apply_choices(action: dict, choices: dict) -> tuple[dict | None, str]:
    """(the completed action, "") or (None, the reason). Only the parameters the action needs, only values of the closed list."""
    needed = {n["param"]: {o["id"] for o in n["options"]} for n in needs_for(action)}
    if not choices or set(choices) - set(needed):
        return None, "UNKNOWN_DETAIL"
    for param, value in choices.items():
        if value not in needed[param]:
            return None, "VALUE_NOT_ALLOWED"
    return {"verb": action["verb"], "params": {**(action.get("params") or {}), **choices}}, ""


def label_of(param: str, value: str) -> str:
    return next((o["label"] for o in _options(param) if o["id"] == value), value)
