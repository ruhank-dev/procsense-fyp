"""Communication act classifier from sentence embeddings + logistic regression (p1-ai-language-s07; chosen over the language-model classifier by the benchmark in docs/research/classifier_benchmark).

    clf = EmbeddingActClassifier(OllamaEmbedder("http://127.0.0.1:11434"))
    r = clf.classify("Could you tell me about your last meal?")      # the same Result as server/classify/classifier.py: act, confidence, credited, source, critical, error

How it works: the sentence is embedded by a small local model (all-minilm:22m through Ollama's /api/embed, 384 numbers, 46 MB), the vector is length-normalised, and a logistic regression head turns it into one probability
per act. The head is a plain JSON file (content/assessment/act_head.json: classes, weights, intercepts, per-act credit thresholds tuned on the dev split only) made by `tools/bench/bench_classifier.py --export-head`;
using it needs no numpy and no scikit-learn. The confidence is the probability of the chosen act, which is better behaved than a number a language model writes about itself.

Safety, unchanged from the language-model classifier and checked at load time:
- the head must list exactly the acts of content/assessment/taxonomy.json, and the critical acts of the taxonomy must be exactly `critical_acts` of content/assessment/mapping.json (else it refuses to start);
- a CRITICAL act is recorded and never credited from speech, whatever its probability; NONE is never credited;
- an act is credited only if its probability reaches the threshold tuned on dev for it (an act without one is never credited);
- a sentence that touches critical content (server/classify/guard.py) is not credited under a non-critical act;
- neither is a sentence to which the head gives a critical-act probability of `critical_mass_max` or more (when the head carries that number: it is tuned on dev only, tools/bench/tune_mass.py);
- if the embedding service is down, slow or answers badly the result is `source: unavailable` and nothing is credited, so the menu and action path decide, as before.
This classifier adds evidence only; deterministic action-based scoring stays authoritative.
"""
from __future__ import annotations

import json
import math
from pathlib import Path
from typing import Callable

import httpx

try:
    from classify.classifier import Result, taxonomy
    from classify.guard import critical_cue
except ImportError:
    from server.classify.classifier import Result, taxonomy
    from server.classify.guard import critical_cue

ROOT = Path(__file__).resolve().parents[2]
HEAD = ROOT / "content" / "assessment" / "act_head.json"
MAPPING = ROOT / "content" / "assessment" / "mapping.json"


class HeadError(ValueError):
    """The head file does not fit the taxonomy or the mapping: refuse to classify rather than guess."""


class OllamaEmbedder:
    """Sentence -> vector through Ollama's /api/embed. Returns None when the service cannot answer."""

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


def load_head(path: Path = HEAD) -> dict:
    head = json.loads(path.read_text(encoding="utf-8"))
    tax = taxonomy()["acts"]
    ids = [a["id"] for a in tax]
    if sorted(head["classes"]) != sorted(ids):
        raise HeadError(f"the head's classes differ from the taxonomy: {sorted(set(head['classes']) ^ set(ids))}")
    critical = {a["id"] for a in tax if a["critical"]}
    listed = set(json.loads(MAPPING.read_text(encoding="utf-8"))["critical_acts"])
    if critical != listed:
        raise HeadError(f"taxonomy critical acts and mapping.json critical_acts disagree: {sorted(critical ^ listed)}")
    if len(head["coef"]) != len(ids) or any(len(row) != head["dimension"] for row in head["coef"]) or len(head["intercept"]) != len(ids):
        raise HeadError("the head's weight matrix does not fit its dimension")
    head["_critical"] = critical
    return head


class EmbeddingActClassifier:
    def __init__(self, embedder: Callable[[str], list[float] | None] | None, head: dict | None = None):
        self.embedder = embedder
        self.head = head if head is not None else load_head()
        self.critical: set[str] = self.head.get("_critical") or {a["id"] for a in taxonomy()["acts"] if a["critical"]}
        self.model = getattr(embedder, "model", "embeddings")

    def probabilities(self, vector: list[float]) -> dict[str, float]:
        h = self.head
        norm = math.sqrt(sum(x * x for x in vector)) or 1.0
        x = [v / norm for v in vector]
        logits = [sum(w * v for w, v in zip(row, x)) + b for row, b in zip(h["coef"], h["intercept"])]
        top = max(logits)
        exp = [math.exp(z - top) for z in logits]
        total = sum(exp)
        return {c: e / total for c, e in zip(h["classes"], exp)}

    def critical_mass(self, probs: dict[str, float]) -> float:
        """The probability the head gives to ANY critical act: how likely the sentence is to be critical content said in some way, whatever act it was named as."""
        return sum(p for c, p in probs.items() if c in self.critical)

    def credit_allowed(self, act: str, probability: float, sentence: str, mass: float | None = None) -> bool:
        if act in self.critical or act == "NONE":
            return False
        t = self.head["thresholds"].get(act)
        cap = self.head.get("critical_mass_max")                       # tuned on dev only (tools/bench/tune_mass.py); absent in a head made before it existed
        if cap is not None and mass is not None and mass >= cap:
            return False
        return t is not None and probability >= t and not critical_cue(sentence)

    def classify(self, sentence: str) -> Result:
        if self.embedder is None:
            return Result("NONE", 0.0, False, "unavailable", error="NO_MODEL")
        vec = self.embedder(sentence)
        if vec is None or len(vec) != self.head["dimension"]:
            return Result("NONE", 0.0, False, "unavailable", error="EMBEDDING_UNAVAILABLE")
        probs = self.probabilities(vec)
        act = max(probs, key=probs.get)
        return Result(act, probs[act], self.credit_allowed(act, probs[act], sentence, self.critical_mass(probs)), "embedding", act in self.critical)
