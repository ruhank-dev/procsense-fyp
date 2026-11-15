"""The demonstration identity on a real PostgreSQL (P3-NET-11): the rows are made on first use, once, and a paired headset's session upload is stored on the laptop once the demonstration user
has agreed through the normal consent route (owner decision D2: no consent exception). Runs only with TEST_DATABASE_URL."""
import uuid

import pytest
from sqlalchemy import func, select
from sqlalchemy.orm import Session as DbSession

from server import config, database
from server.lan import demo_identity
from server.lan.pairing import get_store, reset_store
from server.main import create_app
from server.models.accounts import ConsentRecord, Membership, Organisation, Profile
from server.models.sessions import Session
from server.tests.files import make_file
from fastapi.testclient import TestClient

pytestmark = pytest.mark.db
NDJSON = {"Content-Type": "application/x-ndjson"}


@pytest.fixture
def lan(monkeypatch, migrated, db_url, tmp_path):
    monkeypatch.setenv("DATABASE_URL", db_url)
    monkeypatch.setenv("LAN_PROFILE", "true")
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")
    monkeypatch.setenv("LAN_PAIRING_FILE", str(tmp_path / "pairing.json"))
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()
    with TestClient(create_app()) as client:
        yield client, database.get_engine()
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()


def _pair(client, name="quest"):
    return client.post("/pairing/exchange", json={"code": get_store().code, "device_name": name}).json()["device_token"]


def _consent(client, h, granted=True, kinds=("terms", "results")):
    r = None
    for kind in kinds:
        r = client.post("/privacy/consent", headers=h, json={"kind": kind, "granted": granted})
        assert r.status_code == 200, r.text
    return r.json()


def _counts(engine):
    with DbSession(engine) as s:
        return tuple(s.scalar(select(func.count()).select_from(m)) for m in (Organisation, Profile, Membership))


def test_ensure_rows_is_idempotent_on_postgres(lan):
    _, engine = lan
    for _ in range(2):
        with DbSession(engine) as s:
            profile, org, memberships = demo_identity.ensure_rows(s, "dev-1")
            assert org.id == demo_identity.ORG_ID and profile.user_id == demo_identity.user_id_for("dev-1") and memberships[0].role == "trainee"
    assert _counts(engine) == (1, 1, 1)
    with DbSession(engine) as s:
        demo_identity.ensure_rows(s, "dev-2")
    assert _counts(engine) == (1, 2, 2), "one shared organisation, one user per headset"


def test_paired_headset_identity_and_upload_are_stored(lan):
    client, engine = lan
    token = _pair(client)
    h = {"X-ProcSense-Device": token}
    ident = client.get("/pairing/demo-identity", headers=h)
    assert ident.status_code == 200
    user_id = ident.json()["user_id"]
    assert _counts(engine) == (1, 1, 1), "the rows were made by the first call"
    assert _consent(client, h)["needs_consent"] is False
    sid = uuid.uuid4()
    url = f"/organisations/{demo_identity.ORG_ID}/sessions"
    r = client.post(url, content=make_file(session_id=sid), headers={**h, **NDJSON})
    assert r.status_code == 202, r.text
    assert r.json()["outcome"] == "accepted"
    # the same file again: stored once
    assert client.post(url, content=make_file(session_id=sid), headers={**h, **NDJSON}).status_code in (200, 409)
    with DbSession(engine) as s:
        row = s.get(Session, sid)
        assert row is not None and str(row.user_id) == user_id and row.organisation_id == demo_identity.ORG_ID
    assert client.get(f"{url}/{sid}", headers=h).json()["session_id"] == str(sid)
    # another headset cannot read it
    other = {"X-ProcSense-Device": _pair(client, "second")}
    assert client.get(f"{url}/{sid}", headers=other).status_code == 404
    # an outsider organisation is not found
    assert client.get(f"/organisations/{uuid.uuid4()}/sessions/{sid}", headers=h).status_code == 404


