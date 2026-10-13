"""Accounts, organisations, roles, invitations and cohorts on a real PostgreSQL (T-AC1, T-AC2, T-AC3, T-AC4 and more). Two organisations are used in every test."""
from __future__ import annotations

import datetime as dt
import re
import uuid
from dataclasses import dataclass

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session

from server.models.accounts import Invitation, Membership, Profile
from server.repositories import accounts

pytestmark = pytest.mark.db


@dataclass
class World:
    api: object
    org_a: uuid.UUID
    org_b: uuid.UUID
    admin_a: uuid.UUID
    supervisor_a: uuid.UUID
    trainee_a: uuid.UUID
    trainee_a2: uuid.UUID
    supervisor_b: uuid.UUID
    admin_b: uuid.UUID
    outsider: uuid.UUID
    platform: uuid.UUID
    cohort_a: uuid.UUID

    def h(self, who: uuid.UUID, email: str = "") -> dict:
        return self.api.headers(who, email)


@pytest.fixture
def world(api) -> World:
    def build(session: Session) -> World:
        a = accounts.create_organisation(session, "Alpha Hospital", "alpha-hospital")
        b = accounts.create_organisation(session, "Beta Clinic", "beta-clinic")
        people = {name: api.person(session, platform_admin=(name == "platform")) for name in
                  ("admin_a", "supervisor_a", "trainee_a", "trainee_a2", "supervisor_b", "admin_b", "outsider", "platform")}
        for name, org, role in (("admin_a", a, "org_admin"), ("supervisor_a", a, "supervisor"), ("trainee_a", a, "trainee"), ("trainee_a2", a, "trainee"),
                                ("supervisor_b", b, "supervisor"), ("admin_b", b, "org_admin")):
            accounts.add_membership(session, people[name], org.id, role)
        cohort = accounts.create_cohort(session, a.id, "Spring intake")
        accounts.set_cohort_member(session, cohort.id, people["trainee_a"], "trainee")
        accounts.set_cohort_member(session, cohort.id, people["supervisor_a"], "supervisor")
        return World(api, a.id, b.id, **{k: v for k, v in people.items()}, cohort_a=cohort.id)
    return api.seed(build)


# ---------------------------------------------------------------- T-AC1: who may do what

# (method, path template, body, {who: expected status}); "anon" means no token
ROLE_MATRIX = [
    ("GET", "/me", None, {"anon": 401, "outsider": 200, "trainee_a": 200, "supervisor_a": 200, "admin_a": 200, "supervisor_b": 200, "platform": 200}),
    ("GET", "/organisations/{a}", None, {"anon": 401, "outsider": 404, "trainee_a": 200, "supervisor_a": 200, "admin_a": 200, "supervisor_b": 404, "admin_b": 404, "platform": 200}),
    ("GET", "/organisations/{a}/members", None, {"anon": 401, "outsider": 404, "trainee_a": 403, "supervisor_a": 200, "admin_a": 200, "supervisor_b": 404, "admin_b": 404, "platform": 200}),
    ("GET", "/organisations/{a}/cohorts", None, {"anon": 401, "outsider": 404, "trainee_a": 403, "supervisor_a": 200, "admin_a": 200, "supervisor_b": 404, "platform": 200}),
    ("POST", "/organisations/{a}/cohorts", {"name": "Autumn"}, {"anon": 401, "outsider": 404, "trainee_a": 403, "supervisor_a": 403, "admin_a": 201, "supervisor_b": 404, "admin_b": 404, "platform": 201}),
    ("GET", "/organisations/{a}/invitations", None, {"anon": 401, "outsider": 404, "trainee_a": 403, "supervisor_a": 403, "admin_a": 200, "admin_b": 404, "platform": 200}),
    ("POST", "/organisations/{a}/invitations", {"email": "new@example.com", "role": "trainee"}, {"anon": 401, "trainee_a": 403, "supervisor_a": 403, "admin_a": 201, "admin_b": 404, "platform": 201}),
    ("PATCH", "/organisations/{a}/members/{trainee_a2}", {"role": "supervisor"}, {"anon": 401, "trainee_a": 403, "supervisor_a": 403, "admin_a": 200, "admin_b": 404, "platform": 200}),
    ("POST", "/organisations", {"name": "Gamma", "slug": "gamma-org", "admin_email": "boss@example.com"}, {"anon": 401, "trainee_a": 403, "admin_a": 403, "platform": 201}),
]


