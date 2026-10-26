"""Consent to cloud AI processing (DT-248, DT-127, ADR-0027): the Terms and the Privacy Notice are public and versioned; the optional cloud answer is separate from the Terms and from the results question; it is
recorded with the version, the time, the fingerprint of what was shown and where it was given; it can be withdrawn; and the person's words go to the cloud only with the institutional approval AND their own current
yes. On a real PostgreSQL with fake model hosts."""
from __future__ import annotations

import json

import httpx
import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session as DbSession

from server import privacy
from server.dialogue.dialogue import Dialogue
from server.llm import client as client_mod
from server.models.accounts import ConsentRecord
from server.repositories import accounts
from server.routers import dialogue as dialogue_router, speech_action as speech_router
from server.speech_action.service import SpeechActions
from server.tests.test_dialogue_team import CTX, both, host

pytestmark = pytest.mark.db


@pytest.fixture
def world(api, monkeypatch):
    for name in ("CLOUD_ROUTING_ENABLED", "CLOUD_TRAINEE_DATA_APPROVED", "CLOUD_SYNTHETIC_DEPLOYMENT"):
        monkeypatch.delenv(name, raising=False)

    def build(s: DbSession):
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        me = api.person(s)                                     # has accepted the Terms and the results question, nothing else
        accounts.add_membership(s, me, org.id, "trainee")
        return org.id, me
    org, me = api.seed(build)

    class W:
        pass
    w = W()
    w.api, w.org, w.me = api, org, me
    w.seen = []
    w.use = lambda client: api.client.app.dependency_overrides.__setitem__(dialogue_router.get_dialogue, lambda: Dialogue(client))
    w.use(host(both(), w.seen))
    w.consent = lambda granted, kind="cloud": api.client.post("/privacy/consent", json={"kind": kind, "granted": granted}, headers=api.headers(me))
    w.team = lambda: api.client.post(f"/dialogue/organisations/{org}/team", json={"context": CTX, "question": "Where is the pain?", "asked_fact": "h.complaint"}, headers=api.headers(me))
    yield w
    api.client.app.dependency_overrides.clear()


def test_the_terms_and_the_privacy_notice_are_public_versioned_and_say_what_they_must():
    from fastapi.testclient import TestClient
    from server.main import create_app
    client = TestClient(create_app())
    terms, notice = client.get("/legal/terms").json(), client.get("/legal/privacy-notice").json()
    assert terms["version"] == notice["version"] == privacy.CONSENT_VERSION and len(terms["sha256"]) == 64 and terms["sha256"] != notice["sha256"]
    text = json.dumps(notice).lower()
    for must in ("ollama", "gemma 4 31b", "transcript", "united states", "not stored beyond the time required", "never train", "no fixed retention period", "does not say where", "withdraw", "never sent to a cloud ai provider"):
        assert must in text.replace("  ", " "), must
    for never in ("never retained", "no retention", "deleted immediately", "guarantee", "gdpr compliant", "hipaa"):
        assert never not in text, never                        # nothing the provider does not say, and no claim of legal compliance
    assert "does not claim legal compliance" in text and client.get("/legal/nonsense").status_code == 404
    assert "not your consent to any optional use" in json.dumps(terms)       # accepting the Terms is not the cloud consent


def test_the_cloud_question_is_optional_separate_and_points_at_the_notice(world):
    state = world.api.client.get("/privacy/consent", headers=world.api.headers(world.me)).json()
    kinds = {k["kind"]: k for k in state["kinds"]}
    assert state["needs_consent"] is False                     # the Terms and the results question are answered; the cloud question is not required
    assert kinds["cloud"]["required"] is False and kinds["cloud"]["granted"] is None and kinds["cloud"]["documents"] == ["privacy_notice"] and kinds["terms"]["required"] is True
    assert "ollama" in kinds["cloud"]["text"].lower() and "withdraw" in kinds["cloud"]["text"].lower()


