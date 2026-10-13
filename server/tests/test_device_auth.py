"""Signing a headset in with a code on a real PostgreSQL (p1-backend-s10): T-DV1 (a code works once and expires, polling too fast is refused, a code approved by one account cannot be claimed as another),
T-DV2 (a revoked headset is refused on its next request, a refresh token used twice revokes that sign-in), and what a headset's token may and may not do."""
from __future__ import annotations

import datetime as dt
import uuid
from dataclasses import dataclass

import pytest
from sqlalchemy import select, update
from sqlalchemy.orm import Session

from server.auth import device_tokens
from server.models.device_session import DeviceLogin, DeviceSession
from server.repositories import accounts
from server.routers import device_auth

pytestmark = pytest.mark.db


@dataclass
class World:
    api: object
    org: uuid.UUID
    admin: uuid.UUID
    trainee: uuid.UUID
    other: uuid.UUID

    def web(self, who) -> dict:
        return self.api.headers(who)


def _clear_limiters():
    for limiter in (device_auth._start_limiter, device_auth._decide_limiter):
        if hasattr(limiter, "cache_clear"):   # a test may have put a plain function there
            limiter.cache_clear()


@pytest.fixture
def world(api) -> World:
    _clear_limiters()

    def build(s: Session) -> World:
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        admin, trainee, other = api.person(s), api.person(s), api.person(s)
        accounts.add_membership(s, admin, org.id, "org_admin")
        accounts.add_membership(s, trainee, org.id, "trainee")
        accounts.add_membership(s, other, org.id, "trainee")
        return World(api, org.id, admin, trainee, other)
    yield api.seed(build)
    _clear_limiters()


def start(w: World, name="Quest 3 / ward") -> dict:
    r = w.api.client.post("/auth/device/start", json={"device_name": name})
    assert r.status_code == 200, r.text
    return r.json()


def approve(w: World, who, user_code, approve=True):
    return w.api.client.post("/auth/device/decision", json={"user_code": user_code, "approve": approve}, headers=w.web(who))


def collect(w: World, device_code):
    return w.api.client.post("/auth/device/token", json={"device_code": device_code})


def make_poll_due(w: World):
    """Moves the last poll back, as if the headset had waited for the interval."""
    w.api.seed(lambda s: s.execute(update(DeviceLogin).values(last_polled_at=dt.datetime.now(dt.timezone.utc) - dt.timedelta(minutes=1))))


def signed_in_headset(w: World, who=None):
    who = who or w.trainee
    s = start(w)
    assert approve(w, who, s["user_code"]).status_code == 200
    r = collect(w, s["device_code"])
    assert r.status_code == 200, r.text
    return r.json()


def bearer(token) -> dict:
    return {"Authorization": "Bearer " + token}


def test_start_gives_a_readable_code_and_an_address(world):
    s = start(world)
    assert len(s["user_code"]) == 9 and s["user_code"][4] == "-"
    assert set(s["user_code"].replace("-", "")) <= set(device_tokens.USER_CODE_ALPHABET)
    assert not set("AEIOUY01OIL") & set(device_tokens.USER_CODE_ALPHABET)   # no vowels (no words), no look-alikes
    assert s["verification_uri"].endswith("/device") and s["verification_uri_complete"].endswith("?code=" + s["user_code"])
    assert s["interval"] == 5 and s["expires_in"] == 600
    assert world.api.client.post("/auth/device/start", json={}).json()["device_code"] != s["device_code"]


def test_the_device_code_is_never_stored_only_its_hash(world):
    s = start(world)
    with Session(world.api.engine) as db:
        row = db.scalars(select(DeviceLogin)).one()
        assert row.device_code_hash == device_tokens.hash_secret(s["device_code"]) and s["device_code"] not in row.device_code_hash


def test_a_pending_code_answers_pending_and_a_poll_that_is_too_fast_is_told_to_slow_down(world):
    s = start(world)
    r = collect(world, s["device_code"])
    assert (r.status_code, r.json()["detail"]) == (400, "authorization_pending")
    r = collect(world, s["device_code"])   # straight away again
    assert r.json()["detail"] == "slow_down"
    make_poll_due(world)
    assert collect(world, s["device_code"]).json()["detail"] == "authorization_pending"