@pytest.mark.parametrize("method,template,body,expected", ROLE_MATRIX, ids=[f"{m} {t}" for m, t, _, _ in ROLE_MATRIX])
def test_T_AC1_every_endpoint_gives_the_right_answer_to_every_kind_of_caller(api, method, template, body, expected):
    for who, status in expected.items():
        # a fresh world per caller keeps one caller's change (a role, a new cohort) from affecting the next
        w = _fresh_world(api)
        ids = {"a": w.org_a, "b": w.org_b, "trainee_a2": w.trainee_a2}
        path = template.format(**ids)
        headers = {} if who == "anon" else w.h(getattr(w, who))
        r = api.client.request(method, path, json=body, headers=headers)
        assert r.status_code == status, f"{who} {method} {path}: expected {status}, got {r.status_code} {r.text}"


_counter = {"n": 0}


def _fresh_world(api) -> World:
    """Another set of organisations and people (names made unique) in the same database."""
    _counter["n"] += 1
    n = _counter["n"]

    def build(session: Session) -> World:
        a = accounts.create_organisation(session, f"Alpha {n}", f"alpha-{n:03d}")
        b = accounts.create_organisation(session, f"Beta {n}", f"beta-{n:03d}")
        people = {name: api.person(session, platform_admin=(name == "platform")) for name in
                  ("admin_a", "supervisor_a", "trainee_a", "trainee_a2", "supervisor_b", "admin_b", "outsider", "platform")}
        for name, org, role in (("admin_a", a, "org_admin"), ("supervisor_a", a, "supervisor"), ("trainee_a", a, "trainee"), ("trainee_a2", a, "trainee"),
                                ("supervisor_b", b, "supervisor"), ("admin_b", b, "org_admin")):
            accounts.add_membership(session, people[name], org.id, role)
        cohort = accounts.create_cohort(session, a.id, "Spring intake")
        return World(api, a.id, b.id, **people, cohort_a=cohort.id)
    return api.seed(build)


# ---------------------------------------------------------------- T-AC2: tokens at the door

def test_T_AC2_a_bad_token_gets_a_plain_401_that_says_nothing_about_why(api, world):
    uid = world.admin_a
    bad = {
        "expired": api.headers(uid, expires_in=-120),
        "wrong audience": api.headers(uid, audience="anon"),
        "wrong issuer": api.headers(uid, issuer="https://evil.example/auth/v1"),
        "not a token": {"Authorization": "Bearer abc.def.ghi"},
        "wrong scheme": {"Authorization": "Basic dXNlcjpwYXNz"},
        "empty": {"Authorization": "Bearer "},
    }
    for name, headers in bad.items():
        r = api.client.get("/me", headers=headers)
        assert r.status_code == 401, name
        assert r.json() == {"detail": "not_signed_in"}, name
        assert r.headers.get("www-authenticate") == "Bearer"


# ---------------------------------------------------------------- first sign-in and pseudonyms

def test_the_first_sign_in_makes_a_profile_with_a_pseudonym_and_the_second_keeps_it(api):
    uid = uuid.uuid4()
    first = api.client.get("/me", headers=api.headers(uid, "new.person@example.com")).json()
    second = api.client.get("/me", headers=api.headers(uid, "new.person@example.com")).json()
    assert re.fullmatch(r"T-[2-9a-hjkmnp-z]{8}", first["pseudonym"])
    assert first["pseudonym"] == second["pseudonym"] and first["memberships"] == []
    assert first["is_platform_admin"] is False


