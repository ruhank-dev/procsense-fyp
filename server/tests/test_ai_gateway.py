"""The AI gateway (p1-backend-s06) on a real PostgreSQL, with a fake model host: who may call, what is audited, and what the caller gets when the model is down, slow, busy or wrong."""
from __future__ import annotations

import json
import threading
import uuid

import httpx
import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server.config import get_settings
from server.llm import client as client_mod
from server.llm.validators import VALIDATORS
from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
from server.routers import ai_gateway

pytestmark = pytest.mark.db
LINE = "I take 5 mg of amlodipine every day for my blood pressure."
VARS = {"style": "brief", "age": "60", "sex_word": "man", "topic": "medicines", "original": LINE}


def host(answer):
    """A fake Ollama: `answer(original) -> str` is the model's JSON text, or an exception to raise."""
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": "qwen3.5:4b-q4_K_M", "digest": "56671c2ab9385f9c"}]})
        original = json.loads(request.content)["messages"][1]["content"].split("Original line: ", 1)[1].splitlines()[0]
        out = answer(original)
        if isinstance(out, Exception):
            raise out
        return httpx.Response(200, json={"message": {"content": out}, "eval_count": 9})
    return client_mod.OllamaClient(transport=httpx.MockTransport(handler))


@pytest.fixture
def world(api):
    def build(s: DbSession):
        a, b = accounts.create_organisation(s, "Alpha", "alpha-org"), accounts.create_organisation(s, "Beta", "beta-org")
        me, other = api.person(s), api.person(s)
        accounts.add_membership(s, me, a.id, "trainee")
        accounts.add_membership(s, other, b.id, "trainee")
        return a.id, b.id, me, other
    a, b, me, other = api.seed(build)

    class W:
        pass
    w = W()
    w.api, w.a, w.b, w.me, w.other = api, a, b, me, other
    w.use = lambda client: api.client.app.dependency_overrides.__setitem__(ai_gateway.get_llm_client, lambda: client)
    w.use(host(lambda o: json.dumps({"text": "Every day I take 5 mg of amlodipine for my blood pressure."})))
    w.post = lambda body=None, who=me, org=a: api.client.post(f"/llm/organisations/{org}/generate", json=body or {"prompt_id": "compose.narrative", "variables": VARS}, headers=api.headers(who))
    yield w
    api.client.app.dependency_overrides.clear()


def rows(api) -> list[AiCall]:
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_every_ai_route_of_the_application_starts_an_audit_record(world):
    assert audit.unaudited_ai_routes(world.api.client.app) == []


def test_a_member_gets_a_checked_answer_and_the_audit_holds_no_text(world):
    r = world.post()
    assert r.status_code == 200, r.text
    body = r.json()
    assert (body["ok"], body["fallback"], body["model"], body["prompt"], body["attempts"]) == (True, False, "qwen3.5:4b-q4_K_M", "compose.narrative@2", 1)
    assert "amlodipine" in body["value"]["text"]
    (row,) = rows(world.api)
    assert (row.task, row.validation, row.fallback, row.model, row.prompt_id, row.organisation_id, row.user_id) == ("llm_generate", "passed", False, "qwen3.5:4b-q4_K_M", "compose.narrative:v2", world.a, world.me)
    assert row.model_version == "56671c2ab938"
    assert "amlodipine" not in json.dumps([row.input_hash, row.output_ref, row.prompt_id, row.context_id])
    assert uuid.UUID(r.headers["x-ai-call-id"]) == row.id


def test_someone_from_another_organisation_or_nobody_is_refused(world):
    assert world.post(who=world.other).status_code in (403, 404)
    assert world.api.client.post(f"/llm/organisations/{world.a}/generate", json={"prompt_id": "compose.narrative", "variables": VARS}).status_code in (401, 403)
    assert rows(world.api) == []


def test_only_registered_prompts_with_a_validator_and_the_right_variables_are_callable(world, monkeypatch):
    assert world.post({"prompt_id": "no.such", "variables": {}}).status_code == 404
    assert world.post({"prompt_id": "compose.narrative", "variables": {"style": "x"}}).status_code == 422
    monkeypatch.delitem(VALIDATORS, "compose.narrative")
    assert world.post().status_code == 403                                  # no check, no route


def test_a_model_that_is_down_gives_the_fallback_signal_and_not_an_error(world):
    world.use(host(lambda o: httpx.ConnectError("down")))
    r = world.post()
    assert r.status_code == 200
    assert (r.json()["ok"], r.json()["fallback"], r.json()["value"]) == (False, True, None)
    (row,) = rows(world.api)
    assert (row.fallback, row.fallback_reason, row.validation) == (True, "UNREACHABLE", "failed")


def test_no_model_host_configured_is_the_fallback_signal(world):
    world.use(None)
    assert world.post().json()["fallback"] is True
    assert rows(world.api)[0].fallback_reason == "MODEL_HOST_OFF"


def test_an_answer_that_changes_a_number_is_rejected_after_one_retry_and_the_reasons_are_codes(world):
    calls = []

    def wrong(original):
        calls.append(original)
        return json.dumps({"text": "I take 10 mg of amlodipine every day for my blood pressure."})
    world.use(host(wrong))
    r = world.post()
    assert (r.json()["ok"], r.json()["fallback"], r.json()["attempts"]) == (False, True, 2)
    assert "NUMBERS" in r.json()["reasons"]
    assert len(calls) == 2
    (row,) = rows(world.api)
    assert (row.validation, row.reasons[:1], row.fallback) == ("failed", ["NUMBERS"], True)


def test_the_one_model_slot_is_held_by_one_call_at_a_time(world, monkeypatch):
    monkeypatch.setenv("AI_QUEUE_WAIT_SECONDS", "0.2")
    get_settings.cache_clear()
    held = ai_gateway._slot.acquire()
    try:
        r = world.post()
    finally:
        ai_gateway._slot.release()
    assert held and r.json()["fallback"] is True
    assert rows(world.api)[0].fallback_reason == "MODEL_BUSY"
    assert ai_gateway._slot.acquire(blocking=False)                          # released: the next call can have it
    ai_gateway._slot.release()


def test_too_many_calls_in_an_hour_from_one_person_are_refused(world, monkeypatch):
    monkeypatch.setenv("AI_RATE_PER_HOUR", "2")
    get_settings.cache_clear()
    ai_gateway._limiters.clear()
    assert [world.post().status_code for _ in range(3)] == [200, 200, 429]


def test_concurrent_calls_all_get_an_answer_one_at_a_time(world):
    results = []

    def go():
        results.append(world.post().json()["ok"])
    threads = [threading.Thread(target=go) for _ in range(4)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    assert results == [True] * 4
    assert len(rows(world.api)) == 4


def test_the_health_route_says_whether_the_model_host_answers(world):
    assert world.api.client.get("/ai/health").json()["llm"]["up"] is True
    world.use(host(lambda o: "x"))
    world.api.client.app.dependency_overrides[ai_gateway.get_llm_client] = lambda: client_mod.OllamaClient(transport=httpx.MockTransport(lambda r: (_ for _ in ()).throw(httpx.ConnectError("x"))))
    assert world.api.client.get("/ai/health").json()["llm"]["up"] is False
    world.use(None)
    assert world.api.client.get("/ai/health").json()["llm"] == {"up": False, "configured": False}
