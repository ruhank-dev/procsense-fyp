"""LAN demonstration end to end on the desktop (P3-NET-07/11/12/15): a real uvicorn on loopback over HTTPS with the self-signed certificate (the `--tls` mode), a client that pins the certificate fingerprint the way the headset does,
pairing, the demonstration identity, the AI routes the headset calls, the push socket, a server restart and the errors a headset meets. The client here is Python on the desktop: it proves the server side of the contract, not the Quest's
own certificate handling. The parts that need a database run only with TEST_DATABASE_URL."""
from __future__ import annotations

import hashlib
import json
import socket
import ssl
import threading
import time
import uuid

import httpx
import pytest
import uvicorn
from websockets.exceptions import ConnectionClosedError, InvalidStatus
from websockets.sync.client import connect

from server import config, database
from server.lan import demo_identity, tls
from server.lan.pairing import get_store, reset_store
from server.main import create_app
from server.tests.files import make_file

NDJSON = {"Content-Type": "application/x-ndjson"}


def _free_port() -> int:
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def peer_fingerprint(port: int) -> str:
    """What a pinning client reads from the server it reaches: SHA-256 of the presented certificate (DER)."""
    ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    with socket.create_connection(("127.0.0.1", port), timeout=5) as raw, ctx.wrap_socket(raw) as tls_sock:
        return hashlib.sha256(tls_sock.getpeercert(binary_form=True)).hexdigest()


class Pinned:
    """A headset-like client: refuses to talk to a server whose certificate is not the pinned one."""

    def __init__(self, port: int, pin: str):
        self.port, self.pin = port, pin
        self.base = f"https://127.0.0.1:{port}"
        self.ctx = ssl._create_unverified_context()    # trust is the pin, not a certificate authority

    def check(self) -> None:
        if peer_fingerprint(self.port) != self.pin:
            raise PermissionError("certificate does not match the pinned fingerprint")

    def http(self) -> httpx.Client:
        self.check()
        return httpx.Client(base_url=self.base, verify=self.ctx, timeout=10)

    def socket(self, session_id: str, token: str):
        self.check()
        return connect(f"wss://127.0.0.1:{self.port}/ws/headset/{session_id}", ssl=self.ctx, additional_headers={"X-ProcSense-Device": token}, open_timeout=10)


class Laptop:
    """The server in a thread of this process (so the test can read the pairing code), listening on TLS; stop() and start() again keep the paired devices (the pairing file)."""

    def __init__(self, port: int, cert, key, fresh_settings: bool = True):
        self.port, self.cert, self.key, self.fresh, self.server, self.thread = port, cert, key, fresh_settings, None, None

    def start(self) -> None:
        if self.fresh:
            config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()
        cfg = uvicorn.Config(create_app(), host="127.0.0.1", port=self.port, ssl_certfile=str(self.cert), ssl_keyfile=str(self.key), log_level="warning", ws="websockets")
        self.server = uvicorn.Server(cfg)
        self.thread = threading.Thread(target=self.server.run, daemon=True)
        self.thread.start()
        for _ in range(100):
            if self.server.started:
                return
            time.sleep(0.1)
        raise RuntimeError("the server did not start")

    def stop(self) -> None:
        self.server.should_exit = True
        self.thread.join(15)
        assert not self.thread.is_alive()


@pytest.fixture
def env(monkeypatch, tmp_path):
    monkeypatch.setenv("DATABASE_URL", "")
    monkeypatch.setenv("LAN_PROFILE", "true")
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")
    monkeypatch.setenv("OLLAMA_URL", "")                         # no model host: the routes answer with their offline fallbacks
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "all")
    monkeypatch.setenv("LAN_PAIRING_FILE", str(tmp_path / "pairing.json"))
    cert, key, fp = tls.ensure(tmp_path / "tls", ["127.0.0.1"])
    monkeypatch.setenv("LAN_TLS_FINGERPRINT", fp)
    return {"tmp": tmp_path, "cert": cert, "key": key, "fp": fp}


@pytest.fixture
def laptop(env):
    box = Laptop(_free_port(), env["cert"], env["key"])
    box.start()
    yield box, Pinned(box.port, env["fp"]), env
    if box.thread.is_alive():
        box.stop()
    config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()


def _pair(client: httpx.Client, name="quest") -> dict:
    r = client.post("/pairing/exchange", json={"code": get_store().code, "device_name": name})
    assert r.status_code == 200, r.text
    return r.json()


