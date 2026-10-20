"""The interpretation route (p1-ai-language-s11, s12; ADR-0022) on a real PostgreSQL: an eligible act is offered, the gate off gives the menu, a failing classifier gives the menu, a bad body is refused, every answer is audited by code and hash. Nothing is credited by the route."""
from __future__ import annotations

import json
from pathlib import Path

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server import ai_features, config
from server.classify.ensemble import EnsembleActClassifier
from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
from server.routers import interpret as interpret_router

pytestmark = pytest.mark.db

ROOT = Path(__file__).resolve().parents[2]
CASES = json.loads((ROOT / "tests" / "classifier_regression" / "cases.json").read_text(encoding="utf-8"))["cases"]
LOOKUP = {c["text"]: c["embedding"] for c in CASES}
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
STEP = 9                                                                  # the behaviours of step 9 hold CHECK_UNDERSTANDING and EXPLAIN_PROCEDURE
TEXT = "So tell me, how will you get out of bed tomorrow?"                 # a committed sentence the gate credits as CHECK_UNDERSTANDING


def context(steps=(STEP,), **kw):
    eligible = [{"act": a, "behaviour": b["id"], "critical": a in MAPPING["critical_acts"]} for b in MAPPING["behaviours"] if set(b["steps"]) & set(steps) for a in b["ai_acts"]]
    return {"case": "X", "scenario": "S1", "mode": "practice", "steps": list(steps), "eligible": eligible, "accepted": [], "patient_question_pending": False,
            "readback_pending": False, "last_turns": [], "speaker": "trainee", "addressee": "patient", **kw}


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
    api.client.app.dependency_overrides[interpret_router.get_interpreter] = lambda: interpret_router.Interpreter(EnsembleActClassifier(lambda t: LOOKUP.get(t)))
    w.post = lambda body: api.client.post(f"/interpret/organisations/{org}", json=body, headers=api.headers(me))
    yield w
    api.client.app.dependency_overrides.clear()


def rows(api):
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_an_eligible_act_is_offered_as_evidence_and_the_audit_holds_no_text(world):
    r = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-1"})
    assert r.status_code == 200, r.text
    j = r.json()
    assert (j["decision"], j["evidence"]["act"], j["evidence"]["utterance_id"], j["clarify"]) == ("OFFER_EVIDENCE", "CHECK_UNDERSTANDING", "u-1", "")
    (row,) = rows(world.api)
    assert (row.task, row.validation, row.fallback) == ("interpret", "passed", False)
    assert "bed" not in (row.output_ref or "") and "bed" not in (row.input_hash or "")
    assert list(row.reasons) == ["OFFER_EVIDENCE"] and row.prompt_id.startswith("interpret-")        # the rule set behind the decision, no words


def test_the_same_sentence_outside_its_window_is_not_offered(world):
    j = world.post({"context": context(steps=(2,)), "utterance": TEXT, "utterance_id": "u-2"}).json()
    assert (j["decision"], j["evidence"]) == ("ABSTAIN", None)
    assert "NOT_ELIGIBLE" in j["reasons"]
    row = rows(world.api)[-1]                                          # a refusal keeps its reason codes in the audit row (counts by cause), and still holds no words
    assert (row.validation, row.output_ref) == ("failed", "ABSTAIN") and "NOT_ELIGIBLE" in list(row.reasons)
    assert "bed" not in " ".join(row.reasons) and "bed" not in (row.output_ref or "")


def test_with_the_gate_closed_the_answer_is_the_menu_and_the_fallback_is_audited(world, monkeypatch):
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "")
    config.get_settings.cache_clear()
    ai_features._file.cache_clear()
    monkeypatch.setattr(ai_features, "_file", lambda: {})
    j = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-3"}).json()
    assert (j["decision"], j["reasons"], j["evidence"]) == ("MENU", ["FEATURE_OFF"], None)
    assert j["menu"]
    assert rows(world.api)[-1].fallback_reason == "FEATURE_OFF"


def test_a_failing_classifier_gives_the_menu_not_an_error(world):
    class Boom:
        model = "boom"

        def classify_with_probs(self, s):
            raise RuntimeError("the embedder fell over")
    world.api.client.app.dependency_overrides[interpret_router.get_interpreter] = lambda: interpret_router.Interpreter(Boom())
    r = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-4"})
    assert r.status_code == 200
    assert (r.json()["decision"], r.json()["reasons"]) == ("MENU", ["INTERPRET_ERROR"])
    assert rows(world.api)[-1].fallback_reason == "INTERPRET_ERROR"


def test_an_embedder_that_answers_nothing_gives_the_menu(world):
    world.api.client.app.dependency_overrides[interpret_router.get_interpreter] = lambda: interpret_router.Interpreter(EnsembleActClassifier(lambda t: None))
    j = world.post({"context": context(), "utterance": TEXT, "utterance_id": "u-5"}).json()
    assert (j["decision"], j["evidence"]) == ("MENU", None)
    assert j["reasons"] == ["EMBEDDING_UNAVAILABLE"]


def test_a_bad_body_is_refused_with_field_errors(world):
    assert world.post({"context": {"steps": [99]}, "utterance": TEXT, "utterance_id": "u"}).status_code == 422
    assert world.post({"context": context(), "utterance": "", "utterance_id": "u"}).status_code == 422
    assert world.post({"context": context(), "utterance": TEXT}).status_code == 422
    assert world.post({"context": context(mode="exam"), "utterance": TEXT, "utterance_id": "u"}).status_code == 422


def test_every_ai_route_starts_an_audit_record(world):
    assert audit.unaudited_ai_routes(world.api.client.app) == []
