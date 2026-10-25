"""The team-turn route (ADR-0026): one call answers the patient and the staff. On a real PostgreSQL with fake model hosts: the patient stays grounded, staff speak only the approved line of an allowed intent,
a hidden fact never leaves, a client cannot widen the closed lists, the fallbacks are the authored lines, the audit holds no text, and a cloud route is used (and falls back) only as the gates say."""
from __future__ import annotations

import json

import httpx
import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server import ai_features
from server.dialogue.dialogue import Dialogue
from server.llm import client as client_mod, routing
from server.middleware import audit
from server.models.ai_call import AiCall
from server.repositories import accounts
from server.routers import dialogue as dialogue_router

pytestmark = pytest.mark.db

CTX = {"role": "patient", "case": "X",
       "facts": [{"id": "h.complaint", "value": "Pain low on the right for two days.", "disclosed": False, "disclosable_now": True},
                 {"id": "h.medicines", "value": "I take warfarin every day.", "disclosed": False, "disclosable_now": False}], "last_turns": []}
PATIENT = {"intent": "answer_question", "text": "Pain low on the right for two days.", "fact_ids": ["h.complaint"]}
STAFF = {"nurse": "report_observation", "anaesthesia": "respond_to_escalation"}


def both(patient=PATIENT, staff=STAFF):
    """What a model host answers to the two prompts of a turn: the patient's reply, and the staff's choice of intents."""
    return lambda body: staff if "Roles and their allowed intents" in body["messages"][1]["content"] else patient
LINES = json.loads((dialogue_router.STAFF_LINES_FILE).read_text(encoding="utf-8"))["roles"]


