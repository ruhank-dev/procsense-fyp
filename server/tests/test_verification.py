"""Verifying sessions (p1-backend-s09): the worker, the runner, the sweeper route, and the whole path with the real engine when it is available.

Most tests use a stand-in for the engine (tests/fake_engine.py) so the service's own logic is tested exactly; the tests at the end use the real command-line engine and run when ENGINE_CLI is set (task ci and CI set it).
"""
from __future__ import annotations

import gzip
import hashlib
import json
import os
import shutil
import subprocess
import sys
import threading
import uuid
from pathlib import Path

import pytest
from sqlalchemy import func, select
from sqlalchemy.orm import Session as DbSession

from server import config
from server.models.sessions import Session, SessionBehaviour, SessionScoreItem
from server.repositories import accounts
from server.tests.files import make_file
from server.verification import worker
from server.verification.runner import EngineRunner, RunnerError, clean_environment

pytestmark = pytest.mark.db
NDJSON = {"Content-Type": "application/x-ndjson"}
VERSION = "0123456789ab-practice"
FAKE = Path(__file__).resolve().parent / "fake_engine.py"


@pytest.fixture
def world(api, tmp_path, monkeypatch):
    """An organisation with a trainee, a content folder for one version, and the stand-in engine configured."""
    root = tmp_path / "content_root"
    (root / VERSION).mkdir(parents=True)
    monkeypatch.setenv("ENGINE_CLI", json.dumps([sys.executable, str(FAKE)]))
    monkeypatch.setenv("ENGINE_CONTENT_ROOT", str(root))
    monkeypatch.setenv("VERIFY_ON_UPLOAD", "false")   # the tests run the worker themselves
    config.get_settings.cache_clear()
    worker.get_runner.cache_clear()

    def build(s):
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        trainee = api.person(s)
        accounts.add_membership(s, trainee, org.id, "trainee")
        return org.id, trainee
    org, trainee = api.seed(build)

    class W:
        pass
    w = W()
    w.api, w.org, w.trainee, w.root = api, org, trainee, root
    w.headers = {**api.headers(trainee), **NDJSON}

    def upload(**options) -> uuid.UUID:
        sid = options.pop("session_id", None) or uuid.uuid4()
        r = api.client.post(f"/organisations/{org}/sessions", content=make_file(session_id=sid, content_version=options.pop("content_version", VERSION), **options), headers=w.headers)
        assert r.status_code == 202, r.text
        return sid
    w.upload = upload

    def row(sid) -> Session:
        with DbSession(api.engine) as s:
            r = s.get(Session, sid)
            s.expunge(r)
            return r
    w.row = row
    return w


def sweep(world, limit=5):
    return worker.verify_pending(limit)


# ---------------------------------------------------------------- verified

