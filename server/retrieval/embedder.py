"""Sentence -> vector through Ollama's /api/embed (the retrieval index of the patient-dialogue facts, tools/ai/eval_retrieval.py). Returns None when the service cannot answer."""
from __future__ import annotations

import httpx


class OllamaEmbedder:
    def __init__(self, base_url: str, model: str = "all-minilm:22m", timeout: float = 10.0, transport: httpx.BaseTransport | None = None):
        self.model = model
        self._http = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout, transport=transport)

    def __call__(self, text: str) -> list[float] | None:
        try:
            r = self._http.post("/api/embed", json={"model": self.model, "input": [text], "keep_alive": "30m"})
            r.raise_for_status()
            vec = r.json()["embeddings"][0]
        except (httpx.HTTPError, ValueError, KeyError, IndexError):
            return None
        return vec if isinstance(vec, list) and vec else None