def test_an_approved_code_gives_tokens_once_and_only_once(world):
    s = start(world)
    assert approve(world, world.trainee, s["user_code"]).json() == {"device_name": "Quest 3 / ward", "status": "approved"}
    make_poll_due(world)
    first = collect(world, s["device_code"])
    assert first.status_code == 200 and first.headers["cache-control"] == "no-store"
    assert first.json()["access_token"].startswith("psd_") and first.json()["refresh_token"].startswith("psr_") and first.json()["expires_in"] == 900
    make_poll_due(world)
    again = collect(world, s["device_code"])
    assert (again.status_code, again.json()["detail"]) == (400, "expired_token")   # the code was used up


def test_the_tokens_belong_to_the_person_who_approved_not_to_whoever_asks(world):
    s = start(world)
    approve(world, world.other, s["user_code"])   # `other` approves the code the headset shows
    make_poll_due(world)
    tokens = collect(world, s["device_code"]).json()
    me = world.api.client.get("/me", headers=bearer(tokens["access_token"])).json()
    assert uuid.UUID(me["user_id"]) == world.other


def test_a_denied_code_is_refused(world):
    s = start(world)
    assert approve(world, world.trainee, s["user_code"], approve=False).json()["status"] == "denied"
    make_poll_due(world)
    assert collect(world, s["device_code"]).json()["detail"] == "access_denied"
    assert approve(world, world.trainee, s["user_code"]).status_code == 404   # a decided code cannot be decided again


def test_an_expired_code_cannot_be_approved_or_collected(world):
    s = start(world)
    world.api.seed(lambda db: db.execute(update(DeviceLogin).values(expires_at=dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1))))
    assert approve(world, world.trainee, s["user_code"]).status_code == 404
    assert collect(world, s["device_code"]).json()["detail"] == "expired_token"


def test_an_unknown_device_code_looks_like_an_expired_one(world):
    r = collect(world, "x" * 43)
    assert (r.status_code, r.json()["detail"]) == (400, "expired_token")


def test_a_code_typed_with_a_space_or_in_small_letters_still_works(world):
    s = start(world)
    code = s["user_code"].lower().replace("-", " ")
    assert approve(world, world.trainee, code).status_code == 200


def test_approving_needs_a_web_sign_in(world):
    s = start(world)
    assert world.api.client.post("/auth/device/decision", json={"user_code": s["user_code"]}).status_code == 401
    headset = signed_in_headset(world)
    s2 = start(world)
    r = world.api.client.post("/auth/device/decision", json={"user_code": s2["user_code"]}, headers=bearer(headset["access_token"]))
    assert (r.status_code, r.json()["detail"]) == (403, "web_sign_in_required")   # a stolen headset cannot sign more headsets in


def test_guessing_codes_is_slowed_down(world, monkeypatch):
    monkeypatch.setattr(device_auth, "_decide_limiter", lambda: type("L", (), {"allow": lambda self, key: False})())
    s = start(world)
    assert approve(world, world.trainee, s["user_code"]).status_code == 429


def test_starting_too_many_logins_from_one_address_is_refused(world, monkeypatch):
    from server.ratelimit import RateLimiter
    limiter = RateLimiter(2)
    monkeypatch.setattr(device_auth, "_start_limiter", lambda: limiter)
    assert [world.api.client.post("/auth/device/start", json={}).status_code for _ in range(3)] == [200, 200, 429]


# --- the headset's own tokens ---------------------------------------------------------------------------------------------------------------------------------------------------------

def test_a_headset_token_is_accepted_for_training_and_only_the_hash_is_stored(world):
    t = signed_in_headset(world)
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 200
    with Session(world.api.engine) as db:
        row = db.scalars(select(DeviceSession)).one()
        assert row.access_hash == device_tokens.hash_secret(t["access_token"]) and row.refresh_hash == device_tokens.hash_secret(t["refresh_token"])
        assert t["access_token"] not in (row.access_hash, row.refresh_hash)


def test_a_headset_token_cannot_administer_or_change_the_account(world):
    t = signed_in_headset(world, world.admin)   # even for an organisation administrator
    h = bearer(t["access_token"])
    assert world.api.client.get(f"/organisations/{world.org}/members", headers=h).status_code == 403
    assert world.api.client.get(f"/organisations/{world.org}/members", headers=world.web(world.admin)).status_code == 200
    assert world.api.client.patch("/me", json={"display_name": "x"}, headers=h).status_code == 403
    assert world.api.client.get("/me/devices", headers=h).status_code == 403


def test_a_bad_or_unknown_headset_token_is_a_plain_401(world):
    assert world.api.client.get("/me", headers=bearer("psd_" + "a" * 43)).status_code == 401
    assert world.api.client.get("/me", headers=bearer("psr_" + "a" * 43)).status_code == 401   # a refresh token is not an access token