def test_a_session_the_engine_verifies_gets_its_result_its_score_rows_and_its_report(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    sid = world.upload()
    assert world.row(sid).status == "pending"
    assert sweep(world) == {"processed": 1, "configured": True}
    row = world.row(sid)
    assert (row.status, row.reject_reason, row.end_state, row.result) == ("verified", "", "READY_SAFE", "SUCCESS")
    assert row.verified_at is not None and row.report_json["schema"] == "debrief_facts.v1"
    assert b"report" in gzip.decompress(row.report_html_gz)
    with DbSession(world.api.engine) as s:
        items = s.scalars(select(SessionScoreItem).where(SessionScoreItem.session_id == sid).order_by(SessionScoreItem.ordinal)).all()
        assert [(i.item_id, i.behaviour, float(i.points), i.evidence) for i in items] == [("credit.test_appropriate", "b16", 1.0, [2, 5]), ("minor.no_introduction", "b13", -1.0, [])]
        assert items[0].step > 0 and items[1].step == 0   # the step of the first evidence action; none for an item with no action
        behaviours = {b.behaviour: (b.credits, b.penalties, b.net) for b in s.scalars(select(SessionBehaviour).where(SessionBehaviour.session_id == sid))}
        assert behaviours == {"b16": (1, 0, 1), "b13": (0, 1, -1)}   # a behaviour with no items is not stored


def test_the_status_route_shows_the_result_to_the_owner(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    sid = world.upload()
    sweep(world)
    body = world.api.client.get(f"/organisations/{world.org}/sessions/{sid}", headers=world.headers).json()
    assert (body["status"], body["result"], body["end_state"]) == ("verified", "SUCCESS", "READY_SAFE")


def test_verifying_again_does_not_duplicate_anything(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    sid = world.upload()
    sweep(world)
    with DbSession(world.api.engine) as s:   # put it back to pending, as a re-verification after an engine fix would
        s.get(Session, sid).status = "pending"
        s.commit()
    sweep(world)
    with DbSession(world.api.engine) as s:
        assert s.scalar(select(func.count()).select_from(SessionScoreItem).where(SessionScoreItem.session_id == sid)) == 2


# ---------------------------------------------------------------- rejected and unverifiable

@pytest.mark.parametrize("mode,status,reason", [
    ("hash_mismatch", "rejected", "HASH_MISMATCH"),
    ("not_allowed", "rejected", "CASE_NOT_ALLOWED_FOR_MODE"),
    ("package_loader_missing", "unverifiable", "PACKAGE_LOADER_MISSING"),
])
def test_a_refusal_from_the_engine_is_kept_with_its_reason(world, monkeypatch, mode, status, reason):
    monkeypatch.setenv("FAKE_ENGINE_MODE", mode)
    sid = world.upload()
    sweep(world)
    row = world.row(sid)
    assert (row.status, row.reject_reason) == (status, reason)
    assert row.result is None and row.report_json is None   # nothing counts
    with DbSession(world.api.engine) as s:
        assert s.scalar(select(func.count()).select_from(SessionScoreItem)) == 0


def test_a_content_version_this_service_does_not_hold_is_unverifiable_and_the_engine_is_not_run(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "crash")   # would fail if the engine were run
    sid = world.upload(content_version="ffffffffffff-practice")
    sweep(world)
    row = world.row(sid)
    assert (row.status, row.reject_reason, row.verify_attempts) == ("unverifiable", "CONTENT_VERSION_UNKNOWN", 0)


def test_an_engine_version_the_service_does_not_support_is_unverifiable_without_running_the_engine(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "crash")
    monkeypatch.setenv("SUPPORTED_ENGINE_VERSIONS", "1.0.0")
    config.get_settings.cache_clear()
    sid = world.upload(engine_version="0.1.0")
    sweep(world)
    assert (world.row(sid).status, world.row(sid).reject_reason) == ("unverifiable", "ENGINE_VERSION_UNSUPPORTED")


def test_a_content_version_name_cannot_point_outside_the_content_root(tmp_path):
    runner = EngineRunner(["x"], str(tmp_path))
    for bad in ("../etc", "..", "/etc/passwd", "0123456789ab/../..", "ABCDEF123456", "0123456789ab-practice/x", ""):
        assert runner.content_dir_for(bad) is None, bad


# ---------------------------------------------------------------- no verdict: try again, then give up

@pytest.mark.parametrize("mode", ["crash", "garbage", "huge", "contradiction"])
def test_an_engine_that_gives_no_verdict_leaves_the_session_pending_and_counts_the_attempt(world, monkeypatch, mode):
    monkeypatch.setenv("FAKE_ENGINE_MODE", mode)
    sid = world.upload()
    sweep(world, limit=1)
    row = world.row(sid)
    assert (row.status, row.verify_attempts) == ("pending", 1)


def test_after_three_failed_attempts_the_session_is_unverifiable_with_a_reason(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "crash")
    sid = world.upload()
    for _ in range(3):
        sweep(world, limit=1)
    row = world.row(sid)
    assert (row.status, row.reject_reason, row.verify_attempts) == ("unverifiable", "VERIFIER_ERROR", 3)
    assert sweep(world) == {"processed": 0, "configured": True}   # nothing left to try


def test_an_engine_that_takes_too_long_is_stopped(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "slow")
    monkeypatch.setenv("VERIFY_TIMEOUT_SECONDS", "5")
    config.get_settings.cache_clear()
    worker.get_runner.cache_clear()
    sid = world.upload()
    sweep(world, limit=1)
    assert world.row(sid).status == "pending" and world.row(sid).verify_attempts == 1


def test_the_engine_never_sees_a_secret_in_its_environment(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "environment")
    monkeypatch.setenv("DATABASE_URL", "postgresql://x:yyyyyyyy@localhost:1/z")
    monkeypatch.setenv("SUPABASE_SECRET_KEY", "not-for-the-engine")
    monkeypatch.setenv("INTERNAL_KEY", "nor-this")
    sid = world.upload()
    sweep(world, limit=1)
    assert world.row(sid).status == "verified"
    env = clean_environment({"PATH": "/bin", "DATABASE_URL": "x", "SUPABASE_URL": "y", "MY_TOKEN": "z", "HOME": "/h", "DB_PASSWORD": "p", "INTERNAL_KEY": "k"})
    assert env == {"PATH": "/bin", "HOME": "/h"}


# ---------------------------------------------------------------- the queue

def test_the_oldest_session_is_judged_first_and_the_limit_is_respected(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    ids = [world.upload() for _ in range(3)]
    assert sweep(world, limit=2) == {"processed": 2, "configured": True}
    assert [world.row(i).status for i in ids] == ["verified", "verified", "pending"]


def test_two_workers_never_judge_the_same_session(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    world.upload()
    barrier, claimed = threading.Barrier(2), []
    runner, settings = worker.get_runner(), config.get_settings()

    def work():
        with DbSession(world.api.engine) as db:
            barrier.wait()
            claimed.append(worker.verify_one(db, runner, settings))
    threads = [threading.Thread(target=work) for _ in range(2)]
    [t.start() for t in threads]
    [t.join() for t in threads]
    assert sorted(claimed) == [False, True]


def test_nothing_happens_when_verification_is_not_configured(api, monkeypatch):
    monkeypatch.setenv("ENGINE_CLI", "")
    config.get_settings.cache_clear()
    worker.get_runner.cache_clear()
    assert worker.verify_pending(5) == {"processed": 0, "configured": False}


# ---------------------------------------------------------------- the sweeper route

def test_the_sweeper_route_is_closed_without_a_key_and_needs_the_right_one(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    sid = world.upload()
    client = world.api.client
    assert client.post("/internal/verify-pending", headers={"X-Internal-Key": "anything"}).status_code == 404   # no key configured: it looks like it does not exist
    monkeypatch.setenv("INTERNAL_KEY", "correct-horse-battery")
    config.get_settings.cache_clear()
    assert client.post("/internal/verify-pending").status_code == 403
    assert client.post("/internal/verify-pending", headers={"X-Internal-Key": "wrong"}).status_code == 403
    ok = client.post("/internal/verify-pending", headers={"X-Internal-Key": "correct-horse-battery"})
    assert ok.status_code == 200 and ok.json() == {"processed": 1, "configured": True}
    assert world.row(sid).status == "verified"
    assert "/internal/verify-pending" not in client.get("/openapi.json").json()["paths"]


def test_an_upload_is_verified_in_the_background_when_that_is_switched_on(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    monkeypatch.setenv("VERIFY_ON_UPLOAD", "true")
    config.get_settings.cache_clear()
    sid = world.upload()   # the test client runs background tasks before it returns
    assert world.row(sid).status == "verified"


def test_a_failure_in_the_background_never_reaches_the_person_who_uploaded(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "crash")
    monkeypatch.setenv("VERIFY_ON_UPLOAD", "true")
    config.get_settings.cache_clear()
    sid = world.upload()   # the upload still answered 202 (asserted in upload())
    assert world.row(sid).status == "pending"


# ---------------------------------------------------------------- the real engine

ENGINE = os.environ.get("ENGINE_CLI", "")
needs_engine = pytest.mark.skipif(not ENGINE, reason="set ENGINE_CLI (a JSON list, e.g. [\"dotnet\", \"src/ProcSense.Cli/bin/Release/net9.0/ProcSense.Cli.dll\"]) to run the tests with the real engine")
REPO = Path(__file__).resolve().parents[2]


@pytest.fixture
def real(api, tmp_path, monkeypatch):
    command = json.loads(ENGINE)
    content = tmp_path / "content_copy"
    shutil.copytree(REPO / "content", content, ignore=shutil.ignore_patterns("_extract"))
    version = json.loads((content / "manifest.json").read_text(encoding="utf-8"))["content_hash"][:12]
    root = tmp_path / "root"
    root.mkdir()
    shutil.move(str(content), str(root / version))
    session_path = tmp_path / "run.session.jsonl"
    done = subprocess.run([*command, "run", "--case", "VAR-S1-A", "--seed", "42", "--actions", str(REPO / "tests/golden/skeleton1_correct.actions.json"), "--out", str(tmp_path / "run.json"),
                           "--log", str(session_path), "--content", str(root / version)], capture_output=True, text=True, cwd=REPO)
    assert done.returncode == 0, done.stderr + done.stdout
    monkeypatch.setenv("ENGINE_CLI", ENGINE)
    monkeypatch.setenv("ENGINE_CONTENT_ROOT", str(root))
    monkeypatch.setenv("VERIFY_ON_UPLOAD", "false")
    monkeypatch.setenv("VERIFY_TIMEOUT_SECONDS", "120")
    config.get_settings.cache_clear()
    worker.get_runner.cache_clear()

    def build(s):
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        trainee = api.person(s)
        accounts.add_membership(s, trainee, org.id, "trainee")
        return org.id, trainee
    org, trainee = api.seed(build)

    class R:
        pass
    r = R()
    r.api, r.org, r.headers, r.text, r.version = api, org, {**api.headers(trainee), **NDJSON}, session_path.read_text(encoding="utf-8"), version
    return r


def _post(real, text):
    return real.api.client.post(f"/organisations/{real.org}/sessions", content=text, headers=real.headers)


@needs_engine
def test_a_session_written_by_the_real_engine_is_uploaded_and_verified(real):
    r = _post(real, real.text)
    assert r.status_code == 202, r.text
    sid = uuid.UUID(r.json()["session"]["session_id"])
    assert worker.verify_pending(5)["processed"] == 1
    with DbSession(real.api.engine) as s:
        row = s.get(Session, sid)
        assert row.status == "verified", row.reject_reason
        assert row.content_version == real.version and row.engine_version
        assert row.result and row.report_json["schema"] == "debrief_facts.v1"
        assert s.scalar(select(func.count()).select_from(SessionScoreItem).where(SessionScoreItem.session_id == sid)) == len(row.report_json["items"]) > 0


@needs_engine
def test_the_real_engine_refuses_a_log_edited_to_look_better_even_when_its_hash_is_fixed_up(real):
    head, _, body = real.text.partition("\n")
    edited = body.replace('"score:', '"info:', 1) if '"score:' in body else body.replace('"accepted":true', '"accepted":false', 1)
    assert edited != body
    old = json.loads(head)["header"]["log_hash"]
    text = head.replace(old, hashlib.sha256(edited.encode("utf-8")).hexdigest()) + "\n" + edited
    r = _post(real, text)
    assert r.status_code == 202   # the upload checks the file is whole, not that the result is genuine
    worker.verify_pending(5)
    with DbSession(real.api.engine) as s:
        row = s.get(Session, uuid.UUID(r.json()["session"]["session_id"]))
        assert (row.status, row.reject_reason) == ("rejected", "HASH_MISMATCH")
        assert s.scalar(select(func.count()).select_from(SessionScoreItem)) == 0


@needs_engine
def test_the_real_engine_cannot_verify_a_session_played_with_other_content(real):
    text = real.text.replace(f'"content_version":"{real.version}"', '"content_version":"ffffffffffff"')
    r = _post(real, text)
    worker.verify_pending(5)
    with DbSession(real.api.engine) as s:
        row = s.get(Session, uuid.UUID(r.json()["session"]["session_id"]))
        assert (row.status, row.reject_reason) == ("unverifiable", "CONTENT_VERSION_UNKNOWN")


def test_an_engine_that_the_system_stopped_is_named_by_its_signal_in_the_log():
    import signal
    from server.verification.runner import exit_text
    assert exit_text(0) == "exit 0" and exit_text(2) == "exit 2"
    assert exit_text(-200) == "killed by signal 200"
    expected = {25: "SIGXFSZ ", 9: "SIGKILL "}          # the names exist on Linux, where the service runs; elsewhere the number alone is shown
    for number, name in expected.items():
        text = exit_text(-number)
        assert text == (f"killed by signal {name}({number})" if hasattr(signal, name.strip()) else f"killed by signal {number}"), text   # 25 is the file-size limit: what happened on the host on 14 October 2026


def test_the_engine_is_started_without_secrets_and_with_the_setting_that_lets_it_run_under_the_file_size_limit():
    from server.verification.runner import engine_environment
    env = engine_environment({"PATH": "/bin", "SUPABASE_URL": "y", "MY_TOKEN": "z", "HOME": "/h"})
    assert env == {"PATH": "/bin", "HOME": "/h", "DOTNET_EnableWriteXorExecute": "0"}
    assert engine_environment({"DOTNET_EnableWriteXorExecute": "1"})["DOTNET_EnableWriteXorExecute"] == "0"   # the service decides, not whatever the host had