def host(answer, models_seen=None):
    def h(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        body = json.loads(request.content)
        if models_seen is not None:
            models_seen.append(body["model"])
        out = answer(body) if callable(answer) else answer
        if isinstance(out, httpx.Response):
            return out
        return httpx.Response(200, json={"message": {"content": json.dumps(out)}})
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


@pytest.fixture
def cloud(monkeypatch):
    """The cloud model is allowed (the switch is on and the deployment is declared synthetic): the staff are chosen by it."""
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")


@pytest.fixture
def world(api, monkeypatch):
    for name in ("CLOUD_ROUTING_ENABLED", "CLOUD_TRAINEE_DATA_APPROVED", "CLOUD_SYNTHETIC_DEPLOYMENT"):
        monkeypatch.delenv(name, raising=False)

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
    w.use = lambda client: api.client.app.dependency_overrides.__setitem__(dialogue_router.get_dialogue, lambda: Dialogue(client))
    w.use(host(both()))
    w.post = lambda body=None: api.client.post(f"/dialogue/organisations/{org}/team", json=body or {"context": CTX, "question": "Where is the pain? Nurse, check the pulse.", "age": 40, "sex": "F", "asked_fact": "h.complaint"}, headers=api.headers(me))
    yield w
    api.client.app.dependency_overrides.clear()


def rows(api):
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


def test_the_patient_is_answered_from_the_shown_fact_and_the_staff_speak_their_approved_lines(world, cloud):
    r = world.post()
    assert r.status_code == 200, r.text
    got = {x["role"]: x for x in r.json()["replies"]}
    assert [x["role"] for x in r.json()["replies"]] == ["patient", "nurse", "anaesthesia"]            # the order they speak
    assert (got["patient"]["source"], got["patient"]["fact_ids"], got["patient"]["text"]) == ("model", ["h.complaint"], "Pain low on the right for two days.")
    assert got["nurse"]["intent"] == "report_observation" and got["nurse"]["text"] == LINES["nurse"]["report_observation"]            # the words are the approved line, not the model's
    assert got["anaesthesia"]["text"] == LINES["anaesthesia"]["respond_to_escalation"]
    (row,) = rows(world.api)
    assert (row.task, row.validation) == ("dialogue_team", "passed") and "Pain" not in row.output_ref


def test_a_hidden_fact_never_leaves_and_the_patient_gets_the_authored_line(world, cloud):
    world.use(host(both(patient={"intent": "answer_question", "text": "I take warfarin every day.", "fact_ids": []})))
    r = world.post({"context": CTX, "question": "What do you take?", "authored": {"cannot_recall": "I am not sure about that."}, "asked_fact": "h.medicines"})
    got = {x["role"]: x for x in r.json()["replies"]}
    assert got["patient"]["source"] == "fallback" and "warfarin" not in got["patient"]["text"].lower() and got["patient"]["text"] == "I am not sure about that."
    assert "nurse" not in got                                                                                 # the patient's answer was replaced by the authored line, so the turn stays with the patient


def test_the_client_cannot_widen_the_staff_lists_or_pick_the_words(world, cloud):
    seen = []
    world.use(host(both(), seen))
    r = world.post({"context": dict(CTX, allowed_intents=["prescribe_drug"]), "question": "Hello", "staff_roles": ["nurse", "wizard", "anaesthesia"]})
    assert [x["role"] for x in r.json()["replies"]] == ["patient", "nurse", "anaesthesia"]            # an unknown role is dropped
    assert "prescribe_drug" not in json.dumps(r.json())


def test_a_staff_intent_outside_the_closed_list_is_refused_and_the_staff_stay_silent(world, cloud):
    world.use(host(both(staff=dict(STAFF, nurse="prescribe_drug"))))
    r = world.post()
    got = {x["role"]: x for x in r.json()["replies"]}
    assert got["patient"]["source"] == "model" and "nurse" not in got and "anaesthesia" not in got            # nobody is made to speak a line the model did not choose
    assert "prescribe" not in json.dumps(r.json()).lower()


def test_a_team_member_who_is_not_addressed_chooses_silence_and_says_nothing(world, cloud):
    world.use(host(both(staff={"nurse": "stay_silent", "anaesthesia": "respond_to_escalation"})))
    got = {x["role"]: x for x in world.post().json()["replies"]}
    assert sorted(got) == ["anaesthesia", "patient"] and got["anaesthesia"]["text"] == LINES["anaesthesia"]["respond_to_escalation"]
    world.use(host(both(staff={"nurse": "stay_silent", "anaesthesia": "stay_silent"})))
    assert [x["role"] for x in world.post().json()["replies"]] == ["patient"]


def test_a_down_model_gives_the_authored_lines_and_never_an_error(world):
    world.use(host(lambda b: httpx.Response(500, text="down")))
    r = world.post()
    assert r.status_code == 200 and [x["role"] for x in r.json()["replies"]] == ["patient"] and r.json()["replies"][0]["source"] == "fallback"


def test_the_feature_switch_off_gives_the_authored_lines_without_a_model_call(world, monkeypatch):
    seen = []
    world.use(host(both(), seen))
    monkeypatch.setattr(ai_features, "enabled", lambda name: False)
    r = world.post()
    assert r.status_code == 200 and seen == [] and [x["role"] for x in r.json()["replies"]] == ["patient"] and r.json()["replies"][0]["source"] == "fallback"


def test_the_cloud_model_answers_when_the_gates_allow_and_the_audit_says_so(world, cloud):
    seen = []
    world.use(host(both(), seen))
    r = world.post()
    assert r.json()["cloud"] is True and r.json()["model"] == "gemma4:31b-cloud" and seen == ["gemma4:31b-cloud", "gemma4:31b-cloud"]       # the patient's call, then the staff call
    assert [x["role"] for x in r.json()["replies"]] == ["patient", "nurse", "anaesthesia"]
    assert "plain text" in routing.FORMAT_INSTRUCTION


def test_real_trainee_speech_stays_on_the_local_model_and_only_the_patient_answers(world, monkeypatch):
    seen = []
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")                       # the switch is on, the privacy decision and the synthetic declaration are not
    world.use(host(both(), seen))
    r = world.post()
    assert r.json()["cloud"] is False and seen == [client_mod.LIVE_MODEL]                  # one local call: the staff are not asked of a model that cannot be trusted to choose or to stay silent
    assert [x["role"] for x in r.json()["replies"]] == ["patient"]


def test_a_cloud_outage_falls_back_to_the_local_model_for_the_patient_and_the_staff_stay_quiet(world, cloud):
    seen = []
    answer = both()
    world.use(host(lambda b: httpx.Response(402, text="not in the Free plan") if b["model"].endswith("-cloud") else answer(b), seen))
    r = world.post()
    assert r.status_code == 200 and seen == ["gemma4:31b-cloud", client_mod.LIVE_MODEL] and r.json()["cloud"] is False        # no second cloud call, no local staff call
    assert [x["role"] for x in r.json()["replies"]] == ["patient"] and r.json()["replies"][0]["source"] == "model"


def test_a_cloud_staff_choice_that_fails_leaves_the_patient_answered_and_the_staff_quiet(world, cloud):
    answer = both()
    calls = []

    def flaky(b):
        calls.append(b["model"])
        if "Roles and their allowed intents" in b["messages"][1]["content"] and b["model"].endswith("-cloud"):
            return httpx.Response(500, text="boom")
        return answer(b)
    world.use(host(flaky))
    r = world.post()
    assert [x["role"] for x in r.json()["replies"]] == ["patient"] and r.json()["replies"][0]["source"] == "model"


def test_the_health_page_says_what_the_routing_does_without_any_secret(world, cloud):
    status = world.api.client.get("/ai/health").json()["routing"]
    assert status == {"cloud_routing": True, "synthetic_deployment": True, "trainee_data_approved": False, "trainee_routes_use_cloud": True, "needs_personal_consent": False}
