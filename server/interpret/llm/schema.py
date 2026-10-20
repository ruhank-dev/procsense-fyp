"""The canonical, versioned response schema of the semantic reader (Pydantic). Same contract as the evaluated rubric experiment (tools/bench/rubric_eval.py): per objective a verdict, actor, modality, evidence copied from the utterance and a reason code, at most two findings.
The Ollama Cloud route does NOT enforce this schema (official documentation: "Ollama's cloud currently does not support structured outputs"; reproduced on 20 Oct 2026 with `format` and with the OpenAI-style `response_format` on gemma4:31b), so the schema is sent to ground the model and the reply is validated here, strictly, every time."""
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, ConfigDict, Field

SCHEMA_VERSION = "semantic-reading-1"
Verdict = Literal["SUPPORTED", "NOT_SUPPORTED", "UNCERTAIN"]
Actor = Literal["trainee", "other", "unclear"]
Modality = Literal["actual", "negated", "intended", "hypothetical", "reported", "incomplete"]
Reason = Literal["MEETS_RUBRIC", "DENIED", "FUTURE_INTENT", "HYPOTHETICAL", "REPORTED", "OTHER_SPEAKER", "BROKEN_OFF", "WRONG_CONTENT", "DAMAGED_SPEECH", "AMBIGUOUS"]
MAX_FINDINGS = 2
MAX_EVIDENCE = 120


class Finding(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)
    objective: str = Field(min_length=1, max_length=40)
    verdict: Verdict
    actor: Actor
    modality: Modality
    evidence: str = Field(max_length=MAX_EVIDENCE)
    reason: Reason


class Reading(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)
    findings: list[Finding] = Field(max_length=MAX_FINDINGS)


def json_schema(objective_ids: list[str]) -> dict:
    """The JSON Schema sent with the request (Reading's own schema, with the objective restricted to the approved identifiers)."""
    s = Reading.model_json_schema()
    s["$defs"]["Finding"]["properties"]["objective"] = {"enum": list(objective_ids), "type": "string"}
    s["x-schema-version"] = SCHEMA_VERSION
    return s
