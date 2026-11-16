"""P3 contract fields through the real routes on PostgreSQL (P3-API-02, -05, -06): old and new clients are both answered, the request id is logged, a bad mode is refused, and a verified session's report
shows the hints and the verdict from the server's replay. Runs only with TEST_DATABASE_URL."""
from __future__ import annotations

import json
import logging
from pathlib import Path

import pytest

from server import request_ids
from server.routers import orders as orders_router
from server.tests.files import changed_log
from server.tests.test_verification import sweep, world  # noqa: F401  (the verification fixture: an organisation, a trainee, the stand-in engine)

pytestmark = pytest.mark.db
CONTRACTS = Path(__file__).resolve().parents[2] / "tests" / "contracts" / "p3"


def _fixture(name: str) -> dict:
    return json.loads((CONTRACTS / f"{name}.json").read_text(encoding="utf-8"))["body"]


@pytest.fixture
def voice(world):  # noqa: F811
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: None
    request_ids.RECENT.clear()
    yield world
    world.api.client.app.dependency_overrides.clear()


def _post(w, path: str, body: dict, **headers):
    return w.api.client.post(f"/{path.format(org=w.org)}", json=body, headers={**w.api.headers(w.trainee), **headers})


def test_old_and_new_order_clients_are_answered_alike_and_the_request_id_is_logged(voice, caplog):
    with caplog.at_level(logging.INFO, logger="procsense.requests"):
        old = _post(voice, "orders/organisations/{org}/hear", _fixture("voice_turn_legacy_client"))
        new = _post(voice, "orders/organisations/{org}/hear", _fixture("voice_turn_orders_hear"), **{"X-Request-Id": "99"})
        again = _post(voice, "orders/organisations/{org}/hear", _fixture("voice_turn_orders_hear"))
    assert old.status_code == new.status_code == again.status_code == 200, (old.text, new.text)
    assert (old.json()["status"], old.json()["intent"]) == (new.json()["status"], new.json()["intent"]) == ("confirm", "ORDER_TEST")
    lines = [r.getMessage() for r in caplog.records if r.name == "procsense.requests"]
    assert "route=orders_hear request_id=- " in lines[0] + " "
    assert "request_id=17 duplicate=false interaction_mode=voice session_mode=examination" in lines[1]        # the body's id, not the header's
    assert "request_id=17 duplicate=true" in lines[2]


def test_speech_and_dialogue_routes_take_the_new_fields_and_log_the_header_id(voice, caplog):
    with caplog.at_level(logging.INFO, logger="procsense.requests"):
        s = _post(voice, "speech/organisations/{org}/action", {"utterance": "check his pulse", "interaction_mode": "voice"}, **{"X-Request-Id": "41"})
        t = _post(voice, "dialogue/organisations/{org}/team", _fixture("voice_turn_dialogue_team"))
    assert s.status_code == 200 and t.status_code == 200, (s.text, t.text)
    lines = " | ".join(r.getMessage() for r in caplog.records if r.name == "procsense.requests")
    assert "route=speech_action request_id=41" in lines and "route=dialogue_team request_id=a1b2c3d4" in lines


def test_a_bad_mode_or_request_id_is_refused_before_anything_runs(voice):
    assert _post(voice, "orders/organisations/{org}/hear", _fixture("voice_turn_bad_interaction_mode")).status_code == 422
    assert _post(voice, "speech/organisations/{org}/action", _fixture("voice_turn_bad_request_id")).status_code == 422


def test_the_report_shows_hints_and_verdict_from_the_servers_replay(world, monkeypatch):  # noqa: F811
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")

    def two_hints(entries):
        for e in entries[1:3]:
            e["verb"], e["accepted"] = "request_hint", True
    sid = world.upload(lines=changed_log(two_hints))
    sweep(world)
    row = world.row(sid)
    assert row.status == "verified" and row.report_json["server_replay"] == {"hints_used": 2, "hints_charged": 0, "verdict": "SUCCESS", "end_state": "READY_SAFE", "source": "server_replay"}
    r = world.api.client.get(f"/organisations/{world.org}/sessions/{sid}/report", headers=world.api.headers(world.trainee))
    assert r.status_code == 200, r.text
    body = r.json()
    assert (body["hints_used"], body["hints_charged"], body["verdict"], body["verdict_source"]) == (2, 0, "SUCCESS", "server_replay")
