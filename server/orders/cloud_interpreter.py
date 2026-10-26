"""The spoken-order interpreter as the demonstration and the service use it (ADR-0028, amended 27 Oct 2026): Gemma 4 31B Cloud is the preferred and default reader of a sentence; the offline grammar is only its fallback.

    ci = CloudOnlyInterpreter(client)
    it = ci.interpret(text, vocab, context)      # an Interpretation from the cloud model, or None
    ci.last                                      # which system answered and why: {"interpreter": "gemma_cloud" | "offline_grammar", "model", "reported_model", "reason", "ms"}

What was evaluated and accepted is Gemma reading the sentence (97.4 percent on the held-out set); the local model with the same prompt scored 69.5 percent with 13 percent wrong items, so its answer is never used. Whenever the cloud is not used or does not answer, `interpret` returns None and the caller
(`ReadBack.hear`) falls back to the grammar with its question guard. The reason is always recorded, never the words of the trainee:
  CLOUD_ROUTING_OFF   the operator's switch CLOUD_ROUTING_ENABLED is off
  PRIVACY_GATE        the sentence is trainee-class data and the institutional approval is not recorded (and the deployment is not declared synthetic)
  NO_CONSENT          the approval is recorded but this person has not given the optional cloud consent
  CLOUD_BUSY          the plan's one cloud request is taken by another call
  CLOUD_TIMEOUT / CLOUD_UNREACHABLE / CLOUD_LIMIT / CLOUD_ERROR   the cloud call did not answer in the live budget (4 s) or failed (outage, usage limit)
  CLOUD_INVALID       the cloud answer failed the schema or the catalogue check
No part of the safety path is here: the catalogue and intent checks are `OrderInterpreter.check`, the read-back and the signed single-use ticket are in `grammar` and `ticket`, and the engine still validates the action.
"""
from __future__ import annotations

try:
    from llm import gateway, routing
    from orders.interpreter import Interpretation, OrderInterpreter, catalogue, context_text, schema
except ImportError:
    from server.llm import gateway, routing
    from server.orders.interpreter import Interpretation, OrderInterpreter, catalogue, context_text, schema

TASK = "orders_interpret"
ROUTE_REASONS = {"switch_off": "CLOUD_ROUTING_OFF", "privacy_gate": "PRIVACY_GATE", "no_consent": "NO_CONSENT", "no_route": "NO_ROUTE", "route": "ROUTE_LOCAL"}
ERROR_REASONS = {"TIMEOUT": "CLOUD_TIMEOUT", "UNREACHABLE": "CLOUD_UNREACHABLE", "CLOUD_BUSY": "CLOUD_BUSY", "HTTP_ERROR": "CLOUD_LIMIT", "MODEL_MISSING": "CLOUD_UNREACHABLE"}


def reason_of(attempts: list[dict]) -> str:
    """Why the cloud did not answer, from the gateway's record of the attempt."""
    a = attempts[-1] if attempts else {}
    err = a.get("error", "")
    if err.startswith("route:"):
        return ROUTE_REASONS.get(err[6:], "CLOUD_NOT_USED")
    if a.get("defects"):
        return "CLOUD_INVALID"
    return ERROR_REASONS.get(err, "CLOUD_ERROR" if err else "CLOUD_INVALID")


class CloudOnlyInterpreter(OrderInterpreter):
    def __init__(self, client, model: str | None = None, timeout: float | None = None):
        super().__init__(client, model, timeout)
        self.last: dict = {"interpreter": "offline_grammar", "model": "", "reported_model": "", "reason": "NOT_ASKED", "ms": 0}

    def interpret(self, transcript, vocab, context=None, dose_removed=False) -> Interpretation | None:
        self.last = {"interpreter": "offline_grammar", "model": "", "reported_model": "", "reason": "NOT_ASKED", "ms": 0}
        if self.client is None or not transcript.strip():
            self.last["reason"] = "NO_MODEL_HOST" if self.client is None else "EMPTY"
            return None
        items, by_id = catalogue(vocab)
        r = gateway.run(self.client, "orders.interpret", {"heard": transcript.strip(), "items": items, "context": context_text(context) + (" A dose was spoken; it is ignored." if dose_removed else "")},
                        schema=schema(list(by_id)), timeout=self.timeout, retries=0, task=TASK, cloud_only=True)
        a = r.attempts[-1] if r.attempts else {}
        self.last["ms"] = sum(x.get("latency_ms", 0) for x in r.attempts)
        if not r.ok:
            self.last["reason"] = reason_of(r.attempts)
            return None
        it = self.check(r.value, by_id, model=a.get("model", ""))
        if it is None or not routing.is_cloud(it.model):
            self.last["reason"] = "CLOUD_INVALID"
            return None
        self.last.update(interpreter="gemma_cloud", model=it.model, reported_model=a.get("reported_model", ""), reason="")
        return it