def test_no_email_address_is_kept_in_the_profile(api):
    uid = uuid.uuid4()
    api.client.get("/me", headers=api.headers(uid, "private.person@example.com"))
    with Session(api.engine) as s:
        assert "email" not in Profile.__table__.c
        row = s.get(Profile, uid)
        assert "private.person" not in repr(row.__dict__)


def test_a_person_can_set_their_display_name_and_supervisors_of_the_same_organisation_see_it(api, world):
    r = api.client.patch("/me", json={"display_name": "  Dr Khan  "}, headers=world.h(world.trainee_a))
    assert r.status_code == 200 and r.json()["display_name"] == "Dr Khan"
    members = api.client.get(f"/organisations/{world.org_a}/members", headers=world.h(world.supervisor_a)).json()
    assert {m["display_name"] for m in members} >= {"Dr Khan"}


# ---------------------------------------------------------------- T-AC3: removal takes effect at once

def test_T_AC3_a_deactivated_member_loses_access_on_the_very_next_request(api, world):
    assert api.client.get(f"/organisations/{world.org_a}", headers=world.h(world.trainee_a)).status_code == 200
    r = api.client.patch(f"/organisations/{world.org_a}/members/{world.trainee_a}", json={"status": "deactivated"}, headers=world.h(world.admin_a))
    assert r.status_code == 200 and r.json()["status"] == "deactivated"
    # the same token, a moment later
    r = api.client.get(f"/organisations/{world.org_a}", headers=world.h(world.trainee_a))
    assert r.status_code == 403 and r.json()["detail"] == "membership_deactivated"
    api.client.patch(f"/organisations/{world.org_a}/members/{world.trainee_a}", json={"status": "active"}, headers=world.h(world.admin_a))
    assert api.client.get(f"/organisations/{world.org_a}", headers=world.h(world.trainee_a)).status_code == 200


def test_the_role_comes_from_the_database_not_from_the_token(api, world):
    # a token that claims a role of its own changes nothing
    headers = api.headers(world.trainee_a, extra={"role": "org_admin", "app_metadata": {"role": "org_admin"}})
    assert api.client.get(f"/organisations/{world.org_a}/invitations", headers=headers).status_code == 403


def test_the_last_active_administrator_cannot_be_demoted_or_deactivated(api, world):
    for body in ({"role": "trainee"}, {"status": "deactivated"}):
        r = api.client.patch(f"/organisations/{world.org_a}/members/{world.admin_a}", json=body, headers=world.h(world.admin_a))
        assert r.status_code == 409 and r.json()["detail"] == "last_administrator"
    # with a second administrator the first may step down
    api.client.patch(f"/organisations/{world.org_a}/members/{world.supervisor_a}", json={"role": "org_admin"}, headers=world.h(world.admin_a))
    r = api.client.patch(f"/organisations/{world.org_a}/members/{world.admin_a}", json={"role": "trainee"}, headers=world.h(world.supervisor_a))
    assert r.status_code == 200


def test_a_member_of_another_organisation_cannot_be_changed_through_this_one(api, world):
    r = api.client.patch(f"/organisations/{world.org_a}/members/{world.admin_b}", json={"status": "deactivated"}, headers=world.h(world.admin_a))
    assert r.status_code == 404


# ---------------------------------------------------------------- T-AC4: invitations

