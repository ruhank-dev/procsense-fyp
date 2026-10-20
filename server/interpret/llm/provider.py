"""Providers of the semantic reader: Ollama Cloud (the selected platform, ADR-0024), a mock for tests and a replay of recorded replies for offline evaluation. A provider returns text and facts about the call; it never raises for an ordinary failure (a timeout, an HTTP error, an unreachable host are results with an error code).
Ollama Cloud: `POST {base}/api/chat`. Either a signed-in local Ollama relays a cloud model (base = the local URL, no key) or the direct API (`https://ollama.com`) with an API key sent as a Bearer token. The key is held as a secret, never logged and never put in a request body. The request carries only the system prompt and the user prompt
built by server/interpret/llm/prompt.py (approved definitions, the engine's context, the utterance); no credentials, no identifiers, no patient record."""
from __future__ import annotations

import json
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Protocol


@dataclass
class ProviderResult:
    text: str = ""
    done_reason: str | None = None
    error: str = ""                  # "", TIMEOUT, UNREACHABLE:<type>, HTTP_<n>, NOT_CONFIGURED, NO_REPLAY
    status: int = 0
    answered_by: str = ""
    prompt_tokens: int = 0
    output_tokens: int = 0
    wall_ms: int = 0


class Provider(Protocol):
    name: str

    def complete(self, system: str, user: str, schema: dict | None = None, key: str = "", timeout: float | None = None) -> ProviderResult: ...


class OllamaCloudProvider:
    name = "ollama-cloud"

    def __init__(self, model: str, base_url: str = "https://ollama.com", api_key: str = "", timeout: float = 8.0, think: str | None = "low", num_predict: int = 1024, seed: int = 7, transport: Callable | None = None):
        self.model, self.base_url, self._key, self.timeout, self.think, self.num_predict, self.seed = model, base_url.rstrip("/"), api_key, timeout, think, num_predict, seed
        self._transport = transport

    def _post(self, url: str, body: dict, headers: dict, timeout: float):
        if self._transport is not None:
            return self._transport(url, body, headers, timeout)
        import httpx
        return httpx.post(url, json=body, headers=headers, timeout=timeout)

    def complete(self, system: str, user: str, schema: dict | None = None, key: str = "", timeout: float | None = None) -> ProviderResult:
        if not self.model or not self.base_url:
            return ProviderResult(error="NOT_CONFIGURED")
        body = {"model": self.model, "stream": False, "messages": [{"role": "system", "content": system}, {"role": "user", "content": user}], "options": {"temperature": 0, "seed": self.seed, "num_predict": self.num_predict}}
        if schema is not None:
            body["format"] = schema                 # sent for grounding and for hosts that honour it; the cloud route does not enforce it, so every reply is validated anyway
        if self.think:
            body["think"] = self.think
        headers = {"Authorization": "Bearer " + self._key} if self._key else {}
        t = time.perf_counter()
        try:
            r = self._post(self.base_url + "/api/chat", body, headers, timeout or self.timeout)
        except Exception as e:                                          # timeouts and transport errors are results
            name = type(e).__name__
            return ProviderResult(error="TIMEOUT" if "Timeout" in name else "UNREACHABLE:" + name, wall_ms=round((time.perf_counter() - t) * 1000))
        wall = round((time.perf_counter() - t) * 1000)
        if r.status_code >= 400:
            return ProviderResult(error=f"HTTP_{r.status_code}", status=r.status_code, wall_ms=wall)
        try:
            d = r.json()
        except ValueError:
            return ProviderResult(error="BAD_BODY", status=r.status_code, wall_ms=wall)
        m = d.get("message") or {}
        return ProviderResult(text=m.get("content", "") or "", done_reason=d.get("done_reason"), answered_by=d.get("model", ""), prompt_tokens=d.get("prompt_eval_count", 0), output_tokens=d.get("eval_count", 0), wall_ms=wall)


class MockProvider:
    """Deterministic test provider: replies in order (a str, a ProviderResult, or an exception to raise), then repeats the last one."""
    name = "mock"

    def __init__(self, replies):
        self.replies, self.calls = list(replies), []

    def complete(self, system: str, user: str, schema: dict | None = None, key: str = "", timeout: float | None = None) -> ProviderResult:
        self.calls.append({"system": system, "user": user, "key": key})
        r = self.replies[min(len(self.calls) - 1, len(self.replies) - 1)]
        if isinstance(r, Exception):
            raise r
        if isinstance(r, ProviderResult):
            return r
        return ProviderResult(text=r, done_reason="stop", answered_by="mock", wall_ms=1)


class ReplayProvider:
    """Serves the recorded raw replies of an evaluation run (artifacts/ai-bench/rubric/cloud-*.jsonl: {"id", "raw"}) by utterance id, so the integrated pipeline can be evaluated offline at no cost. Tests the policy and the validator, not the context effect (the recordings used the bare prompt)."""
    name = "replay"

    def __init__(self, path: Path | str):
        self.rows = {}
        for line in Path(path).read_text(encoding="utf-8").splitlines():
            if line.strip():
                d = json.loads(line)
                self.rows[d["id"]] = d["raw"]

    def complete(self, system: str, user: str, schema: dict | None = None, key: str = "", timeout: float | None = None) -> ProviderResult:
        raw = self.rows.get(key)
        if raw is None:
            return ProviderResult(error="NO_REPLAY")
        if "error" in raw:
            return ProviderResult(error=raw["error"], wall_ms=raw.get("wall_ms", 0))
        return ProviderResult(text=raw.get("content", ""), done_reason=raw.get("done_reason"), answered_by=raw.get("answered_by", ""), prompt_tokens=raw.get("prompt_tokens", 0), output_tokens=raw.get("output_tokens", 0), wall_ms=raw.get("wall_ms", 0))