def test_an_answer_keeps_the_version_the_time_the_fingerprint_and_where_it_was_given(world):
    world.consent(True)
    with DbSession(world.api.engine) as s:
        row = s.scalars(select(ConsentRecord).where(ConsentRecord.user_id == world.me, ConsentRecord.kind == "cloud")).one()
        assert (row.text_version, row.granted, row.via, row.text_sha256) == (privacy.CONSENT_VERSION, True, "web", privacy.text_hash("cloud")) and row.recorded_at is not None
    export = world.api.client.get("/privacy/export", headers=world.api.headers(world.me)).json()
    assert any(c["kind"] == "cloud" and c["text_sha256"] and c["via"] == "web" for c in export["consents"])        # the person can see what they agreed to and when


def test_a_changed_notice_changes_the_fingerprint_and_asks_again(world, monkeypatch, tmp_path):
    world.consent(True)
    before = privacy.text_hash("cloud")
    (tmp_path / "privacy_notice.json").write_text(json.dumps({"id": "privacy_notice", "version": "x", "title": "t", "sections": []}), encoding="utf-8")
    (tmp_path / "terms.json").write_text((privacy.LEGAL_DIR / "terms.json").read_text(encoding="utf-8"), encoding="utf-8")
    monkeypatch.setattr(privacy, "LEGAL_DIR", tmp_path)
    assert privacy.text_hash("cloud") != before
    monkeypatch.setattr(privacy, "CONSENT_VERSION", "2027-01-01.1")
    state = world.api.client.get("/privacy/consent", headers=world.api.headers(world.me)).json()
    assert state["needs_consent"] is True and {k["kind"]: k["current"] for k in state["kinds"]}["cloud"] is False        # a new text is a new question


def test_the_words_go_to_the_cloud_only_with_the_approval_and_the_persons_own_current_yes(world, monkeypatch):
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")                         # the institutional approval is recorded
    r = world.team()                                                                # the person accepted the Terms only
    assert r.json()["cloud"] is False and world.seen == [client_mod.LIVE_MODEL]    # general acceptance is not consent to cloud processing
    world.seen.clear()
    world.consent(False)                                                            # an explicit no is the same
    assert world.team().json()["cloud"] is False and "gemma4:31b-cloud" not in world.seen
    world.seen.clear()
    world.consent(True)
    r = world.team()
    assert r.json()["cloud"] is True and world.seen[0] == "gemma4:31b-cloud"        # approval and consent together
    world.seen.clear()
    world.consent(False)                                                            # withdrawn: local from the next request
    r = world.team()
    assert r.json()["cloud"] is False and "gemma4:31b-cloud" not in world.seen and r.json()["replies"][0]["source"] == "model"


def test_consent_alone_does_not_open_the_cloud_without_the_institutional_approval(world, monkeypatch):
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    world.consent(True)
    assert world.team().json()["cloud"] is False and "gemma4:31b-cloud" not in world.seen


# ---- spoken actions ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