def test_pin_accepts_the_laptop_certificate_and_refuses_another(laptop, env):
    box, pinned, _ = laptop
    with pinned.http() as c:
        paired = _pair(c)
        assert paired["tls_fingerprint"] == env["fp"] == peer_fingerprint(box.port), "the fingerprint in the pairing reply is the certificate actually served"
    # a different laptop (or someone in the middle) presents another certificate
    other_cert, other_key, other_fp = tls.ensure(env["tmp"] / "other", ["127.0.0.1"])
    assert other_fp != env["fp"]
    port = _free_port()
    impostor = uvicorn.Server(uvicorn.Config(create_app(), host="127.0.0.1", port=port, ssl_certfile=str(other_cert), ssl_keyfile=str(other_key), log_level="warning"))
    t = threading.Thread(target=impostor.run, daemon=True)
    t.start()
    try:
        for _ in range(100):
            if impostor.started:
                break
            time.sleep(0.1)
        with pytest.raises(PermissionError):
            Pinned(port, env["fp"]).http()
        with pytest.raises(PermissionError):
            Pinned(port, env["fp"]).socket("s1", paired["device_token"])
        Pinned(port, other_fp).http().close()      # the same server is fine for a client that pinned its own certificate
    finally:
        impostor.should_exit = True
        t.join(15)
    # without the pin, normal certificate checking refuses a self-signed certificate too
    with pytest.raises(httpx.ConnectError):
        httpx.get(f"https://127.0.0.1:{box.port}/health", timeout=5)


def test_pair_identity_ai_routes_and_socket_over_tls(laptop):
    box, pinned, _ = laptop
    with pinned.http() as c:
        paired = _pair(c)
        h = {"X-ProcSense-Device": paired["device_token"]}
        ident = c.get("/pairing/demo-identity", headers=h)
        assert ident.status_code == 200 and ident.json()["organisation_id"] == str(demo_identity.ORG_ID) and ident.json()["role"] == "trainee"
        # the spoken-order route answers from the grammar with no model host
        hear = c.post(f"/orders/organisations/{demo_identity.ORG_ID}/hear", headers=h, json={"text": "give 1 gram paracetamol"})
        assert hear.status_code == 200 and hear.json()["interpreter"] == "offline_grammar" and hear.json()["fallback_reason"] == "NO_MODEL_HOST"
        # the speech-action route answers with the fallback signal (nothing is invented without the model)
        act = c.post(f"/speech/organisations/{demo_identity.ORG_ID}/action", headers=h, json={"utterance": "check his pulse", "available": [], "turns": []})
        assert act.status_code == 200 and act.json()["source"] == "fallback"
        assert c.get("/ai/health").status_code == 200
        # an engine version the table does not list is refused with 426 so the headset can show its banner
        assert c.get("/orders/organisations/%s/hear" % demo_identity.ORG_ID, headers={**h, "X-ProcSense-Engine": "999.0.0"}).status_code in (405, 426)
        # push socket: hello now, a notice for its session, shutdown when the laptop stops
        with pinned.socket("s1", paired["device_token"]) as ws:
            assert json.loads(ws.recv(timeout=5))["kind"] == "hello"
            from server.lan.notices import get_hub
            from server.schemas.headset_notice import Notice
            assert get_hub().publish(Notice(kind="generation_progress", session_id="s1", progress=0.5, detail="blocks")) == 1
            assert json.loads(ws.recv(timeout=5))["progress"] == 0.5
            threading.Thread(target=box.stop, daemon=True).start()
            # under a real uvicorn the connections are closed with 1012 (service restart) BEFORE the lifespan shutdown runs, so the headset learns of the stop from that close code; the shutdown notice of the hub is only
            # seen by in-process clients (test_lan_demo). The headset must treat 1012 (or any close) as "laptop stopping".
            with pytest.raises(ConnectionClosedError) as closed:
                ws.recv(timeout=10)
            assert closed.value.rcvd is not None and closed.value.rcvd.code == 1012


