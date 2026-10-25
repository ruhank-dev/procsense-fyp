"""The recogniser on the card falls back to the processor (ADR-0027): when the card's libraries are missing or fail the clip is recognised again on the processor, once, and the service stays there. No database,
no card, no model: a fake faster_whisper stands in."""
from __future__ import annotations

import sys
import types
from types import SimpleNamespace

from server.asr import service as svc_mod
from server.asr.service import AsrService


def seg(text):
    return SimpleNamespace(text=text, avg_logprob=-0.2, words=[SimpleNamespace(word=w, start=0.1 * i, end=0.1 * i + 0.1, probability=0.9) for i, w in enumerate(text.split())])


def install_fake(monkeypatch, card_error="Library cublas64_12.dll is not found or cannot be loaded"):
    made = []

    class WhisperModel:
        def __init__(self, name, device="cpu", compute_type="int8"):
            self.device = device
            made.append((device, compute_type))

        def transcribe(self, audio, **kw):
            if self.device == "cuda" and card_error:
                def broken():
                    raise RuntimeError(card_error)
                    yield                                                                  # a generator: the error comes when the segments are read, as in CTranslate2
                return broken(), None
            return iter([seg("do you have any allergies")]), None
    monkeypatch.setitem(sys.modules, "faster_whisper", types.SimpleNamespace(WhisperModel=WhisperModel))
    monkeypatch.setattr(svc_mod, "decode", lambda audio: ((b"RIFF" + b"\0" * 80, 1.0), ""))
    monkeypatch.setattr(svc_mod, "add_cuda_dlls", lambda: None)
    return made


def test_a_card_that_fails_to_compute_hands_the_clip_to_the_processor_and_stays_there(monkeypatch):
    made = install_fake(monkeypatch)
    s = AsrService("small.en", "cuda", "int8_float16")
    r = s.transcribe(b"audio")
    assert r.ok and r.text == "do you have any allergies" and made == [("cuda", "int8_float16"), ("cpu", "int8")]
    assert s.device == "cpu" and "cublas" in s.fell_back_from_card
    assert s.transcribe(b"audio").ok and made == [("cuda", "int8_float16"), ("cpu", "int8")]       # the processor model is kept: no second attempt on the card


def test_a_working_card_is_used_and_an_unrelated_error_is_not_hidden(monkeypatch):
    made = install_fake(monkeypatch, card_error="")
    s = AsrService("small.en", "cuda", "int8_float16")
    assert s.transcribe(b"audio").ok and made == [("cuda", "int8_float16")] and s.fell_back_from_card == ""
    install_fake(monkeypatch, card_error="out of memory in the decoder")
    s2 = AsrService("small.en", "cuda", "int8_float16")
    try:
        s2.transcribe(b"audio")
        raise AssertionError("an error that is not about the card's libraries must not be swallowed")
    except RuntimeError as e:
        assert "out of memory" in str(e)