def speech_host(answer, seen):
    def h(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        body = json.loads(request.content)
        seen.append(body["model"])
        out = answer(body) if callable(answer) else answer
        return out if isinstance(out, httpx.Response) else httpx.Response(200, json={"message": {"content": json.dumps(out)}})
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


@pytest.fixture
def spoken(world):
    seen = []
    world.sseen = seen
    world.say = lambda reply, **kw: (world.api.client.app.dependency_overrides.__setitem__(speech_router.get_speech_actions, lambda: SpeechActions(speech_host(reply, seen))),
                                     world.api.client.post(f"/speech/organisations/{world.org}/action", json={"utterance": kw.get("utterance", "What medications are you taking?"), "available": kw.get("available", ["ask:medicines", "ask_allergies"]),
                                                                                                                   "turns": kw.get("turns", [])}, headers=world.api.headers(world.me)))[1]
    return world


def test_a_spoken_question_names_the_tablet_action_and_the_closed_list_decides(spoken):
    r = spoken.say({"actions": ["ask:medicines"], "addressee": "patient"})
    assert r.status_code == 200, r.text
    j = r.json()
    assert j["actions"] == [{"id": "ask:medicines", "verb": "ask", "key": "topic", "value": "medicines"}] and j["addressee"] == "patient" and j["source"] == "model"
    j = spoken.say({"actions": ["ask:smoking"], "addressee": "patient"}).json()                  # not in the list of actions available now: refused, no action
    assert j["actions"] == [] and j["source"] == "fallback"
    j = spoken.say({"actions": ["confirm_identity"], "addressee": "patient"}).json()              # a physical action is not a spoken action at all
    assert j["actions"] == [] and j["source"] == "fallback"
    j = spoken.say({"actions": ["ask:medicines", "ask_allergies", "ask_allergies"], "addressee": "patient"}).json()
    assert j["actions"] == [] and j["source"] == "fallback"                                       # more than two, or a repeat, breaks the contract


def test_an_unavailable_model_gives_no_action_and_never_an_error(spoken):
    r = spoken.say(httpx.Response(500, text="down"))
    assert r.status_code == 200 and r.json()["actions"] == [] and r.json()["source"] == "fallback"
    assert spoken.say({"actions": [], "addressee": "nobody"}, available=[]).json()["reasons"] == ["NOTHING_TO_ASK"]


def test_spoken_words_follow_the_same_consent_rule_as_the_dialogue(spoken, monkeypatch):
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")
    j = spoken.say({"actions": ["ask:medicines"], "addressee": "patient"}).json()
    assert j["cloud"] is False and spoken.sseen[-1] == client_mod.LIVE_MODEL                      # no personal consent: the local model reads the words
    spoken.consent(True)
    j = spoken.say({"actions": ["ask:medicines"], "addressee": "patient"}).json()
    assert j["cloud"] is True and spoken.sseen[-1] == "gemma4:31b-cloud"


def test_spoken_orders_follow_the_same_consent_rule_as_the_dialogue(world, monkeypatch):
    import json as _json

    import httpx

    from server.routers import orders as orders_router
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")
    asked = []

    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": client_mod.LIVE_MODEL}, {"name": "gemma4:31b-cloud"}]})
        asked.append(_json.loads(request.content)["model"])
        return httpx.Response(200, json={"model": "gemma4:31b", "message": {"content": _json.dumps({"kind": "order", "intent": "ORDER_TEST", "target": "test:ecg", "confidence": 0.9})}})
    world.api.client.app.dependency_overrides[orders_router.get_mapper] = lambda: orders_router.CloudOnlyInterpreter(client_mod.OllamaClient(transport=httpx.MockTransport(h)))

    def hear():
        return world.api.client.post(f"/orders/organisations/{world.org}/hear", json={"text": "could I get a trace of his heart"}, headers=world.api.headers(world.me))
    h0 = hear().json()
    assert asked == [] and (h0["status"], h0["source"], h0["interpreter"], h0["fallback_reason"]) == ("menu", "grammar", "offline_grammar", "NO_CONSENT")      # the Terms alone are not consent: the local model's reading is dropped and the grammar (with its question guard) answers
    asked.clear()
    world.consent(True)
    h1 = hear().json()
    assert asked == ["gemma4:31b-cloud"] and (h1["status"], h1["source"], h1["interpreter"], h1["interpreter_model"], h1["interpreter_reported_model"], h1["fallback_reason"]) == ("confirm", "model", "gemma_cloud", "gemma4:31b-cloud", "gemma4:31b", "")        # approval and the person's own yes together: Gemma reads it, and the read-back still waits for the trainee's yes
    asked.clear()
    world.consent(False)
    h2 = hear().json()
    assert asked == [] and (h2["status"], h2["source"], h2["interpreter"], h2["fallback_reason"]) == ("menu", "grammar", "offline_grammar", "NO_CONSENT")        # withdrawn: nothing from the local model is used
