"""Which simulator action a spoken sentence is (DT-251, ADR-0027): the language model reads the transcript, the recent turns and the actions available now (content/ai/spoken_actions.json) and answers with
action ids and who was spoken to. The ids are chosen from a closed list built here from the server's own content, so a request cannot add an action or change what an action means. Nothing here decides what an
action changes or what it scores: the client starts the same action as the tablet button and the engine decides. A model that is down, slow or answers badly gives no action (the tablet remains).

    s = SpeechActions(client)
    r = s.interpret(["ask:medicines", "ask_allergies"], [{"who": "trainee", "text": "..."}], "What tablets are you on?")
    r.actions, r.addressee, r.source        # ["ask:medicines"], "patient", "model"
"""
from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

try:
    from llm import gateway
except ImportError:
    from server.llm import gateway

ROOT = Path(__file__).resolve().parents[2]
ACTIONS_FILE = ROOT / "content" / "ai" / "spoken_actions.json"
ADDRESSEES = ["patient", "nurse", "anaesthesia", "nobody"]
MAX_ACTIONS = 2


def catalogue() -> dict[str, dict]:
    return {a["id"]: a for a in json.loads(ACTIONS_FILE.read_text(encoding="utf-8"))["actions"]}


@dataclass
class SpokenAction:
    actions: list[str] = field(default_factory=list)
    addressee: str = "nobody"
    source: str = "model"                 # model | fallback
    reasons: list[str] = field(default_factory=list)
    audit: dict = field(default_factory=dict)


def schema_for(ids: list[str]) -> dict:
    return {"type": "object", "additionalProperties": False, "required": ["actions", "addressee"],
            "properties": {"actions": {"type": "array", "maxItems": MAX_ACTIONS, "uniqueItems": True, "items": {"type": "string", "enum": ids}}, "addressee": {"type": "string", "enum": ADDRESSEES}}}


class SpeechActions:
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        self.client, self.model, self.timeout = client, model, timeout

    def interpret(self, available: list[str], turns: list[dict], utterance: str) -> SpokenAction:
        cat = catalogue()
        ids = [i for i in dict.fromkeys(available) if i in cat]
        if not ids or sum(c.isalpha() for c in utterance) < 2:                 # nothing to choose from, or recogniser noise: no model call
            return SpokenAction([], "nobody", "fallback", ["NOTHING_TO_ASK" if not ids else "EMPTY_INPUT"])
        if self.client is None:
            return SpokenAction([], "nobody", "fallback", ["NO_MODEL"])
        menu = "\n".join(f"- {i}: {cat[i]['label']}. {cat[i]['means']}" for i in ids)
        recent = "\n".join(f"{t.get('who', '?')}: {t.get('text', '')}" for t in turns[-4:]) or "(none)"
        r = gateway.run(self.client, "speech.action", {"menu": menu, "turns": recent, "utterance": utterance}, schema=schema_for(ids), model=self.model, timeout=self.timeout, retries=1, task="speech_action")
        if not r.ok:
            return SpokenAction([], "nobody", "fallback", [d.split(":")[0] for d in r.audit["defects"]] or [r.audit["error"] or "REJECTED"], r.audit)
        return SpokenAction(list(r.value["actions"]), r.value["addressee"], "model", [], r.audit)
