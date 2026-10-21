"""The dialogue, evaluation and debrief routes (p1-ai-language s04, s07, s08, s22; p1-backend-s06) on a real PostgreSQL with fake model hosts: answers, fallbacks, the feature gate, the audit."""
from __future__ import annotations

import json

import httpx
import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server import ai_features, config
from server.debrief.rewrite import Rewriter
from server.dialogue.dialogue import Dialogue
from server.llm import client as client_mod
from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
from server.routers import debrief as debrief_router, dialogue as dialogue_router

pytestmark = pytest.mark.db


def host(answer):
    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


CTX = {"role": "patient", "case": "X", "allowed_intents": ["answer_question", "cannot_recall"],
       "facts": [{"id": "h.complaint", "value": "Pain low on the right for two days.", "disclosed": False, "disclosable_now": True},
                 {"id": "h.medicines", "value": "I take warfarin every day.", "disclosed": False, "disclosable_now": False}], "last_turns": []}
REPORT = {"case": "X", "end_state": "SAFE_BUT_DELAYED", "result": "SUCCESS_WITH_DELAY", "items": [{"id": "credit.decision_correct", "status": "credit"}, {"id": "major.postponed_unnecessarily", "status": "penalty"}]}


@pytest.fixture
def world(api):
    def build(s: DbSession):
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        me = api.person(s)
        accounts.add_membership(s, me, org.id, "trainee")
        return org.id, me
    org, me = api.seed(build)

    class W:
        pass
    w = W()
    w.api, w.org, w.me = api, org, me
    o = api.client.app.dependency_overrides
    o[dialogue_router.get_dialogue] = lambda: Dialogue(host({"intent": "answer_question", "text": "Pain low on the right for two days.", "fact_ids": ["h.complaint"]}))
    o[debrief_router.get_rewriter] = lambda: Rewriter(host({"sections": [{"title": "What went well", "text": "You made the right decision, well done.", "fact_ids": ["f1"]},
                                                                           {"title": "What to improve", "text": "The case was postponed when it did not need to be, a mistake to work on.", "fact_ids": ["f2"]},
                                                                           {"title": "How the session ended", "text": "It ended safe but delayed.", "fact_ids": ["f3"]}]}))
    w.post = lambda path, body: api.client.post(f"/{path}/organisations/{org}" , json=body, headers=api.headers(me))
    yield w
    o.clear()


def rows(api):
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_the_patient_route_answers_from_the_shown_fact_and_audits_hashes(world):
    r = world.api.client.post(f"/dialogue/organisations/{world.org}/patient", json={"context": CTX, "question": "Where is the pain?", "age": 40, "sex": "F"}, headers=world.api.headers(world.me))
    assert r.status_code == 200, r.text
    assert (r.json()["source"], r.json()["intent"], r.json()["fact_ids"]) == ("model", "answer_question", ["h.complaint"])
    (row,) = rows(world.api)
    assert (row.task, row.prompt_id, row.context_id, row.validation) == ("dialogue_patient", "dialogue.patient:v1", "X", "passed") and "Pain" not in row.output_ref


def test_a_leaking_model_gets_the_authored_line_and_a_bad_context_is_refused(world):
    world.api.client.app.dependency_overrides[dialogue_router.get_dialogue] = lambda: Dialogue(host({"intent": "answer_question", "text": "I take warfarin every day.", "fact_ids": []}))
    r = world.api.client.post(f"/dialogue/organisations/{world.org}/patient", json={"context": CTX, "question": "Anything else?", "authored": {"cannot_recall": "I do not remember that."}}, headers=world.api.headers(world.me))
    assert (r.json()["source"], r.json()["text"], r.json()["intent"]) == ("fallback", "I do not remember that.", "cannot_recall")
    assert "DISCLOSURE_LEAK" in r.json()["reasons"]
    assert world.api.client.post(f"/dialogue/organisations/{world.org}/patient", json={"context": {"facts": []}, "question": "x"}, headers=world.api.headers(world.me)).status_code == 422
    assert rows(world.api)[0].fallback is True


def test_the_staff_route_uses_the_staff_prompt(world):
    ctx = {"role": "nurse", "case": "X", "allowed_intents": ["ask_clarification", "confirm_order"], "facts": [{"id": "s.1", "value": "The cannula is in place.", "disclosed": False, "disclosable_now": True}], "last_turns": []}
    world.api.client.app.dependency_overrides[dialogue_router.get_dialogue] = lambda: Dialogue(host({"intent": "confirm_order", "text": "The cannula is in place.", "fact_ids": ["s.1"]}))
    r = world.api.client.post(f"/dialogue/organisations/{world.org}/staff", json={"context": ctx, "question": "Is the cannula in?", "role": "nurse"}, headers=world.api.headers(world.me))
    assert (r.status_code, r.json()["source"], r.json()["intent"]) == (200, "model", "confirm_order")
    assert rows(world.api)[0].prompt_id == "dialogue.staff:v1"


def test_the_debrief_route_gives_the_template_and_the_rewrite_when_the_validator_passes(world):
    r = world.api.client.post(f"/debrief/organisations/{world.org}/rewrite", json={"report": REPORT}, headers=world.api.headers(world.me))
    j = r.json()
    assert (r.status_code, j["used"], len(j["template"]) >= 2, j["ai"] is not None) == (200, "ai", True, True)
    bad = {"sections": [{"title": "What went well", "text": "You made the wrong decision.", "fact_ids": ["f1", "f2", "f3"]}]}
    world.api.client.app.dependency_overrides[debrief_router.get_rewriter] = lambda: Rewriter(host(bad))
    j = world.api.client.post(f"/debrief/organisations/{world.org}/rewrite", json={"report": REPORT}, headers=world.api.headers(world.me)).json()
    assert (j["used"], j["ai"]) == ("template", None) and j["reasons"]


def test_with_the_gate_closed_every_route_answers_with_the_fallback(world, monkeypatch):
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "")
    config.get_settings.cache_clear()
    ai_features._file.cache_clear()
    monkeypatch.setattr(ai_features, "_file", lambda: {})                       # a gate with no feature on, whatever the committed results say today
    c = world.api.client
    d = c.post(f"/dialogue/organisations/{world.org}/patient", json={"context": CTX, "question": "x"}, headers=world.api.headers(world.me)).json()
    assert (d["source"], d["reasons"]) == ("fallback", ["FEATURE_OFF"])
    k = c.post(f"/evaluate/organisations/{world.org}", json={"context": {"case": "X", "scenario": "s", "mode": "practice", "steps": [2], "eligible": []}, "utterance": "x", "utterance_id": "u1"}, headers=world.api.headers(world.me)).json()
    assert (k["decision"], k["acts"], k["reasons"]) == ("UNAVAILABLE", [], ["FEATURE_OFF"])
    b = c.post(f"/debrief/organisations/{world.org}/rewrite", json={"report": REPORT}, headers=world.api.headers(world.me)).json()
    assert (b["used"], b["reasons"]) == ("template", ["FEATURE_OFF"])
    assert {r.fallback_reason for r in rows(world.api)} == {"FEATURE_OFF"}


def test_every_ai_route_starts_an_audit_record(world):
    assert audit.unaudited_ai_routes(world.api.client.app) == []
