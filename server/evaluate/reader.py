"""The evaluator's reader (p1-ai-language-s22, ADR-0025): one request to the model with a bounded retry on a format rejection or a transport error, a total wall budget, and an honest outcome. It never raises for an ordinary failure and never repairs a reply.
Fail-safe: any failure (no model, a time-out, a rejected reply twice, a 401/402/403/429) is an UNAVAILABLE or INVALID outcome; the caller then gives NO speech credit and the trainee keeps the menu actions. Credit is never guessed."""
from __future__ import annotations

import time
from dataclasses import dataclass, field

from server.evaluate import prompt as P
from server.evaluate.contract import RETRY_FORMAT, Evaluation, Invalid, parse
from server.evaluate.provider import Provider

NO_RETRY_HTTP = (401, 402, 403, 429)


@dataclass
class Outcome:
    status: str                                   # VALID | INVALID | UNAVAILABLE
    evaluation: Evaluation | None = None
    codes: list[str] = field(default_factory=list)
    attempts: int = 0
    prompt_tokens: int = 0
    output_tokens: int = 0
    wall_ms: int = 0
    model: str = ""
    raw: list[str] = field(default_factory=list)


class Reader:
    def __init__(self, provider: Provider, max_attempts: int = 2, budget_ms: int = 25000, call_timeout: float = 15.0):
        self.provider, self.max_attempts, self.budget_ms, self.call_timeout = provider, max(1, max_attempts), budget_ms, call_timeout
        self.system = P.full_system_prompt()

    def read(self, context: dict, utterance: str, key: str = "") -> Outcome:
        out, retry, t0 = Outcome(status="UNAVAILABLE"), "", time.perf_counter()
        for attempt in range(1, self.max_attempts + 1):
            left_ms = self.budget_ms - (time.perf_counter() - t0) * 1000
            if attempt > 1 and left_ms <= 0:
                break
            try:
                r = self.provider.complete(self.system, P.user_prompt(context, utterance, retry), None, key=key, timeout=max(0.5, min(self.call_timeout, left_ms / 1000)))
            except Exception as e:                     # a provider that raises is unavailable, never a crash of the request
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
                if r.status in NO_RETRY_HTTP or not (r.error == "TIMEOUT" or r.error.startswith("UNREACHABLE") or (r.status or 0) >= 500):
                    break
                retry = ""
                continue
            out.raw.append(r.text)
            try:
                out.evaluation = parse(r.text, r.done_reason, utterance)
                out.status = "VALID"
                return out
            except Invalid as e:
                out.codes.append(e.code + (":" + e.detail if e.detail else ""))
                out.status = "INVALID"
                if e.code not in RETRY_FORMAT:
                    break
                retry = e.code
        out.evaluation = None
        return out
