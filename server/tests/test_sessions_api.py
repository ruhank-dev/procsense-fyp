"""Uploading sessions on a real PostgreSQL: T-BE1 (upload twice, stored once), T-BE2 (a partial upload stores nothing and the retry works), T-BE3 (pseudonyms only), T-BE4 (the right person and organisation),
T-BE6 (generated scenario packages), limits, and who may see a session's status."""
from __future__ import annotations

import gzip
import hashlib
import json
import threading
import uuid
from dataclasses import dataclass

import pytest
from sqlalchemy import func, select
from sqlalchemy.orm import Session as DbSession

from server.models.sessions import ScenarioPackage, Session
from server.repositories import accounts
from server.repositories import sessions as repository
from server.sessionfile import parse_session_file
from server.tests.files import changed_log, make_file

pytestmark = pytest.mark.db
NDJSON = {"Content-Type": "application/x-ndjson"}


@dataclass
class Setup:
    api: object
    org_a: uuid.UUID
    org_b: uuid.UUID
    trainee: uuid.UUID
    other_trainee: uuid.UUID
    supervisor: uuid.UUID
    outsider_b: uuid.UUID

    def h(self, who) -> dict:
        return {**self.api.headers(who), **NDJSON}

    def url(self, org=None) -> str:
        return f"/organisations/{org or self.org_a}/sessions"


@pytest.fixture
def setup(api) -> Setup:
    def build(s: DbSession) -> Setup:
        a, b = accounts.create_organisation(s, "Alpha", "alpha-org"), accounts.create_organisation(s, "Beta", "beta-org")
        people = {n: api.person(s) for n in ("trainee", "other_trainee", "supervisor", "outsider_b")}
        accounts.add_membership(s, people["trainee"], a.id, "trainee")
        accounts.add_membership(s, people["other_trainee"], a.id, "trainee")
        accounts.add_membership(s, people["supervisor"], a.id, "supervisor")
        accounts.add_membership(s, people["outsider_b"], b.id, "trainee")
        return Setup(api, a.id, b.id, **people)
    return api.seed(build)


def count_sessions(api) -> int:
    with DbSession(api.engine) as s:
        return s.scalar(select(func.count()).select_from(Session))


# ---------------------------------------------------------------- T-BE1, T-BE2

def test_T_BE1_the_same_upload_twice_is_stored_once(api, setup):
    text = make_file()
    first = api.client.post(setup.url(), content=text, headers=setup.h(setup.trainee))
    second = api.client.post(setup.url(), content=text, headers=setup.h(setup.trainee))
    assert first.status_code == 202 and first.json()["outcome"] == "accepted"
    assert second.status_code == 200 and second.json()["outcome"] == "already_stored"
    assert first.json()["session"]["session_id"] == second.json()["session"]["session_id"]
    assert count_sessions(api) == 1
    assert first.json()["session"]["status"] == "pending" and first.json()["session"]["log_entries"] == 78


def test_T_BE2_a_partial_upload_stores_nothing_and_the_retry_succeeds(api, setup):
    sid = uuid.uuid4()
    whole = make_file(session_id=sid)
    cut = whole.rstrip("\n").rsplit("\n", 1)[0] + "\n"   # the connection dropped before the last action arrived
    broken = api.client.post(setup.url(), content=cut, headers=setup.h(setup.trainee))
    assert broken.status_code == 422 and broken.json()["detail"]["code"] == "LOG_HASH_MISMATCH"
    assert count_sessions(api) == 0
    retry = api.client.post(setup.url(), content=whole, headers=setup.h(setup.trainee))
    assert retry.status_code == 202 and count_sessions(api) == 1


def test_two_identical_uploads_at_the_same_moment_store_one_row(api, setup):
    parsed = parse_session_file(make_file())
    barrier, results = threading.Barrier(2), []

    def upload():
        with DbSession(api.engine) as s:
            barrier.wait()
            results.append(repository.insert_session(s, parsed, setup.org_a, setup.trainee))
            s.commit()
    threads = [threading.Thread(target=upload) for _ in range(2)]
    [t.start() for t in threads]
    [t.join() for t in threads]
    assert sorted(results) == [False, True] and count_sessions(api) == 1


def test_the_log_is_stored_compressed_and_comes_back_whole(api, setup):
    text = make_file()
    api.client.post(setup.url(), content=text, headers=setup.h(setup.trainee))
    with DbSession(api.engine) as s:
        row = s.scalars(select(Session)).one()
        assert gzip.decompress(row.log_gz).decode("utf-8") == text.split("\n", 1)[1]
        assert len(row.log_gz) < len(text) / 3


def test_the_session_row_holds_what_the_header_says_and_the_person_comes_from_the_token(api, setup):
    sid = uuid.uuid4()
    api.client.post(setup.url(), content=make_file(session_id=sid, content_version="abc123abc123-practice", engine_version="0.9.1"), headers=setup.h(setup.trainee))
    with DbSession(api.engine) as s:
        row = s.get(Session, sid)
        assert (row.user_id, row.organisation_id) == (setup.trainee, setup.org_a)
        assert (row.case_id, row.mode, row.seed, row.content_version, row.engine_version, row.scenario_package_hash) == ("VAR-S1-A", "practice", "42", "abc123abc123-practice", "0.9.1", "")
        assert row.status == "pending" and row.ended_step == 7818 and row.received_at is not None


