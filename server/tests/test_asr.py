"""The speech recognition route and service (p1-voice-input-s01, plan tests T-ASR2 to T-ASR6) with a fake recogniser: limits, low confidence, queueing, audit, fallback."""
from __future__ import annotations

import threading
import time
from types import SimpleNamespace

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server.asr import service as svc_mod
from server.asr.service import AsrService
from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
from server.routers import asr as asr_router

pytestmark = pytest.mark.db


def seg(text, logprob=-0.2):
    return SimpleNamespace(text=text, avg_logprob=logprob, words=[SimpleNamespace(word=w, start=0.1 * i, end=0.1 * i + 0.1, probability=0.9) for i, w in enumerate(text.split())])


class FakeModel:
    def __init__(self, segments, delay=0.0):
        self.segments, self.delay, self.calls = segments, delay, 0

    def transcribe(self, audio, **kw):
        self.calls += 1
        self.kw = kw
        time.sleep(self.delay)
        return iter(self.segments), None


def service(segments, delay=0.0):
    return AsrService(model_factory=lambda: FakeModel(segments, delay))


@pytest.fixture(autouse=True)
def fake_decode(monkeypatch):
    """Five seconds for any clip, 20 seconds for a clip that starts with LONG, an error for one that starts with BAD."""
    def decode(audio):
        if audio.startswith(b"BAD"):
            return None, "BAD_AUDIO"
        return (b"wav", 20.0 if audio.startswith(b"LONG") else 5.0), ""
    monkeypatch.setattr(svc_mod, "decode", decode)


def test_a_clip_is_transcribed_with_the_vocabulary_prompt_and_the_settings_of_the_plan():
    s = service([seg("Please check the lap tower")])
    r = s.transcribe(b"clip")
    assert (r.ok, r.text, r.low_confidence, r.duration) == (True, "Please check the lap tower", False, 5.0)
    assert [w[0] for w in r.words] == ["Please", "check", "the", "lap", "tower"]
    kw = s._model.kw
    assert (kw["beam_size"], kw["temperature"], kw["vad_filter"], kw["language"]) == (3, 0, True, "en")
    assert "Pre-operative checks" in kw["initial_prompt"]


def test_a_clip_over_15_seconds_and_non_audio_are_refused_without_calling_the_model():
    s = service([seg("x")])
    assert s.transcribe(b"LONG").error == "CLIP_TOO_LONG"
    assert s.transcribe(b"BAD").error == "BAD_AUDIO"
    assert s._model is None                                         # the model was never even loaded


def test_low_confidence_and_no_speech_are_flagged_and_not_credited():
    assert service([seg("mumble", -1.8)]).transcribe(b"c").low_confidence is True
    r = service([]).transcribe(b"c")
    assert (r.ok, r.error, r.low_confidence) == (False, "NO_SPEECH", True)


def test_concurrent_clips_queue_and_none_crashes():
    s = service([seg("ok")], delay=0.05)
    out = []
    threads = [threading.Thread(target=lambda: out.append(s.transcribe(b"c").ok)) for _ in range(6)]
    t = time.monotonic()
    for x in threads:
        x.start()
    for x in threads:
        x.join()
    assert out == [True] * 6
    assert time.monotonic() - t >= 6 * 0.05 * 0.9                    # one at a time
    assert s._model.calls == 6


def test_without_faster_whisper_the_service_says_so():
    s = AsrService()
    s.available = lambda: False                                       # type: ignore[method-assign]
    assert s.transcribe(b"c").error == "ASR_UNAVAILABLE"


@pytest.fixture
def world(api):
    def build(sess: DbSession):
        org = accounts.create_organisation(sess, "Alpha", "alpha-org")
        me = api.person(sess)
        accounts.add_membership(sess, me, org.id, "trainee")
        return org.id, me
    org, me = api.seed(build)
    api.client.app.dependency_overrides[asr_router.get_asr] = lambda: service([seg("Check the pulse oximeter")])

    class W:
        pass
    w = W()
    w.api, w.org, w.me = api, org, me
    w.post = lambda body=b"clip-bytes": api.client.post(f"/asr/organisations/{org}/transcribe", content=body, headers={**api.headers(me), "content-type": "application/octet-stream"})
    yield w
    api.client.app.dependency_overrides.clear()


def rows(api):
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_the_route_returns_text_and_audits_a_hash_not_the_clip(world):
    r = world.post()
    assert r.status_code == 200 and r.json()["text"] == "Check the pulse oximeter" and r.json()["fallback"] is False
    (row,) = rows(world.api)
    assert (row.task, row.prompt_id, row.validation, row.organisation_id) == ("asr", "asr:v1", "passed", world.org)
    assert len(row.input_hash) == 64 and "oximeter" not in row.output_ref


def test_the_route_refuses_empty_and_oversize_bodies_and_a_clip_that_is_too_long(world):
    assert world.post(b"").status_code == 422
    assert world.post(b"x" * (asr_router.MAX_BYTES + 1)).status_code == 413
    assert world.post(b"LONG clip").status_code == 422
    assert world.post(b"BAD clip").status_code == 422


def test_without_the_recogniser_the_route_gives_the_fallback_signal(world):
    s = AsrService()
    s.available = lambda: False                                       # type: ignore[method-assign]
    world.api.client.app.dependency_overrides[asr_router.get_asr] = lambda: s
    r = world.post()
    assert (r.status_code, r.json()["fallback"], r.json()["error"]) == (200, True, "ASR_UNAVAILABLE")
    assert rows(world.api)[0].fallback_reason == "ASR_UNAVAILABLE"


def test_every_ai_route_including_this_one_starts_an_audit_record(world):
    assert audit.unaudited_ai_routes(world.api.client.app) == []
