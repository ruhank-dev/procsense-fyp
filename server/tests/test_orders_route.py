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
from server.orders.interpreter import OrderInterpreter
from server.repositories import accounts
from server.routers import orders as orders_router

pytestmark = pytest.mark.db


def host(answer):
    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
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


def test_the_model_only_suggests_a_read_back(world):
    ask = {"kind": "order", "intent": "ORDER_TEST", "target": "test:ecg", "confidence": 0.9}
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: OrderInterpreter(host(ask))
    h = world.post("hear", {"text": "get a trace of his heart"}).json()
    assert (h["status"], h["source"], h["label"], h["interpretation"]) == ("confirm", "model", "ECG", "order")
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["action"] == {"verb": "order_investigation", "params": {"test": "ecg"}}
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: OrderInterpreter(host({"kind": "question", "intent": "NONE", "target": "NONE", "confidence": 0.9}))
    q = world.post("hear", {"text": "can I ask what an ECG shows"}).json()
    assert (q["status"], q["ticket"], q["interpretation"]) == ("menu", "", "question")


def test_a_hold_is_read_back_and_released_to_the_hold_verb(world):
    h = world.post("hear", {"text": "hold the metformin"}).json()
    assert (h["intent"], h["status"]) == ("HOLD_MED", "confirm")
    c = world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()
    assert (c["outcome"], c["action"]) == ("released", {"verb": "hold_medicine", "params": {"drug": "metformin"}})


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


def _demo(monkeypatch):
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")


def test_in_the_demonstration_profile_gemma_reads_the_order_and_only_the_yes_releases_it(world, monkeypatch):
    from server.orders.cloud_interpreter import CloudOnlyInterpreter
    _demo(monkeypatch)
    ask = {"kind": "order", "intent": "REQUEST_MEDICATION", "target": "medicine:TR-PARACETAMOL", "confidence": 0.9}
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: CloudOnlyInterpreter(host(ask))
    h = world.post("hear", {"text": "can I get some paracetamol for her?"}).json()
    assert (h["status"], h["interpreter"], h["interpreter_model"], h["fallback_reason"]) == ("confirm", "gemma_cloud", "gemma4:31b-cloud", "") and h["ticket"]
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "maybe"}).json()["outcome"] == "open"                    # nothing is released by anything but a yes
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["action"]["verb"] == "request_medication"
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["outcome"] != "released"                   # single use
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: CloudOnlyInterpreter(host({"kind": "ambiguous", "intent": "NONE", "target": "NONE", "alternatives": ["test:ct", "test:mri"], "confidence": 0.8, "clarification": "CT or MRI?"}))
    a = world.post("hear", {"text": "get a scan of her tummy"}).json()
    assert (a["status"], a["ticket"], a["interpreter"]) == ("clarify", "", "gemma_cloud")                                    # an ambiguous request makes no ticket: there is nothing to confirm or execute


def test_when_gemma_cannot_answer_the_grammar_answers_and_the_route_says_so_and_audits_it(world, monkeypatch):
    from server.llm import client as cm
    from server.orders.cloud_interpreter import CloudOnlyInterpreter
    _demo(monkeypatch)
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: CloudOnlyInterpreter(cm.OllamaClient("http://127.0.0.1:9"))
    h = world.post("hear", {"text": "order a crp"}).json()
    assert (h["status"], h["source"], h["interpreter"], h["fallback_reason"], h["interpreter_model"]) == ("confirm", "grammar", "offline_grammar", "CLOUD_UNREACHABLE", "")
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["action"] == {"verb": "order_investigation", "params": {"test": "crp"}}
    rs = rows(world.api)
    assert rs[0].fallback_reason == "CLOUD_UNREACHABLE" and "crp" not in " ".join(r.output_ref or "" for r in rs).lower()      # the audit names the reason and never the words


