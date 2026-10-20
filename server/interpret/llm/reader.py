"""The semantic reader: one utterance in, a validated `Reading` or a safe failure out. Bounded: at most `max_attempts` provider calls and a total wall budget; a retry happens only after a FORMAT rejection (fence, not JSON, text around the object, schema, truncated) or a transient transport error (timeout, HTTP 5xx), never after a semantic rejection, and
never changes the rubric (the retry adds one fixed sentence naming the format problem; no model text is echoed back). A reply that fails validation after the last attempt, a timeout, a 401/402/403/429, an unreachable host or an unconfigured provider is `UNAVAILABLE` or `INVALID`: the caller falls back, nothing is repaired and nothing is credited."""
from __future__ import annotations

import time
from dataclasses import dataclass, field

from server.interpret.llm import prompt as P
from server.interpret.llm.provider import Provider, ProviderResult
from server.interpret.llm.schema import SCHEMA_VERSION, Reading, json_schema
from server.interpret.llm.strict import Invalid, parse

RETRY_FORMAT = {"FENCED", "NOT_JSON", "SURROUNDING_TEXT", "SCHEMA", "TRUNCATED", "EMPTY", "DUPLICATE_KEY"}
RETRY_TRANSPORT = {"TIMEOUT"}
NO_RETRY_HTTP = {401, 402, 403, 429}                    # an authentication, billing or quota signal ends the request at once


@dataclass
class ReadOutcome:
    status: str                                          # VALID | INVALID | UNAVAILABLE
    reading: Reading | None = None
    codes: list[str] = field(default_factory=list)       # rejection or error codes, one per failed attempt
    attempts: int = 0
    prompt_tokens: int = 0
    output_tokens: int = 0
    wall_ms: int = 0
    model: str = ""
    schema_version: str = SCHEMA_VERSION


class SemanticReader:
    def __init__(self, provider: Provider, max_attempts: int = 2, budget_ms: int = 12000):
        self.provider, self.max_attempts, self.budget_ms = provider, max(1, max_attempts), budget_ms
        self._system = P.full_system_prompt()
        self._schema = json_schema(P.objective_ids())

    def read(self, context, utterance: str, key: str = "") -> ReadOutcome:
        out, retry_code, t0 = ReadOutcome(status="UNAVAILABLE"), "", time.perf_counter()
        for attempt in range(1, self.max_attempts + 1):
            left_ms = self.budget_ms - (time.perf_counter() - t0) * 1000
            if attempt > 1 and left_ms <= 0:
                break
            try:
                r: ProviderResult = self.provider.complete(self._system, P.user_prompt(context, utterance, retry_code), self._schema, key=key, timeout=max(0.5, left_ms / 1000))
            except Exception as e:                       # a provider that raises is unavailable, never a crash of the request
                out.codes.append("PROVIDER_RAISED:" + type(e).__name__)
                out.attempts = attempt
                break
            out.attempts, out.model = attempt, r.answered_by or out.model
            out.prompt_tokens += r.prompt_tokens
            out.output_tokens += r.output_tokens
            out.wall_ms += r.wall_ms
            if r.error:
                out.codes.append(r.error)
                out.status = "UNAVAILABLE"
                if r.status in NO_RETRY_HTTP or not (r.error in RETRY_TRANSPORT or (r.status >= 500)):
                    break
                retry_code = ""
                continue
            try:
                out.reading = parse(r.text, r.done_reason, utterance, P.objective_ids())
                out.status = "VALID"
                return out
            except Invalid as e:
                out.codes.append(e.code)
                out.status = "INVALID"
                if e.code not in RETRY_FORMAT:
                    break
                retry_code = e.code
        out.reading = None
        return out