def test_T_AC4_an_invitation_lands_the_person_in_the_right_organisation_and_role(api, world):
    r = api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "New.Trainee@Example.com", "role": "supervisor", "cohort_id": str(world.cohort_a)}, headers=world.h(world.admin_a))
    assert r.status_code == 201 and r.json()["mail"] == "sent"
    assert api.admin.sent == ["new.trainee@example.com"]
    newcomer = uuid.uuid4()
    me = api.client.get("/me", headers=api.headers(newcomer, "new.trainee@example.com")).json()
    assert [(m["slug"], m["role"], m["status"]) for m in me["memberships"]] == [("alpha-hospital", "supervisor", "active")]
    cohorts = api.client.get(f"/organisations/{world.org_a}/cohorts", headers=world.h(world.admin_a)).json()
    assert str(newcomer) in {m["user_id"] for c in cohorts for m in c["members"]}
    with Session(api.engine) as s:
        inv = s.scalars(select(Invitation).where(Invitation.organisation_id == world.org_a)).one()
        assert inv.status == "accepted" and inv.email is None   # the address is cleared once used


def test_an_invitation_is_used_once_and_only_by_the_address_it_was_sent_to(api, world):
    api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "right@example.com", "role": "trainee"}, headers=world.h(world.admin_a))
    stranger = api.client.get("/me", headers=api.headers(uuid.uuid4(), "wrong@example.com")).json()
    assert stranger["memberships"] == []
    first = api.client.get("/me", headers=api.headers(uuid.uuid4(), "right@example.com")).json()
    second = api.client.get("/me", headers=api.headers(uuid.uuid4(), "right@example.com")).json()
    assert len(first["memberships"]) == 1 and second["memberships"] == []


def test_an_expired_or_revoked_invitation_is_ignored(api, world):
    api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "late@example.com"}, headers=world.h(world.admin_a))
    api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "revoked@example.com"}, headers=world.h(world.admin_a))
    pending = api.client.get(f"/organisations/{world.org_a}/invitations", headers=world.h(world.admin_a)).json()
    revoked_id = next(i["id"] for i in pending if i["email"] == "revoked@example.com")
    assert api.client.delete(f"/organisations/{world.org_a}/invitations/{revoked_id}", headers=world.h(world.admin_a)).status_code == 204

    def age(session: Session):
        for inv in session.scalars(select(Invitation).where(Invitation.email == "late@example.com")):
            inv.expires_at = dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=1)
    api.seed(age)
    assert api.client.get("/me", headers=api.headers(uuid.uuid4(), "late@example.com")).json()["memberships"] == []
    assert api.client.get("/me", headers=api.headers(uuid.uuid4(), "revoked@example.com")).json()["memberships"] == []


def test_a_failed_email_does_not_fail_the_invitation(api, world):
    api.admin.result = "error"
    r = api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "offline@example.com"}, headers=world.h(world.admin_a))
    assert r.status_code == 201 and r.json()["mail"] == "not_sent"
    api.admin.result = "already_registered"
    r = api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "known@example.com"}, headers=world.h(world.admin_a))
    assert r.json()["mail"] == "already_registered"


def test_an_invitation_into_a_cohort_of_another_organisation_is_refused(api, world):
    other = api.client.post(f"/organisations/{world.org_b}/cohorts", json={"name": "Beta group"}, headers=world.h(world.admin_b)).json()["id"]
    r = api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "x@example.com", "cohort_id": other}, headers=world.h(world.admin_a))
    assert r.status_code == 404


def test_a_bad_email_is_refused(api, world):
    r = api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "not-an-address"}, headers=world.h(world.admin_a))
    assert r.status_code == 422


# ---------------------------------------------------------------- organisations

def test_a_platform_administrator_creates_an_organisation_and_invites_its_first_administrator(api, world):
    r = api.client.post("/organisations", json={"name": "Gamma Hospital", "slug": "gamma-hospital", "admin_email": "Boss@Example.com"}, headers=world.h(world.platform))
    assert r.status_code == 201
    assert api.admin.sent == ["boss@example.com"]
    boss = uuid.uuid4()
    me = api.client.get("/me", headers=api.headers(boss, "boss@example.com")).json()
    assert [(m["slug"], m["role"]) for m in me["memberships"]] == [("gamma-hospital", "org_admin")]
    dup = api.client.post("/organisations", json={"name": "Again", "slug": "gamma-hospital", "admin_email": "x@example.com"}, headers=world.h(world.platform))
    assert dup.status_code == 409


