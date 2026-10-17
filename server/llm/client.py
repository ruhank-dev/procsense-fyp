"""The one place a language model is called from (p1-backend-s06 gateway client, p1-ai-language-s02, ADR-0010).

    client = OllamaClient()                                         # http://127.0.0.1:11434 (not "localhost": +2 s per request on Windows)
    reply = client.chat("qwen3.5:4b-q4_K_M", system, user, schema=SCHEMA, think=False, timeout=20)
    reply.ok, reply.json, reply.text, reply.digest, reply.latency_ms, reply.error

Everything a caller needs to trust or distrust the answer is in the reply: whether it came, how long it took, which model file answered (the digest), and why it did not come. A model that is down, slow, or
answers with something that is not the JSON asked for gives `ok = False` and an `error` code; the caller then uses its fallback (a template, a pooled package, a pre-rendered line). No exception leaves this
module for an ordinary model failure. The client keeps no prompts and decides nothing: prompts live in prompts/ (prompts.py), validation in the callers.

Options follow ADR-0010: the 4B model for live work with thinking off, the 9B model for offline batch work; context 4096 unless a caller asks for more; sampling settings are the model card's non-thinking set.
"""
from __future__ import annotations

import json
import time
from dataclasses import dataclass, field
from datetime import datetime, timezone

import httpx

DEFAULT_URL = "http://127.0.0.1:11434"
LIVE_MODEL = "qwen3.5:4b-q4_K_M"
BATCH_MODEL = "qwen3.5:9b-q4_K_M"
SAMPLING = {"temperature": 0.7, "top_p": 0.8, "top_k": 20}
ERRORS = ("UNREACHABLE", "TIMEOUT", "HTTP_ERROR", "NOT_JSON", "EMPTY", "MODEL_MISSING")


@dataclass
class Reply:
    ok: bool
    text: str = ""
    json: object = None
    model: str = ""
    digest: str = ""
    latency_ms: int = 0
    prompt_tokens: int = 0
    output_tokens: int = 0
    error: str = ""
    detail: str = ""
    thinking: str = ""
    at: str = field(default_factory=lambda: datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"))


class OllamaClient:
    def __init__(self, base_url: str = DEFAULT_URL, transport: httpx.BaseTransport | None = None, default_timeout: float = 30.0):
        self.base_url = base_url.rstrip("/")
        self._http = httpx.Client(base_url=self.base_url, transport=transport, timeout=default_timeout)
        self._digests: dict[str, str] = {}

    def close(self) -> None:
        self._http.close()

    # ---- state ------------------------------------------------------------------------------------------
    def models(self) -> dict[str, str]:
        """name -> digest of what is installed; {} when the host does not answer."""
        try:
            r = self._http.get("/api/tags", timeout=5)
            r.raise_for_status()
            self._digests = {m["name"]: m.get("digest", "") for m in r.json().get("models", [])}
        except (httpx.HTTPError, ValueError, KeyError):
            return {}
        return dict(self._digests)

    def health(self) -> dict:
        """What /health reports for the model host: up, the models it holds, and the time the answer took."""
        t = time.monotonic()
        models = self.models()
        return {"up": bool(models), "models": sorted(models), "ms": round((time.monotonic() - t) * 1000)}

    # ---- calls ------------------------------------------------------------------------------------------
    def chat(self, model: str, system: str, user: str, schema: dict | None = None, think: bool = False, timeout: float | None = None, num_predict: int = 400,
             num_ctx: int = 4096, options: dict | None = None, keep_alive: str = "30m") -> Reply:
        body = {"model": model, "stream": False, "think": think, "keep_alive": keep_alive,
                "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}],
                "options": {**SAMPLING, "num_predict": num_predict, "num_ctx": num_ctx, **(options or {})}}
        if schema is not None:
            body["format"] = schema
        started = time.monotonic()
        try:
            r = self._http.post("/api/chat", json=body, timeout=timeout)
        except httpx.TimeoutException:
            return Reply(False, model=model, latency_ms=self._ms(started), error="TIMEOUT", detail=f"no answer within {timeout or self._http.timeout.read} s")
        except httpx.HTTPError as e:
            return Reply(False, model=model, latency_ms=self._ms(started), error="UNREACHABLE", detail=type(e).__name__)
        latency = self._ms(started)
        if r.status_code == 404:
            return Reply(False, model=model, latency_ms=latency, error="MODEL_MISSING", detail=r.text[:200])
        if r.status_code >= 400:
            return Reply(False, model=model, latency_ms=latency, error="HTTP_ERROR", detail=f"{r.status_code} {r.text[:200]}")
        try:
            doc = r.json()
            content = (doc.get("message") or {}).get("content", "")
        except ValueError:
            return Reply(False, model=model, latency_ms=latency, error="NOT_JSON", detail="the host's answer is not JSON")
        digest = self._digests.get(model) or self.models().get(model, "")
        reply = Reply(True, text=content, model=model, digest=digest, latency_ms=latency, prompt_tokens=int(doc.get("prompt_eval_count", 0)), output_tokens=int(doc.get("eval_count", 0)),
                      thinking=(doc.get("message") or {}).get("thinking", "") or "")
        if not content.strip():
            reply.ok, reply.error, reply.detail = False, "EMPTY", "the model returned no text (a thinking model may have used its whole token budget)"
            return reply
        if schema is not None:
            try:
                reply.json = json.loads(content)
            except ValueError:
                reply.ok, reply.error, reply.detail = False, "NOT_JSON", content[:200]
        return reply

    @staticmethod
    def _ms(started: float) -> int:
        return round((time.monotonic() - started) * 1000)
