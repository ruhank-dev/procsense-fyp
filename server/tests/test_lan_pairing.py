"""Pairing and version checks of the networked Quest demonstration (P3-NET-07, P3-NET-10)."""
import json
import re
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from server import config, database
from server.lan import compat
from server.lan.pairing import PairingStore, get_store, reset_store
from server.main import create_app


@pytest.fixture
def lan_app(monkeypatch, tmp_path):
    monkeypatch.setenv("DATABASE_URL", "")
    monkeypatch.setenv("LAN_PROFILE", "true")
    monkeypatch.setenv("LAN_PAIRING_FILE", str(tmp_path / "pairing.json"))
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()
    with TestClient(create_app()) as client:
        yield client
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()


def test_store_exchange_and_revoke(tmp_path):
    store = PairingStore(tmp_path / "p.json")
    assert re.fullmatch(r"\d{6}", store.code)
    assert store.exchange("000000" if store.code != "000000" else "111111") is None
    device_id, token = store.exchange(store.code, "quest")
    assert store.verify(token) and not store.verify("nope")
    assert PairingStore(tmp_path / "p.json").verify(token), "a laptop restart keeps the paired headset"
    assert token not in (tmp_path / "p.json").read_text(), "only the hash is stored"
    assert store.revoke(device_id) and not store.verify(token)


def test_five_wrong_codes_lock_the_exchange(tmp_path):
    now = [0.0]
    store = PairingStore(tmp_path / "p.json", clock=lambda: now[0])
    wrong = "000000" if store.code != "000000" else "111111"
    for _ in range(5):
        assert store.exchange(wrong) is None
    assert store.locked() and store.exchange(store.code) is None, "even the right code waits out the lock"
    now[0] += 61
    assert store.exchange(store.code) is not None


def test_ai_routes_need_a_token_in_the_lan_profile(lan_app):
    assert lan_app.get("/health").status_code == 200
    assert lan_app.get("/health").json()["lan_profile"] is True
    blocked = lan_app.post("/asr/organisations/x/transcribe")
    assert blocked.status_code == 401 and blocked.json()["detail"] == "pairing_required"
    r = lan_app.post("/pairing/exchange", json={"code": get_store().code, "device_name": "quest"})
    assert r.status_code == 200
    token = r.json()["device_token"]
    # with a token the guard lets the request through (the route itself may then refuse for its own reasons, but not with 401/426)
    passed = lan_app.post("/asr/organisations/x/transcribe", headers={"Authorization": f"Bearer {token}"})
    assert passed.json().get("detail") not in ("pairing_required",) and passed.status_code != 426


def test_wrong_code_and_lockout_over_http(lan_app):
    wrong = "000000" if get_store().code != "000000" else "111111"
    assert lan_app.post("/pairing/exchange", json={"code": wrong}).status_code == 403
    for _ in range(4):
        lan_app.post("/pairing/exchange", json={"code": wrong})
    assert lan_app.post("/pairing/exchange", json={"code": get_store().code}).status_code == 429


def test_incompatible_engine_is_refused_with_426(lan_app):
    token = lan_app.post("/pairing/exchange", json={"code": get_store().code}).json()["device_token"]
    r = lan_app.post("/orders/organisations/x/hear", headers={"Authorization": f"Bearer {token}", "X-ProcSense-Engine": "9.9.9"})
    assert r.status_code == 426 and "engine_version_unsupported" in r.json()["detail"]


def test_devices_can_be_listed_and_revoked_from_the_laptop(lan_app):
    lan_app.post("/pairing/exchange", json={"code": get_store().code, "device_name": "quest"})
    listed = lan_app.get("/pairing/devices").json()
    assert listed and listed[0]["name"] == "quest"
    assert lan_app.delete(f"/pairing/devices/{listed[0]['id']}").status_code == 200
    assert lan_app.get("/pairing/devices").json() == []


def test_pairing_is_off_without_the_lan_profile(app_without_database):
    assert app_without_database.post("/pairing/exchange", json={"code": "123456"}).status_code == 404
    assert app_without_database.get("/health").json()["lan_profile"] is False


def test_compat_table_lists_the_engine_version_in_the_source():
    core = Path(__file__).resolve().parents[2] / "src" / "ProcSense.Core" / "CoreInfo.cs"
    version = re.search(r'Version\s*=\s*"([^"]+)"', core.read_text(encoding="utf-8")).group(1)
    assert version in compat.load_table()["engine_versions"], "raise content/ai/compat.json together with CoreInfo.Version"
    assert compat.check({"x-procsense-engine": version}) is None
    assert compat.check({}) is None, "an old client without the header is not refused"