# ---------------------------------------------------------------- T-BE3: pseudonyms only

def test_T_BE3_the_session_tables_hold_no_name_or_email_and_the_reply_names_nobody(api, setup):
    r = api.client.post(setup.url(), content=make_file(), headers=setup.h(setup.trainee))
    assert set(r.json()["session"]) == {"session_id", "status", "reject_reason", "case_id", "mode", "log_entries", "received_at", "end_state", "result", "process_score"}
    for column in Session.__table__.c.keys():
        assert "email" not in column and "name" not in column.replace("case_id", "")
    assert "user_id" not in r.json()["session"]


# ---------------------------------------------------------------- T-BE4: the right person and organisation

def test_T_BE4_nobody_uploads_into_an_organisation_they_do_not_belong_to(api, setup):
    assert api.client.post(setup.url(setup.org_b), content=make_file(), headers=setup.h(setup.trainee)).status_code == 404
    assert api.client.post(setup.url(), content=make_file(), headers={**NDJSON}).status_code == 401
    assert count_sessions(api) == 0


def test_T_BE4_a_deactivated_member_cannot_upload(api, setup):
    api.seed(lambda s: accounts.set_member(s, setup.org_a, setup.trainee, None, "deactivated"))
    r = api.client.post(setup.url(), content=make_file(), headers=setup.h(setup.trainee))
    assert r.status_code == 403 and count_sessions(api) == 0


def test_an_id_that_is_already_used_with_other_content_or_by_another_person_is_refused_and_logged(api, setup, caplog):
    sid = uuid.uuid4()
    assert api.client.post(setup.url(), content=make_file(session_id=sid), headers=setup.h(setup.trainee)).status_code == 202
    other_log = changed_log(lambda es: es[-1].update(step=es[-1]["step"] + 1))
    with caplog.at_level("WARNING", logger="procsense.security"):
        different = api.client.post(setup.url(), content=make_file(session_id=sid, lines=other_log), headers=setup.h(setup.trainee))
        another_person = api.client.post(setup.url(), content=make_file(session_id=sid), headers=setup.h(setup.other_trainee))
    assert different.status_code == 409 and another_person.status_code == 409
    assert different.json()["detail"] == "session_id_conflict"
    assert sum("session id reused" in m for m in caplog.messages) == 2
    assert count_sessions(api) == 1


# ---------------------------------------------------------------- limits and encodings

def test_an_oversized_upload_is_refused_with_413(api, setup, monkeypatch):
    from server import config
    monkeypatch.setenv("MAX_UPLOAD_BYTES", "4096")
    config.get_settings.cache_clear()
    assert api.client.post(setup.url(), content=make_file(), headers=setup.h(setup.trainee)).status_code == 413
    assert count_sessions(api) == 0


def test_a_gzip_upload_is_read_and_a_gzip_bomb_is_refused(api, setup, monkeypatch):
    text = make_file()
    ok = api.client.post(setup.url(), content=gzip.compress(text.encode()), headers={**setup.h(setup.trainee), "Content-Encoding": "gzip"})
    assert ok.status_code == 202
    from server import config
    monkeypatch.setenv("MAX_UPLOAD_BYTES", "4096")
    config.get_settings.cache_clear()
    bomb = gzip.compress(b"x" * 5_000_000)   # a few KB on the wire, five MB when unpacked
    assert len(bomb) < 8192
    refused = api.client.post(setup.url(), content=bomb, headers={**setup.h(setup.trainee), "Content-Encoding": "gzip"})
    assert refused.status_code == 413


def test_bad_gzip_other_encodings_and_text_that_is_not_utf8_are_refused(api, setup):
    assert api.client.post(setup.url(), content=b"not gzip at all", headers={**setup.h(setup.trainee), "Content-Encoding": "gzip"}).status_code == 422
    assert api.client.post(setup.url(), content=b"abc", headers={**setup.h(setup.trainee), "Content-Encoding": "br"}).status_code == 415
    assert api.client.post(setup.url(), content=b"\xff\xfe\xfa binary", headers=setup.h(setup.trainee)).status_code == 422


def test_a_malformed_line_is_refused_with_its_line_number(api, setup):
    lines = changed_log(lambda es: es[3].update(extra="x"))
    r = api.client.post(setup.url(), content=make_file(lines=lines), headers=setup.h(setup.trainee))
    assert r.status_code == 422 and r.json()["detail"]["code"] == "LINE_INVALID" and r.json()["detail"]["line"] == 5
    assert count_sessions(api) == 0