def test_an_expired_access_token_is_refused_and_the_refresh_token_gets_a_new_one(world):
    t = signed_in_headset(world)
    world.api.seed(lambda db: db.execute(update(DeviceSession).values(access_expires_at=dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1))))
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 401
    r = world.api.client.post("/auth/device/refresh", json={"refresh_token": t["refresh_token"]})
    assert r.status_code == 200
    new = r.json()
    assert new["access_token"] != t["access_token"] and new["refresh_token"] != t["refresh_token"]
    assert world.api.client.get("/me", headers=bearer(new["access_token"])).status_code == 200
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 401   # the old access token stopped working with the swap


def test_a_refresh_token_not_used_for_30_days_is_refused(world):
    t = signed_in_headset(world)
    world.api.seed(lambda db: db.execute(update(DeviceSession).values(refresh_expires_at=dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1))))
    assert world.api.client.post("/auth/device/refresh", json={"refresh_token": t["refresh_token"]}).status_code == 401


def test_a_refresh_token_used_twice_revokes_the_whole_sign_in(world):
    t = signed_in_headset(world)
    new = world.api.client.post("/auth/device/refresh", json={"refresh_token": t["refresh_token"]}).json()
    stolen_use = world.api.client.post("/auth/device/refresh", json={"refresh_token": t["refresh_token"]})   # the old token again: someone copied it
    assert stolen_use.status_code == 401
    assert world.api.client.get("/me", headers=bearer(new["access_token"])).status_code == 401           # the honest headset is signed out too, and must sign in again
    assert world.api.client.post("/auth/device/refresh", json={"refresh_token": new["refresh_token"]}).status_code == 401
    with Session(world.api.engine) as db:
        assert db.scalars(select(DeviceSession)).one().revoked_reason == "refresh_token_reused"


def test_a_garbage_refresh_token_is_refused_and_revokes_nothing(world):
    t = signed_in_headset(world)
    assert world.api.client.post("/auth/device/refresh", json={"refresh_token": "psr_" + "b" * 43}).status_code == 401
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 200


def test_a_person_sees_and_revokes_their_own_headsets_and_the_headset_is_refused_at_once(world):
    t = signed_in_headset(world)
    listed = world.api.client.get("/me/devices", headers=world.web(world.trainee)).json()
    assert [d["device_name"] for d in listed] == ["Quest 3 / ward"]
    assert world.api.client.get("/me/devices", headers=world.web(world.other)).json() == []
    assert world.api.client.delete(f"/me/devices/{listed[0]['id']}", headers=world.web(world.other)).status_code == 404   # not theirs
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 200
    assert world.api.client.delete(f"/me/devices/{listed[0]['id']}", headers=world.web(world.trainee)).status_code == 204
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 401           # T-DV2: refused on the very next request
    assert world.api.client.post("/auth/device/refresh", json={"refresh_token": t["refresh_token"]}).status_code == 401
    assert world.api.client.get("/me/devices", headers=world.web(world.trainee)).json() == []


def test_signing_out_on_the_headset_revokes_it(world):
    t = signed_in_headset(world)
    assert world.api.client.post("/auth/device/logout", headers=bearer(t["access_token"])).status_code == 204
    assert world.api.client.get("/me", headers=bearer(t["access_token"])).status_code == 401
    assert world.api.client.post("/auth/device/logout").status_code == 401


def test_a_deactivated_member_loses_the_organisation_even_on_a_headset(world):
    from server.models.accounts import Membership
    t = signed_in_headset(world)
    h = bearer(t["access_token"])
    assert world.api.client.get(f"/organisations/{world.org}/sessions/{uuid.uuid4()}", headers=h).status_code == 404   # a member: only the session is unknown
    world.api.seed(lambda db: db.execute(update(Membership).where(Membership.user_id == world.trainee).values(status="deactivated")))
    assert world.api.client.get(f"/organisations/{world.org}/sessions/{uuid.uuid4()}", headers=h).status_code == 403


def test_the_two_new_tables_are_closed_to_the_public_roles(migrated):
    from sqlalchemy import text
    with migrated.connect() as conn:
        for table in ("device_login", "device_session"):
            assert conn.execute(text("select relrowsecurity from pg_class where relname = :t"), {"t": table}).scalar_one() is True
            assert conn.execute(text("select count(*) from pg_policies where tablename = :t"), {"t": table}).scalar_one() == 0
