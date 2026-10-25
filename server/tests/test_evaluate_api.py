"""The speech evaluation route (p1-ai-language-s22, ADR-0025) on a real PostgreSQL: a valid judgement is returned as judged and audited by hash and code only, the gate off gives the menu, a model that breaks the contract or is unreachable gives the menu and no credit,
a busy slot gives the menu, a bad body is refused, every answer is audited and the route holds no classifier. The model is a scripted provider: no network."""
from __future__ import annotations

import json

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server import ai_features, config
from server.evaluate.provider import MockProvider, ProviderResult
from server.evaluate.reader import Reader
from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
import server.routers.evaluate as speech_route

pytestmark = pytest.mark.db

TEXT = "So tell me, how will you get out of bed tomorrow?"


def judgement(**over) -> str:
    act = {"objective": "CHECK_UNDERSTANDING", "assertion": "performed", "actor": "trainee", "recipient": "patient", "form": "question", "performed_now": True, "relevance": 3, "correctness": 3, "completeness": 3, "evidence": "how will you get out of bed tomorrow", "rationale": "asks the patient to say it back"}
    act.update(over)
    return json.dumps({"acts": [act], "claims_physical_action": False, "physical_action": "", "critical_topic": False})


def context(steps=(9,), **kw):
    return {"case": "X", "scenario": "S1", "mode": "practice", "steps": list(steps), "eligible": [{"act": "CHECK_UNDERSTANDING", "behaviour": "b07", "critical": False}], "accepted": [], "patient_question_pending": False,
            "readback_pending": False, "last_turns": [], "speaker": "trainee", "addressee": "patient", "verified_actions": [], **kw}


@pytest.fixture
def world(api, monkeypatch):
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")                  # these tests play invented trainees: the deployment is declared synthetic
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
    w.use = lambda *replies: api.client.app.dependency_overrides.__setitem__(speech_route.get_reader, lambda: Reader(MockProvider(list(replies)), 2, 5000, 5.0))
    w.use(judgement())
    w.post = lambda body: api.client.post(f"/evaluate/organisations/{org}", json=body, headers=api.headers(me))
    yield w
    api.client.app.dependency_overrides.clear()


def rows(api):
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_a_judgement_is_returned_as_judged_and_the_audit_holds_no_text(world):
    r = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-1"})
    assert r.status_code == 200, r.text
    j = r.json()
    assert (j["decision"], j["utterance_id"], j["acts"][0]["objective"], j["acts"][0]["performed_now"]) == ("EVALUATED", "u-1", "CHECK_UNDERSTANDING", True)
    assert "score" not in json.dumps(j) and "credit" not in json.dumps(j)
    (row,) = rows(world.api)
    assert (row.task, row.validation, row.fallback) == ("evaluate", "passed", False)
    assert "bed" not in (row.output_ref or "") and "bed" not in (row.input_hash or "") and row.prompt_id.startswith("gemma-eval-")


def test_a_non_performed_judgement_is_returned_for_the_record_and_carries_no_performance(world):
    world.use(judgement(assertion="intended_later", performed_now=False))
    j = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-2"}).json()
    assert (j["decision"], j["acts"][0]["assertion"], j["acts"][0]["performed_now"]) == ("EVALUATED", "intended_later", False)


def test_with_the_gate_closed_the_answer_is_the_menu_and_the_fallback_is_audited(world, monkeypatch):
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "")
    config.get_settings.cache_clear()
    ai_features._file.cache_clear()
    monkeypatch.setattr(ai_features, "_file", lambda: {})
    j = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-3"}).json()
    assert (j["decision"], j["reasons"], j["acts"]) == ("UNAVAILABLE", ["FEATURE_OFF"], [])
    assert j["menu"]
    assert rows(world.api)[-1].fallback_reason == "FEATURE_OFF"


def test_a_model_that_breaks_the_contract_twice_gives_the_menu_and_no_judgement(world):
    world.use("not json", "still not json")
    r = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-4"})
    assert r.status_code == 200
    j = r.json()
    assert (j["decision"], j["acts"]) == ("UNAVAILABLE", []) and j["reasons"][0] == "EVAL_INVALID" and j["menu"]
    row = rows(world.api)[-1]
    assert (row.validation, row.output_ref) == ("failed", "UNAVAILABLE") and "EVAL_INVALID" in list(row.reasons)


def test_an_unreachable_or_raising_model_gives_the_menu_not_an_error(world):
    world.use(ProviderResult(error="UNREACHABLE:ConnectError"))
    j = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-5"}).json()
    assert (j["decision"], j["acts"]) == ("UNAVAILABLE", [])
    world.use(RuntimeError("the provider fell over"))
    r = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-6"})
    assert r.status_code == 200 and r.json()["decision"] == "UNAVAILABLE"


def test_a_bad_body_is_refused_with_field_errors(world):
    assert world.post({"context": {"steps": [99]}, "utterance": TEXT, "utterance_id": "u"}).status_code == 422
    assert world.post({"context": context(), "utterance": "", "utterance_id": "u"}).status_code == 422
    assert world.post({"context": context(), "utterance": TEXT}).status_code == 422
    assert world.post({"context": context(mode="exam"), "utterance": TEXT, "utterance_id": "u"}).status_code == 422


def test_every_ai_route_starts_an_audit_record(world):
    assert audit.unaudited_ai_routes(world.api.client.app) == []


def test_a_busy_model_slot_gives_the_menu_and_no_judgement(world, monkeypatch):
    from server.routers import ai_gateway
    monkeypatch.setenv("AI_QUEUE_WAIT_SECONDS", "0.05")
    config.get_settings.cache_clear()
    assert ai_gateway._slot.acquire(timeout=1)
    try:
        j = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-busy"}).json()
    finally:
        ai_gateway._slot.release()
        config.get_settings.cache_clear()
    assert (j["decision"], j["reasons"], j["acts"]) == ("UNAVAILABLE", ["MODEL_BUSY"], [])
    assert rows(world.api)[-1].fallback_reason == "MODEL_BUSY"


def test_real_trainee_words_do_not_reach_the_cloud_evaluator_without_the_privacy_approval(world, monkeypatch):
    monkeypatch.delenv("CLOUD_SYNTHETIC_DEPLOYMENT", raising=False)
    monkeypatch.delenv("CLOUD_TRAINEE_DATA_APPROVED", raising=False)
    provider = MockProvider([judgement()])
    world.api.client.app.dependency_overrides[speech_route.get_reader] = lambda: Reader(provider, 2, 5000, 5.0)
    r = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u9"})
    j = r.json()
    assert (j["decision"], j["reasons"][0], j["acts"]) == ("UNAVAILABLE", "CLOUD_DATA_NOT_APPROVED", []) and j["menu"]
    assert provider.calls == []                                                  # nothing was sent to the model
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")                       # the privacy decision, once recorded, releases it
    assert world.post({"context": context(), "utterance": TEXT, "utterance_id": "u10"}).json()["decision"] == "EVALUATED"