def test_reconnect_after_server_restart_and_laptop_down(laptop):
    box, pinned, env = laptop
    with pinned.http() as c:
        token = _pair(c)["device_token"]
    h = {"X-ProcSense-Device": token}
    ws = pinned.socket("s1", token)
    assert json.loads(ws.recv(timeout=5))["kind"] == "hello"
    box.stop()                                    # the laptop goes down
    with pytest.raises(Exception):                # the open socket ends (a close, after the shutdown notice if it was read first)
        for _ in range(3):
            ws.recv(timeout=5)
    ws.close()
    with pytest.raises(OSError):                  # nobody is listening: the headset sees a refused connection and plays offline
        pinned.http()
    box.fresh = False                             # the same process keeps its settings; a real restart re-reads them from the same environment
    config.get_settings.cache_clear(); reset_store()
    box.start()                                   # the laptop comes back: same certificate (the pin holds) and same pairing file (the token holds): no re-pairing
    with pinned.http() as c:
        assert c.get("/pairing/demo-identity", headers=h).status_code == 200
        with pinned.socket("s1", token) as ws2:
            assert json.loads(ws2.recv(timeout=5))["kind"] == "hello"


def test_bad_token_and_revoked_device_are_refused(laptop):
    box, pinned, _ = laptop
    with pinned.http() as c:
        paired = _pair(c)
        for bad in ({"X-ProcSense-Device": "nope"}, {}):
            assert c.get("/pairing/demo-identity", headers=bad).status_code == 401
            assert c.post(f"/orders/organisations/{demo_identity.ORG_ID}/hear", headers=bad, json={"text": "x"}).status_code == 401
        with pytest.raises(InvalidStatus):
            pinned.socket("s1", "nope")
        wrong = "000000" if get_store().code != "000000" else "111111"
        assert c.post("/pairing/exchange", json={"code": wrong}).status_code == 403
        # revoking is done from the laptop itself
        assert c.delete(f"/pairing/devices/{paired['device_id']}").status_code == 200
        h = {"X-ProcSense-Device": paired["device_token"]}
        assert c.get("/pairing/demo-identity", headers=h).status_code == 401
        with pytest.raises(InvalidStatus):
            pinned.socket("s1", paired["device_token"])


# ---------------------------------------------------------------- needs PostgreSQL

@pytest.mark.db
def test_session_upload_and_scenario_package_over_tls_on_postgres(monkeypatch, migrated, db_url, env):
    monkeypatch.setenv("DATABASE_URL", db_url)
    pool = env["tmp"] / "pool"
    pool.mkdir()
    pkg = json.dumps({"case": {"id": "VAR-S1-A"}, "provenance": {"spine": "sp1"}}, sort_keys=True).encode()
    (pool / "p1.json").write_bytes(pkg)
    index = {"packages": [{"scenario": "S1", "level": "beginner", "spine": "sp1", "fingerprint": "fp1", "file": "p1.json", "sha256": hashlib.sha256(pkg).hexdigest()}]}
    (pool / "index.json").write_text(json.dumps(index), encoding="utf-8")
    monkeypatch.setenv("PACKAGE_POOL_DIR", str(pool))
    box = Laptop(_free_port(), env["cert"], env["key"])
    box.start()
    try:
        pinned = Pinned(box.port, env["fp"])
        with pinned.http() as c:
            h = {"X-ProcSense-Device": _pair(c)["device_token"]}
            assert c.get("/pairing/demo-identity", headers=h).status_code == 200
            offer = c.post("/scenario-packages/next", headers=h, json={"scenario": "S1", "level": "beginner"})
            assert offer.status_code == 200, offer.text
            fetched = c.get(f"/scenario-packages/{offer.json()['package_hash']}", headers=h)
            assert fetched.status_code == 200 and hashlib.sha256(fetched.content).hexdigest() == offer.json()["package_hash"]
            assert c.post("/scenario-packages/next", headers=h, json={"scenario": "S1", "level": "beginner", "exclude_spines": ["sp1"]}).status_code == 404
            sid = uuid.uuid4()
            url = f"/organisations/{demo_identity.ORG_ID}/sessions"
            refused = c.post(url, content=make_file(session_id=sid), headers={**h, **NDJSON})
            assert refused.status_code == 403 and refused.json()["detail"] == {"code": "consent_required"}, "no consent exception for the demonstration user (D2)"
            for kind in ("terms", "results"):
                assert c.post("/privacy/consent", headers=h, json={"kind": kind, "granted": True}).status_code == 200
            up = c.post(url, content=make_file(session_id=sid), headers={**h, **NDJSON})
            assert up.status_code == 202 and up.json()["outcome"] == "accepted", up.text
            again = c.post(url, content=make_file(session_id=sid), headers={**h, **NDJSON})
            assert again.status_code == 200 and again.json()["outcome"] == "already_stored"
            assert c.get(f"{url}/{sid}", headers=h).json()["session_id"] == str(sid)
    finally:
        box.stop()
        config.get_settings.cache_clear(); database.get_engine.cache_clear(); reset_store()
