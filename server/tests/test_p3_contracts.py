"""P3 integration contracts without a database (P3-API-02, -05, -06, -08, P3-NET-13): the shared JSON examples in tests/contracts/p3 (the Unity EditMode test P3ContractFixturesTests reads the same files),
the optional turn fields, request-id logging, the replay summary of hints and verdict, the OLLAMA_API_KEY name and the cloud concurrency setting."""
from __future__ import annotations

import json
import logging
from pathlib import Path

import pytest
from pydantic import ValidationError

from server import request_ids
from server.config import Settings
from server.llm import routing
from server.routers.dialogue import TeamIn
from server.routers.orders import HearIn, _context
from server.routers.speech_action import ActionIn
from server.schemas.dashboard import SessionReport
from server.tests.files import changed_log
from server.verification import replay_summary

CONTRACTS = Path(__file__).resolve().parents[2] / "tests" / "contracts" / "p3"
SCHEMAS = {"ActionIn": ActionIn, "HearIn": HearIn, "TeamIn": TeamIn, "SessionReport": SessionReport}
FIXTURES = sorted(CONTRACTS.glob("*.json"))


def test_there_are_shared_fixtures_for_every_schema():
    assert {json.loads(p.read_text(encoding="utf-8"))["schema"] for p in FIXTURES} == set(SCHEMAS)


@pytest.mark.parametrize("path", FIXTURES, ids=lambda p: p.stem)
def test_each_shared_fixture_is_accepted_or_refused_as_it_says(path):
    doc = json.loads(path.read_text(encoding="utf-8"))
    assert doc["name"] == path.stem
    model = SCHEMAS[doc["schema"]]
    if doc["valid"]:
        parsed = model.model_validate(doc["body"])
        for field in ("interaction_mode", "session_mode", "request_id", "addressee", "hints_used", "verdict"):
            if field in doc["body"]:
                assert getattr(parsed, field) == doc["body"][field]
    else:
        with pytest.raises(ValidationError):
            model.model_validate(doc["body"])


def test_an_old_client_sends_none_of_the_new_fields_and_nothing_changes():
    for model, body in ((ActionIn, {"utterance": "check his pulse"}), (HearIn, {"text": "order fbc"}), (TeamIn, {"context": {"facts": []}, "question": "hello"})):
        parsed = model.model_validate(body)
        assert (parsed.interaction_mode, parsed.session_mode, parsed.request_id) == (None, None, None)
        assert parsed.turn_context() == {}


def test_the_turn_fields_reach_the_order_interpreter_context():
    h = HearIn(text="order fbc", context={"case": "VAR-S1-A"}, addressee="nurse", interaction_mode="voice", session_mode="examination", request_id="9")
    assert _context(h) == {"case": "VAR-S1-A", "addressee": "nurse", "interaction_mode": "voice", "session_mode": "examination"}       # never the request id
    assert _context(HearIn(text="order fbc", context="the patient is in bed 3", interaction_mode="voice")) == "the patient is in bed 3 interaction_mode=voice"
    assert _context(HearIn(text="order fbc", context={"case": "X"})) == {"case": "X"}
    assert _context(HearIn(text="order fbc", interaction_mode="menu")) == {"interaction_mode": "menu"}


def test_an_examination_session_mode_makes_the_team_route_use_the_examination_policy_unless_mode_was_sent():
    assert TeamIn(context={"facts": []}, question="hi", session_mode="assessment").is_examination()
    explicit = TeamIn(context={"facts": []}, question="hi", mode="practice", session_mode="assessment")
    assert "mode" in explicit.model_fields_set                    # the route keeps an explicit practice mode


