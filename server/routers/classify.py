"""Classification route (p1-ai-language-s07): `POST /classify/organisations/{id}` with {"text"}: the act of one sentence, whether it counts as evidence, never for a critical act. Signed-in member, audited
(hash of the text, the act and the confidence as codes), rate limited with the other AI calls, one model slot. Without a model the answer is `source: unavailable` and nothing is credited.
"""
from __future__ import annotations

from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from server import ai_features
from server.classify.classifier import ActClassifier, Result
from server.classify.ensemble import EnsembleActClassifier
from server.classify.embedding import EmbeddingActClassifier, HeadError, OllamaEmbedder, load_head
from server.classify.tfidf import TfidfActClassifier
from server.config import Settings, get_settings
from server.llm import client as client_mod
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway

router = APIRouter(tags=["classification"])


class ClassifyIn(BaseModel):
    text: str = Field(min_length=1, max_length=400)


class ClassifyOut(BaseModel):
    act: str
    confidence: float
    credited: bool
    critical: bool
    source: str
    error: str = ""
    refused: str = ""                 # the guard that removed credit from an otherwise eligible answer (HEADS_DISAGREE, NON_ASSERTIVE), empty otherwise


class _Down:
    """Stands in when the classifier cannot start (no head file, a head that does not fit the taxonomy): nothing is credited, the menu and action path decide."""
    model = None

    def __init__(self, why: str):
        self.why = why

    def classify(self, sentence: str) -> Result:
        return Result("NONE", 0.0, False, "unavailable", error=self.why)


@lru_cache
def get_classifier():
    s = get_settings()
    if s.classifier_backend == "llm":
        return ActClassifier(client_mod.OllamaClient(s.ollama_url) if s.ollama_url else None)
    try:
        if s.classifier_backend == "tfidf":
            return TfidfActClassifier()
        embedder = OllamaEmbedder(s.ollama_url, s.embed_model) if s.ollama_url else None
        if s.classifier_backend == "embeddings":
            return EmbeddingActClassifier(embedder, load_head())
        return EnsembleActClassifier(embedder, load_head())
    except (OSError, ValueError, KeyError, HeadError):
        return _Down("CLASSIFIER_HEAD_UNAVAILABLE")


@router.post("/classify/organisations/{organisation_id}", response_model=ClassifyOut)
def classify(body: ClassifyIn, call: AiCallRecord = Depends(audited("classify")), settings: Settings = Depends(get_settings), clf=Depends(get_classifier)) -> ClassifyOut:
    if get_settings().classifier_backend == "llm":
        call.set_prompt("classify.acts:v1")
    call.set_input(body.text)
    if not ai_features.enabled("classifier"):
        call.fell_back("FEATURE_OFF")
        return ClassifyOut(act="NONE", confidence=0.0, credited=False, critical=False, source="unavailable", error="FEATURE_OFF")
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        return ClassifyOut(act="NONE", confidence=0.0, credited=False, critical=False, source="unavailable", error="MODEL_BUSY")
    try:
        r = clf.classify(body.text)
    finally:
        ai_gateway._slot.release()
    call.set_model(clf.model or "none")
    if r.source == "unavailable":
        call.fell_back(r.error or "UNAVAILABLE")
    else:
        call.validated(True)
        call.set_output(ref=r.act)
    return ClassifyOut(act=r.act, confidence=round(r.confidence, 3), credited=r.credited, critical=r.critical, source=r.source, error=r.error, refused=getattr(r, "refused", ""))