def test_live_generation_tells_the_headset_socket(lan, monkeypatch):
    """The generation route publishes progress to the socket of the session named in the request (a stand-in pipeline: the wiring is under test, not the generator)."""
    from types import SimpleNamespace
    from server.generation import pipeline
    from server.routers import scenario_packages
    client, _ = lan
    monkeypatch.setenv("LIVE_GENERATION", "1")
    config.get_settings.cache_clear()
    monkeypatch.setattr(scenario_packages, "_context", lambda: None)
    monkeypatch.setattr(pipeline, "generate", lambda *a, **k: SimpleNamespace(package=None, fallback="test"))
    h = {"X-ProcSense-Device": _pair(client)}
    with client.websocket_connect("/ws/headset/s9", headers=h) as ws:
        assert ws.receive_json()["kind"] == "hello"
        r = client.post("/scenario-packages/generate", headers=h, json={"scenario": "S1", "level": "beginner", "session_id": "s9"})
        assert r.status_code == 404 and r.json()["detail"]["code"] == "pool_fallback"
        assert ws.receive_json() == {"kind": "generation_progress", "session_id": "s9", "progress": 0.1, "detail": "generating"}
        assert ws.receive_json()["detail"] == "pool_fallback"


# ---------------------------------------------------------------- consent (owner decision D2: the demonstration user follows the normal consent workflow)

def _upload(client, h, sid=None):
    return client.post(f"/organisations/{demo_identity.ORG_ID}/sessions", content=make_file(session_id=sid or uuid.uuid4()), headers={**h, **NDJSON})


def _stored(engine):
    with DbSession(engine) as s:
        return s.scalar(select(func.count()).select_from(Session))


def _answers(engine):
    with DbSession(engine) as s:
        return {(r.kind, r.granted, r.via) for r in s.scalars(select(ConsentRecord))}


def test_upload_without_consent_is_refused_and_nothing_is_stored(lan):
    client, engine = lan
    h = {"X-ProcSense-Device": _pair(client)}
    state = client.get("/privacy/consent", headers=h).json()
    assert state["needs_consent"] is True and {k["kind"] for k in state["kinds"] if k["required"]} == {"terms", "results"}
    r = _upload(client, h)
    assert (r.status_code, r.json()["detail"]) == (403, {"code": "consent_required"})
    assert _stored(engine) == 0 and _answers(engine) == set(), "consent is never granted automatically"


def test_declined_consent_is_recorded_and_refuses_the_upload(lan):
    client, engine = lan
    h = {"X-ProcSense-Device": _pair(client)}
    assert _consent(client, h, granted=False)["needs_consent"] is True
    r = _upload(client, h)
    assert (r.status_code, r.json()["detail"]) == (403, {"code": "consent_required"})
    assert _stored(engine) == 0
    assert _answers(engine) == {("terms", False, "headset"), ("results", False, "headset")}


def test_granted_consent_lets_the_upload_through_and_withdrawal_stops_it(lan):
    client, engine = lan
    h = {"X-ProcSense-Device": _pair(client)}
    _consent(client, h, kinds=("terms",))
    assert _upload(client, h).status_code == 403, "terms alone is not consent to store results"
    _consent(client, h, kinds=("results",))
    sid = uuid.uuid4()
    r = _upload(client, h, sid)
    assert r.status_code == 202 and r.json()["outcome"] == "accepted", r.text
    with DbSession(engine) as s:
        assert s.get(Session, sid) is not None
    assert _answers(engine) == {("terms", True, "headset"), ("results", True, "headset")}
    _consent(client, h, granted=False, kinds=("results",))   # the latest answer counts
    assert _upload(client, h).status_code == 403
    assert _stored(engine) == 1


def test_consent_is_per_headset_user(lan):
    client, _ = lan
    first = {"X-ProcSense-Device": _pair(client)}
    second = {"X-ProcSense-Device": _pair(client, "second")}
    _consent(client, first)
    assert _upload(client, first).status_code == 202
    assert _upload(client, second).status_code == 403, "one headset's answer is not another's"


def test_unauthorised_callers_cannot_answer_or_upload(lan):
    client, engine = lan
    assert client.post("/privacy/consent", json={"kind": "results", "granted": True}).status_code == 401
    assert client.post("/privacy/consent", headers={"X-ProcSense-Device": "not-a-token"}, json={"kind": "results", "granted": True}).status_code == 401
    h = {"X-ProcSense-Device": _pair(client)}
    _consent(client, h)
    device_id = get_store().devices()[0]["id"]
    assert client.delete(f"/pairing/devices/{device_id}").status_code == 200
    assert client.post("/privacy/consent", headers=h, json={"kind": "results", "granted": True}).status_code == 401
    assert _upload(client, h).status_code == 401
    assert _stored(engine) == 0
    other = {"X-ProcSense-Device": _pair(client, "second")}
    for path in ("/privacy/access-log", "/privacy/export"):   # web-only routes stay web-only for the demonstration user
        assert client.get(path, headers=other).status_code == 403