def test_a_bad_organisation_name_or_slug_is_refused(api, world):
    for body in ({"name": "X", "slug": "ok-slug", "admin_email": "a@b.co"}, {"name": "Fine", "slug": "Bad Slug", "admin_email": "a@b.co"}, {"name": "Fine", "slug": "a", "admin_email": "a@b.co"}):
        assert api.client.post("/organisations", json=body, headers=world.h(world.platform)).status_code == 422


# ---------------------------------------------------------------- isolation between organisations

def test_members_and_cohorts_of_one_organisation_never_appear_in_another(api, world):
    a_members = {m["user_id"] for m in api.client.get(f"/organisations/{world.org_a}/members", headers=world.h(world.admin_a)).json()}
    b_members = {m["user_id"] for m in api.client.get(f"/organisations/{world.org_b}/members", headers=world.h(world.admin_b)).json()}
    assert a_members.isdisjoint(b_members) and str(world.admin_b) not in a_members
    api.client.post(f"/organisations/{world.org_b}/cohorts", json={"name": "Beta only"}, headers=world.h(world.admin_b))
    a_cohorts = {c["name"] for c in api.client.get(f"/organisations/{world.org_a}/cohorts", headers=world.h(world.admin_a)).json()}
    assert "Beta only" not in a_cohorts


def test_a_person_in_two_organisations_names_the_one_they_mean_in_the_address(api, world):
    api.seed(lambda s: accounts.add_membership(s, world.supervisor_a, world.org_b, "trainee"))
    assert api.client.get(f"/organisations/{world.org_a}/members", headers=world.h(world.supervisor_a)).status_code == 200
    assert api.client.get(f"/organisations/{world.org_b}/members", headers=world.h(world.supervisor_a)).status_code == 403   # a trainee there
    me = api.client.get("/me", headers=world.h(world.supervisor_a)).json()
    assert {m["slug"]: m["role"] for m in me["memberships"]} == {"alpha-hospital": "supervisor", "beta-clinic": "trainee"}


# ---------------------------------------------------------------- cohorts

def test_cohort_membership_rules(api, world):
    base = f"/organisations/{world.org_a}/cohorts/{world.cohort_a}/members"
    ok = api.client.put(f"{base}/{world.trainee_a2}", json={"role": "trainee"}, headers=world.h(world.admin_a))
    assert ok.status_code == 200 and str(world.trainee_a2) in {m["user_id"] for m in ok.json()["members"]}
    # a trainee cannot be made a cohort supervisor
    assert api.client.put(f"{base}/{world.trainee_a2}", json={"role": "supervisor"}, headers=world.h(world.admin_a)).status_code == 409
    # someone from another organisation cannot be added
    assert api.client.put(f"{base}/{world.admin_b}", json={"role": "trainee"}, headers=world.h(world.admin_a)).status_code == 404
    assert api.client.delete(f"{base}/{world.trainee_a2}", headers=world.h(world.admin_a)).status_code == 204
    assert api.client.delete(f"{base}/{world.trainee_a2}", headers=world.h(world.admin_a)).status_code == 404


def test_a_cohort_name_is_unique_inside_an_organisation_but_not_across_them(api, world):
    assert api.client.post(f"/organisations/{world.org_a}/cohorts", json={"name": "Spring intake"}, headers=world.h(world.admin_a)).status_code == 409
    assert api.client.post(f"/organisations/{world.org_b}/cohorts", json={"name": "Spring intake"}, headers=world.h(world.admin_b)).status_code == 201


def test_the_database_refuses_a_role_that_is_not_in_the_list(api, world):
    from sqlalchemy.exc import IntegrityError
    with pytest.raises(IntegrityError):
        api.seed(lambda s: accounts.add_membership(s, world.outsider, world.org_a, "superuser"))