def test_request_ids_are_logged_with_duplicates_marked_and_without_words(caplog):
    request_ids.RECENT.clear()
    turn = HearIn(text="order a full blood count", request_id="abc-1", interaction_mode="voice")
    with caplog.at_level(logging.INFO, logger="procsense.requests"):
        assert request_ids.note("orders_hear", "user-1", turn, "55") == "abc-1"                     # the body's id wins over the header's
        assert request_ids.note("orders_hear", "user-1", turn, None) == "abc-1"
        assert request_ids.note("orders_hear", "user-2", turn, None) == "abc-1"                     # another person: not a duplicate
        assert request_ids.note("speech_action", "user-1", ActionIn(utterance="x"), "77") == "77"     # header only (LanHttp)
        assert request_ids.note("speech_action", "user-1", ActionIn(utterance="x"), "bad id!") == ""  # a malformed header is not logged
    lines = [r.getMessage() for r in caplog.records]
    assert "duplicate=false" in lines[0] and "duplicate=true" in lines[1] and "duplicate=false" in lines[2]
    assert "request_id=abc-1" in lines[0] and "interaction_mode=voice" in lines[0] and "request_id=77" in lines[3] and "request_id=-" in lines[4]
    assert not any("full blood count" in x or "user-1" in x for x in lines)


def test_the_recent_window_forgets_the_oldest():
    r = request_ids.RecentIds(size=2)
    assert not r.seen("a") and not r.seen("b") and not r.seen("c")
    assert not r.seen("a") and r.seen("c")


def _with_hints(n: int, accepted: bool = True):
    def edit(entries):
        for e in entries[:n]:
            e["verb"], e["accepted"] = "request_hint", accepted
    return "".join(line + "\n" for line in changed_log(edit))


def test_the_replay_summary_counts_accepted_hints_and_charges_them_in_the_examination_only():
    facts = {"result": "SUCCESS", "end_state": "READY_SAFE"}
    assert replay_summary.summarise(_with_hints(2), "assessment", facts) == {"hints_used": 2, "hints_charged": 2, "verdict": "SUCCESS", "end_state": "READY_SAFE", "source": "server_replay"}
    assert replay_summary.summarise(_with_hints(2), "practice", facts)["hints_charged"] == 0
    assert replay_summary.summarise(_with_hints(3, accepted=False), "assessment", facts)["hints_used"] == 0      # a refused hint (the limit) was not given
    assert replay_summary.read({"server_replay": {"hints_used": 1}}) == {"hints_used": 1}
    assert replay_summary.read(None) == {} and replay_summary.read({"schema": "debrief_facts.v1"}) == {}


def test_the_cloud_key_is_read_as_ollama_api_key_and_the_old_name_still_works(monkeypatch):
    monkeypatch.delenv("SPEECH_EVAL_API_KEY", raising=False)
    monkeypatch.setenv("OLLAMA_API_KEY", "k-new")
    assert Settings(_env_file=None).speech_eval_api_key.get_secret_value() == "k-new"
    monkeypatch.delenv("OLLAMA_API_KEY")
    monkeypatch.setenv("SPEECH_EVAL_API_KEY", "k-old")
    assert Settings(_env_file=None).speech_eval_api_key.get_secret_value() == "k-old"
    monkeypatch.setenv("OLLAMA_API_KEY", "k-new")
    s = Settings(_env_file=None)
    assert s.speech_eval_api_key.get_secret_value() == "k-new" and "k-new" not in repr(s) and "k-new" not in str(s.model_dump())


def test_the_cloud_slot_takes_its_size_from_the_settings(monkeypatch):
    from server import config
    for size in (1, 3):
        monkeypatch.setenv("CLOUD_CONCURRENCY", str(size))
        config.get_settings.cache_clear()
        routing.reset_cloud_slot()
        try:
            got = [routing.acquire_cloud("live") for _ in range(size + 1)]
            assert got == [True] * size + [False]
            for _ in range(size):
                routing.release_cloud()
        finally:
            routing.reset_cloud_slot()
    monkeypatch.delenv("CLOUD_CONCURRENCY")
    config.get_settings.cache_clear()
    assert Settings(_env_file=None).cloud_concurrency == 1          # the Free plan (ADR-0026) is the default
    with pytest.raises(ValidationError):
        Settings(_env_file=None, cloud_concurrency=0)
