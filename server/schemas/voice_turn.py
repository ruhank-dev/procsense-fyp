"""The optional fields every spoken turn may carry (P3-API-02, docs/plans/p3/p3-15-integration-contracts.md `VoiceTurn`): who it was addressed to, whether the trainee plays by voice or menu, whether the session
is practice or an examination, and the request id the headset gave the utterance (P3-API-05). All optional, so an older client that sends none of them is answered exactly as before. They are context only: the
engine still decides what is accepted and scored."""
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field

REQUEST_ID_PATTERN = r"^[A-Za-z0-9._:-]{1,64}$"     # a GUID, or the headset's request counter (LanHttp's X-Request-Id); nothing that could carry text or break a log line

InteractionMode = Literal["menu", "voice"]
SessionMode = Literal["practice", "assessment", "examination"]   # "assessment" is the engine's word, "examination" the headset's; both mean the marked mode


class TurnFields(BaseModel):
    addressee: str | None = Field(default=None, max_length=24)
    interaction_mode: InteractionMode | None = None
    session_mode: SessionMode | None = None
    request_id: str | None = Field(default=None, pattern=REQUEST_ID_PATTERN)

    def is_examination(self) -> bool:
        return self.session_mode in ("assessment", "examination")

    def turn_context(self) -> dict:
        """The fields that were sent, for the interpreter's context (never the request id: it means nothing to a model)."""
        return {k: v for k, v in (("addressee", self.addressee), ("interaction_mode", self.interaction_mode), ("session_mode", self.session_mode)) if v}
