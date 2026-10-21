"""Request and response shapes of the speech evaluation route (p1-ai-language-s22, ADR-0025). The context is the engine's InterpretationContext (src/ProcSense.Core/Speech/InterpretationContext.cs), JSON in a fixed key order;
its keys are listed in server/schemas/interpretation_context.json, which the engine tests also read. The answer is the model's structured JUDGEMENT of the utterance, never a score and never credit: the engine turns it into evidence and communication credit."""
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
    verified_actions: list[str] = Field(default=[], max_length=40)


class EvaluateIn(BaseModel):
    context: ContextIn
    utterance: str = Field(min_length=1, max_length=400)
    utterance_id: str = Field(min_length=1, max_length=64)


class JudgedActOut(BaseModel):
    objective: str
    assertion: str
    actor: str
    recipient: str
    form: str
    performed_now: bool
    relevance: int
    correctness: int
    completeness: int
    evidence: str
    rationale: str


class MenuEntryOut(BaseModel):
    behaviour: str
    label: str
    actions: list[str]


class EvaluateOut(BaseModel):
    decision: str                                  # EVALUATED | UNAVAILABLE
    utterance_id: str = ""
    acts: list[JudgedActOut] = []                  # empty with EVALUATED = the speech addresses no objective
    claims_physical_action: bool = False           # an audit note: the speech claimed or required a physical or system action; never evidence that it happened
    physical_action: str = ""
    critical_topic: bool = False
    menu: list[MenuEntryOut] = []                  # UNAVAILABLE: the existing verified actions stay available
    reasons: list[str] = []
    source: str = "evaluate"
