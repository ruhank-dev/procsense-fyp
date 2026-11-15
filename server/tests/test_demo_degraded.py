"""The degraded demonstration profile (P3-VOX-11, owner decision D2): with the demonstration profile on but no model host reachable (the laptop's internet unplugged and no local model answering), every voice
route still answers at once with its local fallback (the grammar reads orders, the speech action gives no action so the tablet stays, the patient says the approved line) and says why. Run by
`task demo-degraded-check`; the laptop-unreachable half (the headset sees a refused connection) is test_lan_integration.py::test_reconnect_after_server_restart_and_laptop_down."""
from __future__ import annotations

import time

import pytest

from server import config
from server.routers import dialogue as dialogue_router, speech_action as speech_router
from server.tests.test_verification import world  # noqa: F401

pytestmark = pytest.mark.db
DEAD_HOST = "http://127.0.0.1:9"          # the discard port: nothing listens, the connection is refused at once


@pytest.fixture
def offline(world, monkeypatch):  # noqa: F811
    monkeypatch.setenv("OLLAMA_URL", DEAD_HOST)
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "all")
    config.get_settings.cache_clear()
    for f in (dialogue_router.get_dialogue, speech_router.get_speech_actions):
        f.cache_clear()
    yield world
    config.get_settings.cache_clear()
    for f in (dialogue_router.get_dialogue, speech_router.get_speech_actions):
        f.cache_clear()


def _post(w, path, body):
    started = time.monotonic()
    r = w.api.client.post(f"/{path.format(org=w.org)}", json=body, headers=w.api.headers(w.trainee))
    return r, time.monotonic() - started


def test_orders_fall_back_to_the_grammar_and_say_why(offline):
    r, took = _post(offline, "orders/organisations/{org}/hear", {"text": "order a full blood count", "interaction_mode": "voice", "session_mode": "practice"})
    assert r.status_code == 200, r.text
    a = r.json()
    assert (a["status"], a["intent"], a["interpreter"]) == ("confirm", "ORDER_TEST", "offline_grammar") and a["fallback_reason"] and a["ticket"]
    assert took < 20


def test_a_spoken_action_gives_no_action_so_the_tablet_stays(offline):
    r, took = _post(offline, "speech/organisations/{org}/action", {"utterance": "check his pulse", "available": ["measure_pulse"], "interaction_mode": "voice"})
    assert r.status_code == 200, r.text
    a = r.json()
    assert (a["actions"], a["source"]) == ([], "fallback") and a["reasons"]
    assert took < 25


def test_the_patient_answers_with_the_approved_line(offline):
    body = {"context": {"case": "VAR-S1-A", "facts": [{"id": "pain", "value": "pain in the right lower abdomen", "disclosable_now": True}], "last_turns": []}, "question": "Where does it hurt?", "staff_roles": [], "authored": {"pain": "It hurts here."},
            "asked_fact": "pain", "interaction_mode": "voice"}
    r, took = _post(offline, "dialogue/organisations/{org}/team", body)
    assert r.status_code == 200, r.text
    patient = r.json()["replies"][0]
    assert (patient["role"], patient["source"]) == ("patient", "fallback") and patient["text"]
    assert took < 25
