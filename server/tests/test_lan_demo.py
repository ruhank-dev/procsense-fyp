"""Demonstration identity, headset socket and self-signed certificate of the networked Quest demonstration (P3-NET-11, P3-NET-12, P3-NET-15). No database needed."""
import datetime as dt
import uuid
from pathlib import Path

import pytest
from fastapi import Depends
from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

from server import config, database
from server.lan import demo_identity, tls
from server.lan.notices import get_hub
from server.lan.pairing import get_store, reset_store
from server.main import create_app
from server.middleware.audit import audited
from server.schemas.headset_notice import Notice


def _env(monkeypatch, tmp_path, **env):
    monkeypatch.setenv("DATABASE_URL", "")
    monkeypatch.setenv("LAN_PROFILE", "true")
    monkeypatch.setenv("LAN_PAIRING_FILE", str(tmp_path / "pairing.json"))
    for k, v in env.items():
        monkeypatch.setenv(k, v)
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()


def _reset():
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()


@pytest.fixture
def demo(monkeypatch, tmp_path):
    _env(monkeypatch, tmp_path, CLOUD_SYNTHETIC_DEPLOYMENT="1")
    app = create_app()
    app.add_api_route("/asr/organisations/{organisation_id}/probe", lambda record=Depends(audited("asr")): {"user": str(record.user_id), "org": str(record.organisation_id)})
    with TestClient(app) as client:
        yield client
    _reset()


def _pair(client, name="quest"):
    return client.post("/pairing/exchange", json={"code": get_store().code, "device_name": name}).json()["device_token"]


def test_paired_headset_is_the_demo_user_without_a_database(demo):
    token = _pair(demo)
    r = demo.get("/pairing/demo-identity", headers={"X-ProcSense-Device": token})
    assert r.status_code == 200
    body = r.json()
    assert body["organisation_id"] == str(demo_identity.ORG_ID) and body["role"] == "trainee" and body["pseudonym"].startswith("demo-")
    assert uuid.UUID(body["user_id"])
    # the token as the bearer (what the headset sends on authorised calls) gives the same user
    assert demo.get("/pairing/demo-identity", headers={"Authorization": f"Bearer {token}"}).json()["user_id"] == body["user_id"]


def test_ai_route_resolves_the_demo_user_and_only_its_organisation(demo):
    h = {"X-ProcSense-Device": _pair(demo)}
    ok = demo.get(f"/asr/organisations/{demo_identity.ORG_ID}/probe", headers=h)
    assert ok.status_code == 200 and ok.json()["org"] == str(demo_identity.ORG_ID)
    assert demo.get(f"/asr/organisations/{uuid.uuid4()}/probe", headers=h).status_code == 404


def test_each_headset_is_its_own_demo_user(demo):
    ids = {demo.get("/pairing/demo-identity", headers={"X-ProcSense-Device": _pair(demo, n)}).json()["user_id"] for n in ("a", "b")}
    assert len(ids) == 2


def test_unknown_or_revoked_device_is_refused(demo):
    assert demo.get("/pairing/demo-identity", headers={"X-ProcSense-Device": "nope"}).status_code == 401
    token = _pair(demo)
    assert demo.delete(f"/pairing/devices/{get_store().devices()[0]['id']}").status_code == 200
    assert demo.get("/pairing/demo-identity", headers={"X-ProcSense-Device": token}).status_code == 401


def test_real_sign_in_beside_the_device_header_is_not_the_demo_user(demo):
    r = demo.get("/pairing/demo-identity", headers={"X-ProcSense-Device": _pair(demo), "Authorization": "Bearer not-the-pair-token"})
    assert r.status_code == 401, "a real (here invalid) login is judged as itself, never replaced by the demo user"


def test_no_demo_user_without_the_synthetic_declaration(monkeypatch, tmp_path):
    _env(monkeypatch, tmp_path)
    with TestClient(create_app()) as client:
        assert client.get("/pairing/demo-identity", headers={"X-ProcSense-Device": _pair(client)}).status_code == 401
    _reset()


def test_no_demo_user_in_prod(monkeypatch, tmp_path):
    _env(monkeypatch, tmp_path, CLOUD_SYNTHETIC_DEPLOYMENT="1")
    token = get_store().exchange(get_store().code)[1]
    request = type("R", (), {"headers": {"x-procsense-device": token}})()
    assert demo_identity.device_for(request, config.get_settings()) is not None
    assert demo_identity.device_for(request, config.Settings(_env_file=None, lan_profile=True, cloud_synthetic_deployment=False)) is None
    _reset()