def test_a_person_who_uploads_too_often_is_slowed_down(api, setup, monkeypatch):
    from server import config
    from server.routers import sessions as router
    monkeypatch.setenv("UPLOAD_RATE_PER_HOUR", "2")
    config.get_settings.cache_clear()
    router._limiter.cache_clear()
    codes = [api.client.post(setup.url(), content=make_file(), headers=setup.h(setup.trainee)).status_code for _ in range(3)]
    assert codes == [202, 202, 429]
    assert api.client.post(setup.url(), content=make_file(), headers=setup.h(setup.other_trainee)).status_code == 202   # someone else is not affected


# ---------------------------------------------------------------- T-BE6: generated scenarios

def _package_bytes(name="generated-1") -> tuple[bytes, str]:
    raw = json.dumps({"schema": "scenario_package.v1", "id": name, "facts": ["a", "b"]}, separators=(",", ":")).encode()
    return raw, hashlib.sha256(raw).hexdigest()


def test_T_BE6_a_generated_scenario_uploads_in_two_steps_and_the_package_is_stored_once(api, setup):
    raw, digest = _package_bytes()
    first = api.client.post(setup.url(), content=make_file(package=digest), headers=setup.h(setup.trainee))
    assert first.status_code == 409 and first.json()["detail"] == {"code": "package_needed", "package_hash": digest}
    assert count_sessions(api) == 0
    put = api.client.put(f"/scenario-packages/{digest}", content=raw, headers=setup.h(setup.trainee))
    assert put.status_code == 201 and put.json()["outcome"] == "stored"
    again = api.client.put(f"/scenario-packages/{digest}", content=raw, headers=setup.h(setup.other_trainee))
    assert again.status_code == 200 and again.json()["outcome"] == "already_stored"
    retry = api.client.post(setup.url(), content=make_file(package=digest), headers=setup.h(setup.trainee))
    assert retry.status_code == 202
    with DbSession(api.engine) as s:
        assert s.scalar(select(func.count()).select_from(ScenarioPackage)) == 1
        assert s.scalars(select(Session)).one().scenario_package_hash == digest


def test_a_package_must_match_the_hash_it_is_stored_under(api, setup):
    raw, digest = _package_bytes()
    other_raw, other_digest = _package_bytes("something-else")
    assert api.client.put(f"/scenario-packages/{digest}", content=other_raw, headers=setup.h(setup.trainee)).status_code == 422
    assert api.client.put("/scenario-packages/not-a-hash", content=raw, headers=setup.h(setup.trainee)).status_code == 422
    not_json = b"plain text, not a package"
    assert api.client.put(f"/scenario-packages/{hashlib.sha256(not_json).hexdigest()}", content=not_json, headers=setup.h(setup.trainee)).status_code == 422
    with DbSession(api.engine) as s:
        assert s.scalar(select(func.count()).select_from(ScenarioPackage)) == 0


def test_only_people_with_an_active_membership_can_store_a_package_and_an_oversized_one_is_refused(api, setup, monkeypatch):
    raw, digest = _package_bytes()
    stranger = api.client.put(f"/scenario-packages/{digest}", content=raw, headers=api.headers(uuid.uuid4()))
    assert stranger.status_code == 403
    assert api.client.put(f"/scenario-packages/{digest}", content=raw, headers={}).status_code == 401
    from server import config
    monkeypatch.setenv("MAX_PACKAGE_BYTES", "1024")
    config.get_settings.cache_clear()
    big = json.dumps({"filler": "x" * 5000}).encode()
    assert api.client.put(f"/scenario-packages/{hashlib.sha256(big).hexdigest()}", content=big, headers=setup.h(setup.trainee)).status_code == 413


def test_a_generated_scenario_is_practice_only(api, setup):
    raw, digest = _package_bytes()
    api.client.put(f"/scenario-packages/{digest}", content=raw, headers=setup.h(setup.trainee))
    r = api.client.post(setup.url(), content=make_file(mode="assessment", package=digest), headers=setup.h(setup.trainee))
    assert r.status_code == 422 and r.json()["detail"]["code"] == "ASSESSMENT_CANNOT_USE_GENERATED_SCENARIO"
    assert count_sessions(api) == 0


# ---------------------------------------------------------------- who may ask about a session

def test_the_status_of_a_session_is_seen_by_its_owner_and_the_supervisors_of_its_organisation_only(api, setup):
    sid = uuid.uuid4()
    api.client.post(setup.url(), content=make_file(session_id=sid), headers=setup.h(setup.trainee))
    url = f"{setup.url()}/{sid}"
    assert api.client.get(url, headers=setup.h(setup.trainee)).status_code == 200
    assert api.client.get(url, headers=setup.h(setup.supervisor)).status_code == 200
    assert api.client.get(url, headers=setup.h(setup.other_trainee)).status_code == 404   # a colleague, not a supervisor
    assert api.client.get(f"{setup.url(setup.org_b)}/{sid}", headers=setup.h(setup.outsider_b)).status_code == 404   # another organisation
    assert api.client.get(url, headers={}).status_code == 401
    assert api.client.get(f"{setup.url()}/{uuid.uuid4()}", headers=setup.h(setup.trainee)).status_code == 404
