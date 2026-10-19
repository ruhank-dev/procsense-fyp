"""Request and response shapes of the interpretation route (p1-ai-language-s11, ADR-0022). The context is the engine's InterpretationContext (src/ProcSense.Core/Speech/InterpretationContext.cs), JSON in a fixed key order;
its keys are listed in server/schemas/interpretation_context.json, which the engine tests also read."""
from __future__ import annotations

from typing import Annotated

from pydantic import BaseModel, Field


class EligibleActIn(BaseModel):
    act: str = Field(min_length=1, max_length=40)
    behaviour: str = Field(min_length=1, max_length=8)
    critical: bool = False


class TurnIn(BaseModel):
    speaker: str = Field(max_length=24)
    text: str = Field(max_length=300)


class ContextIn(BaseModel):
    case: str = Field(max_length=64)
    scenario: str = Field(max_length=64)
    mode: str = Field(pattern="^(practice|assessment)$")
    steps: list[Annotated[int, Field(ge=1, le=15)]] = Field(max_length=15)
    eligible: list[EligibleActIn] = Field(max_length=60)
    accepted: list[str] = Field(default=[], max_length=30)
    patient_question_pending: bool = False
    readback_pending: bool = False
    last_turns: list[TurnIn] = Field(default=[], max_length=4)
    speaker: str = Field(default="trainee", max_length=24)
    addressee: str = Field(default="patient", max_length=24)


class InterpretIn(BaseModel):
    context: ContextIn
    utterance: str = Field(min_length=1, max_length=400)
    utterance_id: str = Field(min_length=1, max_length=64)
    clarifications_used: int = Field(default=0, ge=0, le=3)


class EvidenceOut(BaseModel):
    act: str
    behaviour: str
    span: str
    status: str
    confidence: float
    utterance_id: str


class MenuEntryOut(BaseModel):
    behaviour: str
    label: str
    actions: list[str]


class InterpretOut(BaseModel):
    decision: str                                  # OFFER_EVIDENCE | CLARIFY | ABSTAIN | MENU
    evidence: EvidenceOut | None = None
    clarify: str = ""                              # the one question (CLARIFY only), built from approved wording
    options: list[str] = []                        # the acts the question names (CLARIFY only)
    menu: list[MenuEntryOut] = []
    points_to: list[str] = []                      # for a critical topic: the verified actions that earn the credit
    reasons: list[str] = []
    source: str = "interpret"