@pytest.mark.skipif(not __import__("os").environ.get("PROCSENSE_LIVE_CLOUD"), reason="live check against the signed-in Ollama and the real Gemma 4 31B Cloud; set PROCSENSE_LIVE_CLOUD=1 (synthetic sentences only, uses the free plan)")
def test_live_the_real_gemma_cloud_answers_through_the_whole_route(world, monkeypatch):
    from server.llm import client as cm
    from server.orders.cloud_interpreter import CloudOnlyInterpreter
    _demo(monkeypatch)
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: CloudOnlyInterpreter(cm.OllamaClient("http://127.0.0.1:11434"))
    h = world.post("hear", {"text": "Can I get a CRP on her?", "context": "Pre-operative ward, planned cholecystectomy. Invented patient."}).json()
    assert (h["status"], h["interpreter"], h["label"]) == ("confirm", "gemma_cloud", "CRP")
    assert "gemma" in h["interpreter_reported_model"].lower()                                                                  # the host itself names the model that answered
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["action"] == {"verb": "order_investigation", "params": {"test": "crp"}}


def test_a_missing_detail_is_chosen_from_a_closed_list_and_the_completed_order_gets_its_own_single_use_ticket(world):
    h = world.post("hear", {"text": "transport the patient to the ct"}).json()
    assert (h["status"], [n["param"] for n in h["needs"]]) == ("confirm", ["mode"])
    bad = world.post("details", {"ticket": h["ticket"], "choices": {"mode": "helicopter"}}).json()
    assert (bad["status"], bad["error"], bad["ticket"]) == ("menu", "VALUE_NOT_ALLOWED", "")                                   # the open ticket is not used up by a refused choice
    d = world.post("details", {"ticket": h["ticket"], "choices": {"mode": "trolley"}}).json()
    assert (d["status"], d["needs"], d["interpreter"]) == ("confirm", [], "") and d["ticket"] and d["ticket"] != h["ticket"]
    assert world.post("confirm", {"ticket": h["ticket"], "spoken": "yes"}).json()["outcome"] == "refused"                         # the first ticket was used up by the completion
    c = world.post("confirm", {"ticket": d["ticket"], "spoken": "yes"}).json()
    assert (c["outcome"], c["verb"]) == ("released", "request_transport") and sorted((p["k"], p["v"]) for p in c["action_pairs"]) == [("mode", "trolley"), ("to", "ct")]
    assert world.post("confirm", {"ticket": d["ticket"], "spoken": "yes"}).json()["outcome"] == "refused"                         # single use


def test_a_ticket_of_another_person_or_a_forged_one_cannot_be_completed(world):
    h = world.post("hear", {"text": "transport the patient to the ct"}).json()
    other = world.api.client.post(f"/orders/organisations/{world.org}/details", json={"ticket": h["ticket"], "choices": {"mode": "walk"}}, headers=world.api.headers(world.other)).json()
    assert other["status"] == "menu" and other["error"] == "TICKET_NOT_YOURS"
    assert world.post("details", {"ticket": h["ticket"][:-3] + "AAA", "choices": {"mode": "walk"}}).json()["error"] == "TICKET_FORGED"


def test_the_exact_body_unity_sends_is_answered_by_the_real_route_in_the_contract_shape(world):
    """The request file is the byte-for-byte output of SpokenOrderClient.HearBody (compared in the Unity test); the route answers it and the answer has every field the Unity parser reads."""
    from pathlib import Path
    body = (Path(__file__).resolve().parents[2] / "tests" / "orders" / "unity_contract" / "requests" / "hear.json").read_text(encoding="utf-8")
    r = world.api.client.post(f"/orders/organisations/{world.org}/hear", content=body, headers={**world.api.headers(world.me), "Content-Type": "application/json"})
    assert r.status_code == 200
    answer = r.json()
    for field in ("status", "intent", "label", "read_back", "options", "dose_removed", "source", "interpretation", "ticket", "error", "interpreter", "interpreter_model", "interpreter_reported_model", "fallback_reason", "interpreter_ms", "needs"):
        assert field in answer, field
    assert answer["interpreter"] == "offline_grammar"                                                                           # no interpreter is built in this fixture: the grammar answered, and says so
