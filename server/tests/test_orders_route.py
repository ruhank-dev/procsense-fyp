"""The spoken-order route (server/routers/orders.py; features C32, C35): a transcript becomes a read-back and a signed ticket, nothing is released before a yes, a forged or replayed ticket is refused, the model fallback only
suggests, and every call is audited. A real PostgreSQL, fake model host."""
from __future__ import annotations

import json

import httpx
import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server import ai_features, config
from server.llm import client as client_mod
from server.middleware import audit
from server.models.ai_call import AiCall
from server.orders.llm_fallback import OrderMapper
from server.repositories import accounts
from server.routers import orders as orders_router

pytestmark = pytest.mark.db


def host(answer):
    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        if '"kind"' in json.dumps(json.loads(request.content).get("format", {})):                   # the kind stage of the mapper: answered consistently with the item the test wants chosen
            tgt = answer.get("target", "NONE")
            return httpx.Response(200, json={"message": {"content": json.dumps({"kind": tgt.split(":")[0] if ":" in tgt else "NONE"})}})
        return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


@pytest.fixture
def world(api):
    def build(s: DbSession):
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        me = api.person(s)
        other = api.person(s)
        accounts.add_membership(s, me, org.id, "trainee")
        accounts.add_membership(s, other, org.id, "trainee")
        return org.id, me, other
    org, me, other = api.seed(build)

    class W:
        pass
    w = W()
    w.api, w.org, w.me, w.other = api, org, me, other
    w.post = lambda action, body, who=None: api.client.post(f"/orders/organisations/{org}/{action}", json=body, headers=api.headers(who or me))
    api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: None
    yield w
    api.client.app.dependency_overrides.clear()


def rows(api):
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_a_clear_order_is_read_back_and_released_only_with_the_ticket_and_a_yes(world):
    h = world.post("hear", {"text": "Could I get a full blood count please"}).json()
    assert (h["status"], h["intent"], h["label"], h["source"]) == ("confirm", "ORDER_TEST", "Full blood count", "grammar")
    assert h["read_back"] == "Confirm: order full blood count?" and h["ticket"]
    unclear = world.post("confirm", {"ticket": h["ticket"], "spoken": "hmm maybe"}).json()
    assert unclear["outcome"] == "open" and unclear["action"] is None
    done = world.post("confirm", {"ticket": h["ticket"], "spoken": "yes please"}).json()
    assert (done["outcome"], done["action"]) == ("released", {"verb": "order_investigation", "params": {"test": "fbc"}})
    again = world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()
    assert (again["outcome"], again["action"], again["reason"]) == ("refused", None, "TICKET_ALREADY_DECIDED")


def test_a_no_cancels_and_nothing_is_released(world):
    h = world.post("hear", {"text": "order a CRP"}).json()
    c = world.post("confirm", {"ticket": h["ticket"], "spoken": "no"}).json()
    assert (c["outcome"], c["action"]) == ("cancelled", None)


def test_something_that_is_not_an_order_gets_the_menu_and_no_ticket(world):
    h = world.post("hear", {"text": "what a lovely morning"}).json()
    assert (h["status"], h["ticket"]) == ("menu", "") and h["options"]


def test_a_ticket_cannot_be_forged_or_used_by_someone_else(world):
    h = world.post("hear", {"text": "order a CRP"}).json()
    mine = h["ticket"]
    assert world.post("confirm", {"ticket": mine, "spoken": "yes"}, who=world.other).json()["reason"] == "TICKET_NOT_YOURS"
    body, sig = mine.split(".")
    assert world.post("confirm", {"ticket": body + "." + sig[::-1], "spoken": "yes"}).json()["reason"] == "TICKET_FORGED"
    assert world.post("confirm", {"ticket": "x" * 40, "spoken": "yes"}).json()["reason"] == "TICKET_MALFORMED"
    assert world.post("confirm", {"ticket": mine, "spoken": "yes"}).json()["outcome"] == "released"         # the real owner can still decide it


def test_the_model_fallback_only_suggests_a_read_back(world):
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: OrderMapper(host({"target": "test:ecg", "confidence": 0.9}))
    h = world.post("hear", {"text": "get a trace of his heart"}).json()
    assert (h["status"], h["source"], h["label"]) == ("confirm", "model", "ECG")
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["action"] == {"verb": "order_investigation", "params": {"test": "ecg"}}
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: OrderMapper(host({"target": "NONE", "confidence": 0.9}))
    assert world.post("hear", {"text": "get a trace of his heart"}).json()["status"] == "menu"


def test_a_hold_is_read_back_and_logged_but_has_no_engine_verb(world):
    h = world.post("hear", {"text": "hold the metformin"}).json()
    assert (h["intent"], h["status"]) == ("HOLD_MED", "confirm")
    c = world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()
    assert (c["outcome"], c["action"], c["reason"]) == ("released", None, "NO_ENGINE_VERB")


def test_with_the_gate_closed_the_menu_is_offered_and_every_call_is_audited(world, monkeypatch):
    h = world.post("hear", {"text": "order a CRP"}).json()
    world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"})
    monkeypatch.setattr(ai_features, "_file", lambda: {})
    config.get_settings.cache_clear()
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "")
    closed = world.post("hear", {"text": "order a CRP"}).json()
    assert (closed["status"], closed["error"], closed["ticket"]) == ("menu", "FEATURE_OFF", "")
    rs = rows(world.api)
    assert [r.task for r in rs] == ["orders", "orders", "orders"] and rs[-1].fallback_reason == "FEATURE_OFF"
    assert "CRP" not in " ".join(r.output_ref or "" for r in rs)


def test_the_text_is_bounded(world):
    assert world.post("hear", {"text": ""}).status_code == 422
    assert world.post("hear", {"text": "x" * 301}).status_code == 422