# ---------------------------------------------------------------- socket

def test_socket_refuses_without_a_paired_token(demo):
    with pytest.raises(WebSocketDisconnect):
        with demo.websocket_connect("/ws/headset/s1"):
            pass
    with pytest.raises(WebSocketDisconnect):
        with demo.websocket_connect("/ws/headset/s1", headers={"X-ProcSense-Device": "nope"}):
            pass


def test_socket_pushes_progress_quota_and_shutdown_to_its_session_only(demo):
    h = {"X-ProcSense-Device": _pair(demo)}
    with demo.websocket_connect("/ws/headset/s1", headers=h) as mine, demo.websocket_connect("/ws/headset/s2", headers=h) as other:
        assert mine.receive_json()["kind"] == "hello" and other.receive_json()["kind"] == "hello"
        hub = get_hub()
        assert hub.publish(Notice(kind="generation_progress", session_id="s1", progress=0.5, detail="blocks")) == 1
        assert mine.receive_json() == {"kind": "generation_progress", "session_id": "s1", "progress": 0.5, "detail": "blocks"}
        hub.publish(Notice(kind="quota", session_id="s1", detail="cloud_quota_exhausted"))
        assert mine.receive_json()["kind"] == "quota"
        assert hub.shutdown() == 2
        assert mine.receive_json()["kind"] == "shutdown" and other.receive_json()["kind"] == "shutdown"
    assert get_hub().listeners() == 0


def test_play_works_without_the_socket(demo):
    assert demo.get(f"/asr/organisations/{demo_identity.ORG_ID}/probe", headers={"X-ProcSense-Device": _pair(demo)}).status_code == 200


# ---------------------------------------------------------------- TLS

def test_certificate_covers_the_lan_address_and_is_reused(tmp_path):
    cert, _, fp = tls.ensure(tmp_path, ["192.168.1.20"])
    assert len(fp) == 64 and fp == tls.fingerprint(cert.read_bytes())
    import ssl
    ssl.create_default_context(ssl.Purpose.CLIENT_AUTH).load_cert_chain(cert, cert.with_name("demo-key.pem"))   # the server can load the pair
    assert tls.ensure(tmp_path, ["192.168.1.20"])[2] == fp, "the pin stays valid across restarts"


def test_new_address_or_expiry_makes_a_different_certificate(tmp_path):
    fp = tls.ensure(tmp_path, ["192.168.1.20"])[2]
    wider = tls.ensure(tmp_path, ["192.168.1.20", "10.0.0.5"])[2]
    assert wider != fp
    later = dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=360)
    assert tls.ensure(tmp_path, ["192.168.1.20", "10.0.0.5"], now=later)[2] != wider


def test_two_certificates_never_share_a_fingerprint_and_the_key_is_not_in_the_cert():
    a, b = tls.make(["192.168.1.20"]), tls.make(["192.168.1.20"])
    assert tls.fingerprint(a[0]) != tls.fingerprint(b[0]), "a different certificate is refused by a pinned headset"
    assert b"PRIVATE" not in a[0] and b"PRIVATE" in a[1]


def test_pairing_reply_carries_the_fingerprint(monkeypatch, tmp_path):
    _env(monkeypatch, tmp_path, LAN_TLS_FINGERPRINT="ab" * 32)
    with TestClient(create_app()) as client:
        assert client.post("/pairing/exchange", json={"code": get_store().code}).json()["tls_fingerprint"] == "ab" * 32
    _reset()


def test_demo_laptop_tls_flag_is_wired():
    text = (Path(__file__).resolve().parents[2] / "tools" / "setup" / "demo_laptop.py").read_text(encoding="utf-8")
    assert '"--tls" in argv' in text and "--ssl-certfile" in text and "LAN_TLS_FINGERPRINT" in text


def test_no_consent_exception_without_a_database(demo):
    """Owner decision D2: without a database nothing can be agreed to or kept, so the demonstration user's upload is refused for missing consent (never waved through) and an answer is refused."""
    from server.tests.files import make_file
    h = {"X-ProcSense-Device": _pair(demo)}
    state = demo.get("/privacy/consent", headers=h)
    assert state.status_code == 200 and state.json()["needs_consent"] is True
    assert demo.post("/privacy/consent", headers=h, json={"kind": "results", "granted": True}).status_code == 503
    r = demo.post(f"/organisations/{demo_identity.ORG_ID}/sessions", content=make_file(session_id=uuid.uuid4()), headers={**h, "Content-Type": "application/x-ndjson"})
    assert (r.status_code, r.json()["detail"]) == (403, {"code": "consent_required"})
