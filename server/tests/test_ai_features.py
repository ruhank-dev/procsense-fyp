"""The server's reading of the AI feature gate (server/ai_features.py; p1-ai-language-s09): it reads content/ai/features.json, which tools/eval/ai_gate.py writes from the committed results, and the development override can force features on."""
from __future__ import annotations

import json
from pathlib import Path

from server import ai_features, config

ROOT = Path(__file__).resolve().parents[2]


def test_the_server_reads_the_gate_file_and_an_override_forces_features_on(monkeypatch):
    monkeypatch.delenv("AI_FEATURES_OVERRIDE", raising=False)
    config.get_settings.cache_clear()
    ai_features._file.cache_clear()
    gate = json.loads((ROOT / "content" / "ai" / "features.json").read_text(encoding="utf-8"))["features"]
    thresholds = json.loads((ROOT / "content" / "ai" / "thresholds.json").read_text(encoding="utf-8"))["features"]
    assert {k for k in thresholds if ai_features.enabled(k)} == {k for k, v in gate.items() if v["enabled"]}
    assert ai_features.enabled("speech_evaluation")                         # on since the final acceptance (ADR-0025): the gate reads the two frozen acceptance reports
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "speech_evaluation, tts")
    config.get_settings.cache_clear()
    assert ai_features.enabled("speech_evaluation") and ai_features.enabled("tts")
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "all")
    config.get_settings.cache_clear()
    assert all(ai_features.enabled(k) for k in thresholds)
    monkeypatch.delenv("AI_FEATURES_OVERRIDE", raising=False)
    config.get_settings.cache_clear()
    ai_features._file.cache_clear()
