"""Consent, the access log, export, deletion, retention and log hygiene on a real PostgreSQL (p1-backend-s12): T-PR1 (a delete leaves no email, name or link to the person in the live tables and organisation totals do not change),
T-PR2 (export returns only that person's data), T-PR3 (a supervisor viewing an individual appears in that person's access log), T-PR4 (a person who did not agree to research is absent from a research export), T-PR5 (no email in the logs)."""
from __future__ import annotations

import datetime as dt
import json
import logging
import uuid

import pytest
from sqlalchemy import func, select, text, update
from sqlalchemy.orm import Session as DbSession

from server import config, privacy
from server.logging_setup import clean
from server.models.accounts import ConsentRecord, Membership, Profile
from server.models.privacy import AccessLog
from server.models.sessions import Session
from server.repositories import accounts
from server.repositories import privacy as repository
from server.tests.files import make_file
from server.tests.test_dashboard import add_session, rows_of, world  # noqa: F401  (the same two organisations and sample sessions)

pytestmark = pytest.mark.db
# made-up values shaped like a signed token, built from pieces so the repository's secret scan does not mistake them for a real one
FAKE_TOKEN = ".".join(["eyJ" + "hbGciOiJFUzI1NiJ9", "eyJ" + "zdWIiOiIxMjM0NTY3ODkwIn0", "c2lnbmF0dXJl"])
BAD_TOKEN = ".".join(["eyJ" + "hbGciOiJub25lIn0", "eyJ" + "zdWIiOiJ4In0", ""])
NDJSON = {"Content-Type": "application/x-ndjson"}


def bearer(token) -> dict:
    return {"Authorization": "Bearer " + token}


def headset_token(world, who) -> str:
    from server.repositories import devices
    return world.api.seed(lambda s: devices.issue(s, who, "Headset", 15, 30).access_token)


# --- consent ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

def test_a_person_who_has_not_answered_must_answer_and_the_answer_is_kept(api):
    who = api.seed(lambda s: api.person(s, consent=False))
    state = api.client.get("/privacy/consent", headers=api.headers(who)).json()
    assert state["needs_consent"] is True and state["version"] == privacy.CONSENT_VERSION
    assert [(k["kind"], k["required"], k["granted"]) for k in state["kinds"]] == [("results", True, None), ("research", False, None)]
    assert all(k["text"] and k["title"] for k in state["kinds"])
    after = api.client.post("/privacy/consent", json={"kind": "results", "granted": True}, headers=api.headers(who)).json()
    assert after["needs_consent"] is False
    api.client.post("/privacy/consent", json={"kind": "research", "granted": True}, headers=api.headers(who))
    api.client.post("/privacy/consent", json={"kind": "research", "granted": False}, headers=api.headers(who))
    state = api.client.get("/privacy/consent", headers=api.headers(who)).json()
    assert {k["kind"]: k["granted"] for k in state["kinds"]} == {"results": True, "research": False}   # the latest answer counts
    with DbSession(api.engine) as s:
        assert s.scalar(select(func.count()).select_from(ConsentRecord).where(ConsentRecord.user_id == who)) == 3   # and every answer is kept


def test_saying_no_to_the_required_question_still_needs_an_answer(api):
    who = api.seed(lambda s: api.person(s, consent=False))
    api.client.post("/privacy/consent", json={"kind": "results", "granted": False}, headers=api.headers(who))
    assert api.client.get("/privacy/consent", headers=api.headers(who)).json()["needs_consent"] is True


def test_a_new_text_asks_everyone_again(api, monkeypatch):
    who = api.seed(lambda s: api.person(s))
    assert api.client.get("/privacy/consent", headers=api.headers(who)).json()["needs_consent"] is False
    monkeypatch.setattr(privacy, "CONSENT_VERSION", "2027-01-01.1")
    assert api.client.get("/privacy/consent", headers=api.headers(who)).json()["needs_consent"] is True


def test_a_headset_can_read_the_question_but_cannot_answer_it(world):
    token = headset_token(world, world.trainee)
    assert world.api.client.get("/privacy/consent", headers=bearer(token)).status_code == 200
    assert world.api.client.post("/privacy/consent", json={"kind": "results", "granted": True}, headers=bearer(token)).status_code == 403
    for path in ("/privacy/access-log", "/privacy/export"):
        assert world.api.client.get(path, headers=bearer(token)).status_code == 403
    assert world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}, headers=bearer(token)).status_code == 403


def test_a_session_from_a_person_who_has_not_agreed_is_refused_until_they_do(api):
    org, who = api.seed(lambda s: (lambda o, p: (accounts.add_membership(s, p, o.id, "trainee"), (o.id, p))[1])(accounts.create_organisation(s, "Gamma", "gamma-org"), api.person(s, consent=False)))
    url = f"/organisations/{org}/sessions"
    refused = api.client.post(url, content=make_file(), headers={**api.headers(who), **NDJSON})
    assert (refused.status_code, refused.json()["detail"]) == (403, {"code": "consent_required"})
    api.client.post("/privacy/consent", json={"kind": "results", "granted": True}, headers=api.headers(who))
    assert api.client.post(url, content=make_file(), headers={**api.headers(who), **NDJSON}).status_code == 202


# --- T-PR3: the access log -------------------------------------------------------------------------------------------------------------------------------------------------------

def test_T_PR3_a_supervisor_looking_at_an_individual_is_in_that_persons_access_log(world):
    sid = add_session(world, world.org_a, world.trainee)
    base = f"/organisations/{world.org_a}"
    assert world.get(f"{base}/sessions/{sid}/report", world.supervisor).status_code == 200
    assert world.get(f"{base}/members/{world.trainee}/progress", world.supervisor).status_code == 200
    assert world.get(f"{base}/sessions", world.admin, trainee_id=str(world.trainee)).status_code == 200
    assert world.get(f"{base}/overview", world.admin, trainee_id=str(world.trainee)).status_code == 200
    assert world.get(f"{base}/export.csv", world.supervisor).status_code == 200
    entries = world.get("/privacy/access-log", world.trainee).json()
    assert sorted((e["action"], e["viewer_role"]) for e in entries) == [("view_progress", "org_admin"), ("view_progress", "supervisor"), ("view_report", "supervisor"), ("view_sessions", "org_admin")]
    assert {e["viewer"] for e in entries} <= {"Sam Supervisor", "Ada Admin"} or all(e["viewer"] for e in entries)
    assert all(e["organisation"] == "Alpha" and e["at"] for e in entries)
    assert next(e for e in entries if e["action"] == "view_report")["session_id"] == str(sid)
    assert world.get("/privacy/access-log", world.colleague).json() == []   # looks at someone else are not on a colleague's page


def test_what_is_not_a_look_at_an_individual_is_not_logged_per_person(world):
    add_session(world, world.org_a, world.trainee)
    base = f"/organisations/{world.org_a}"
    world.get(f"{base}/overview", world.supervisor)                         # an aggregate over several people
    world.get(f"{base}/cohorts/{world.cohort_a}/overview", world.supervisor)
    world.get(f"{base}/sessions", world.supervisor)                          # the whole list, no one person
    world.get(f"{base}/progress", world.trainee)                             # a person looking at themselves
    world.get(f"{base}/sessions", world.trainee)
    with DbSession(world.api.engine) as s:
        assert s.scalar(select(func.count()).select_from(AccessLog)) == 0


def test_a_look_that_is_refused_is_not_logged(world):
    sid = add_session(world, world.org_a, world.trainee)
    world.get(f"/organisations/{world.org_a}/sessions/{sid}/report", world.colleague)     # a colleague is refused
    world.get(f"/organisations/{world.org_b}/sessions/{sid}/report", world.supervisor_b)  # another organisation is refused
    with DbSession(world.api.engine) as s:
        assert s.scalar(select(func.count()).select_from(AccessLog)) == 0


# --- T-PR2: export my data ---------------------------------------------------------------------------------------------------------------------------------------------------------

def test_T_PR2_export_my_data_returns_only_that_persons_data(world):
    mine = add_session(world, world.org_a, world.trainee, critical=["critical.x"])
    theirs = add_session(world, world.org_a, world.colleague)
    other_org = add_session(world, world.org_b, world.trainee_b)
    world.get(f"/organisations/{world.org_a}/members/{world.trainee}/progress", world.supervisor)
    r = world.get("/privacy/export", world.trainee)
    assert r.status_code == 200 and "attachment" in r.headers["content-disposition"] and r.headers["cache-control"] == "no-store"
    body, raw = r.json(), r.text
    assert [s["session_id"] for s in body["sessions"]] == [str(mine)] and body["sessions"][0]["score_items"][0]["item"] == "critical.x" and body["sessions"][0]["report"]["schema"] == "debrief_facts.v1"
    assert body["profile"]["display_name"] == "Jane Doe" and body["memberships"][0]["organisation"] == "Alpha" and body["cohorts"] == [{"cohort": "Cohort 1", "role": "trainee"}]
    assert body["consents"][0]["kind"] == "results" and len(body["who_looked_at_my_data"]) == 1
    with DbSession(world.api.engine) as s:
        others = {s.get(Profile, u).pseudonym for u in (world.colleague, world.trainee_b)}   # the supervisor who looked is, rightly, named in "who looked at my data"
    assert not any(p in raw for p in others) and str(theirs) not in raw and str(other_org) not in raw and "Beta" not in raw
    assert "@" not in raw   # the email is held by the sign-in service and is not in the file
    assert world.api.client.get("/privacy/export").status_code == 401


# --- T-PR1: delete my data ---------------------------------------------------------------------------------------------------------------------------------------------------------

def uuid_hits(engine, value) -> int:
    """How many cells in every table of the database hold this id (as a uuid or as text)."""
    hits = 0
    with engine.connect() as conn:
        columns = conn.execute(text("select table_name, column_name, data_type from information_schema.columns where table_schema = 'public' and data_type in ('uuid', 'text', 'character varying', 'jsonb')")).all()
        for table, column, kind in columns:
            expr = f'"{column}"::text'
            hits += conn.execute(text(f'select count(*) from "{table}" where {expr} like :v'), {"v": f"%{value}%"}).scalar_one()
    return hits


def test_T_PR1_a_delete_leaves_no_email_name_or_link_and_the_totals_do_not_change(world):
    add_session(world, world.org_a, world.trainee, critical=["critical.x"], score=60)
    add_session(world, world.org_a, world.trainee, score=80)
    add_session(world, world.org_a, world.colleague, score=70)
    world.get(f"/organisations/{world.org_a}/members/{world.trainee}/progress", world.supervisor)   # an access-log row about them
    world.get(f"/organisations/{world.org_a}/members/{world.supervisor}/progress", world.admin)
    token = headset_token(world, world.trainee)
    before = world.get(f"/organisations/{world.org_a}/overview", world.supervisor).json()
    with DbSession(world.api.engine) as s:
        old_pseudonym = s.get(Profile, world.trainee).pseudonym
    assert uuid_hits(world.api.engine, world.trainee) > 0 and uuid_hits(world.api.engine, "Jane Doe") > 0

    r = world.api.client.post("/privacy/delete-my-data", json={"confirm": "delete"}, headers=world.h(world.trainee))
    assert r.status_code == 204
    assert world.api.admin.deleted == [str(world.trainee)]                                          # the sign-in account (and the email with it) is removed

    assert uuid_hits(world.api.engine, world.trainee) == 0                                          # no cell anywhere holds the old id
    assert uuid_hits(world.api.engine, "Jane Doe") == 0 and uuid_hits(world.api.engine, old_pseudonym) == 0   # nor the name, nor the old pseudonymous id
    after = world.get(f"/organisations/{world.org_a}/overview", world.supervisor).json()
    assert after == before                                                                          # every total is the same, including the number of people
    assert world.api.client.get("/me", headers=bearer(token)).status_code == 401                    # the headset is signed out
    with DbSession(world.api.engine) as s:
        stand_in = s.scalars(select(Profile).where(Profile.deleted_at.is_not(None))).one()
        assert stand_in.display_name == "" and stand_in.pseudonym != old_pseudonym and not stand_in.is_platform_admin
        assert s.scalar(select(func.count()).select_from(Session).where(Session.user_id == stand_in.user_id)) == 2
        assert s.scalar(select(func.count()).select_from(Membership).where(Membership.user_id == stand_in.user_id)) == 0
        assert s.scalar(select(func.count()).select_from(ConsentRecord).where(ConsentRecord.user_id == stand_in.user_id)) == 0
        assert s.scalar(select(func.count()).select_from(AccessLog).where(AccessLog.subject_id == stand_in.user_id)) == 0
    listed = world.get(f"/organisations/{world.org_a}/sessions", world.supervisor).json()["items"]
    assert sum(1 for i in listed if i["trainee"] == stand_in.pseudonym) == 2 and all(i["display_name"] != "Jane Doe" for i in listed)


def test_what_a_deleted_person_looked_at_stays_but_no_longer_names_them(world):
    add_session(world, world.org_a, world.colleague)
    world.get(f"/organisations/{world.org_a}/members/{world.colleague}/progress", world.supervisor)
    assert world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}, headers=world.h(world.supervisor)).status_code == 204
    entry = next(iter(world.get("/privacy/access-log", world.colleague).json()))
    assert entry["viewer_pseudonym"] == "deleted" and entry["viewer"] == "deleted"   # the person looked at still sees that someone did, not who


def test_the_last_administrator_cannot_delete_their_data_and_nothing_changes(world):
    r = world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}, headers=world.h(world.admin))
    assert (r.status_code, r.json()["detail"]) == (409, "last_administrator")
    assert world.api.admin.deleted == []
    with DbSession(world.api.engine) as s:
        assert s.get(Profile, world.admin) is not None and s.scalar(select(func.count()).select_from(Membership).where(Membership.user_id == world.admin)) == 1


def test_a_failed_account_deletion_cuts_nothing_and_can_be_retried(world):
    add_session(world, world.org_a, world.trainee)
    world.api.admin.delete_fails = True
    r = world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}, headers=world.h(world.trainee))
    assert (r.status_code, r.json()["detail"]) == (502, "account_service_unavailable")
    with DbSession(world.api.engine) as s:
        assert s.get(Profile, world.trainee).display_name == "Jane Doe" and s.scalar(select(func.count()).select_from(Session).where(Session.user_id == world.trainee)) == 1
    world.api.admin.delete_fails = False
    assert world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}, headers=world.h(world.trainee)).status_code == 204


def test_delete_needs_the_word_and_a_web_sign_in(world):
    assert world.api.client.post("/privacy/delete-my-data", json={"confirm": "yes"}, headers=world.h(world.trainee)).status_code == 422
    assert world.api.client.post("/privacy/delete-my-data", json={"confirm": ""}, headers=world.h(world.trainee)).status_code == 422
    assert world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}).status_code == 401
    assert world.api.admin.deleted == []


# --- T-PR4: research ---------------------------------------------------------------------------------------------------------------------------------------------------------------

def test_T_PR4_a_person_who_did_not_agree_to_research_is_absent_from_a_research_export(world):
    from server.repositories import privacy
    for who, org in ((world.trainee, world.org_a), (world.colleague, world.org_a), (world.trainee_b, world.org_b), (world.supervisor, world.org_a)):
        add_session(world, org, who)
    def answers(s):
        privacy.record_consent(s, world.trainee, "research", True)                            # agreed
        privacy.record_consent(s, world.colleague, "research", True)
        privacy.record_consent(s, world.colleague, "research", False)                         # agreed, then changed their mind
        # trainee_b never answered; supervisor never answered
    world.api.seed(answers)
    rows = rows_of(world.get("/export.csv", world.platform))
    with DbSession(world.api.engine) as s:
        pseudonyms = {n: s.get(Profile, u).pseudonym for n, u in (("trainee", world.trainee), ("colleague", world.colleague), ("trainee_b", world.trainee_b), ("supervisor", world.supervisor))}
    assert {r["trainee"] for r in rows} == {pseudonyms["trainee"]}, pseudonyms
    assert len(rows_of(world.get(f"/organisations/{world.org_a}/export.csv", world.supervisor))) == 3   # the organisation's own export is about results, not research


# --- retention -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

def old_session(world, org, who, months_ago, **kw):
    return add_session(world, org, who, days_ago=int(months_ago * 30.5), **kw)


def test_the_retention_job_removes_old_logs_and_keeps_every_result(world):
    from server.jobs import retention
    old = old_session(world, world.org_a, world.trainee, 25, critical=["critical.x"])
    recent = old_session(world, world.org_a, world.trainee, 3)
    before = world.get(f"/organisations/{world.org_a}/overview", world.supervisor).json()
    done = world.api.seed(lambda s: retention.run(s, config.get_settings(), now=dt.datetime(2026, 10, 14, 12, tzinfo=dt.timezone.utc)))
    assert done == {"sessions_purged": 1, "pending_rejected": 0, "access_log_deleted": 0, "invitation_emails_cleared": 0}
    with DbSession(world.api.engine) as s:
        a, b = s.get(Session, old), s.get(Session, recent)
        assert (a.log_gz, a.report_html_gz, a.header_line) == (b"", None, "") and a.log_purged_at is not None
        assert b.log_gz != b"" and b.report_html_gz is not None and b.log_purged_at is None
    assert world.get(f"/organisations/{world.org_a}/overview", world.supervisor).json() == before          # totals, results and critical items are unchanged
    assert world.get(f"/organisations/{world.org_a}/sessions/{old}/report", world.trainee).status_code == 200
    assert world.get(f"/organisations/{world.org_a}/sessions/{old}/report.html", world.trainee).status_code == 404   # the stored page is gone
    again = world.api.seed(lambda s: retention.run(s, config.get_settings(), now=dt.datetime(2026, 10, 14, 12, tzinfo=dt.timezone.utc)))
    assert again["sessions_purged"] == 0                                                                     # running it twice does nothing more


def test_an_organisation_can_choose_a_shorter_period_within_the_limits(world):
    from server.jobs import retention
    s8 = old_session(world, world.org_a, world.trainee, 8)
    other = old_session(world, world.org_b, world.trainee_b, 8)
    url = f"/organisations/{world.org_a}/retention"
    assert world.get(url, world.admin).json() == {"retention_months": None, "effective_months": 24, "default_months": 24}
    assert world.api.client.put(url, json={"retention_months": 6}, headers=world.h(world.admin)).json()["effective_months"] == 6
    for bad in (5, 61, 0, -1):
        assert world.api.client.put(url, json={"retention_months": bad}, headers=world.h(world.admin)).status_code == 422
    for who in (world.supervisor, world.trainee, world.supervisor_b):
        assert world.api.client.put(url, json={"retention_months": 12}, headers=world.h(who)).status_code in (403, 404)
    world.api.seed(lambda s: retention.run(s, config.get_settings()))
    with DbSession(world.api.engine) as s:
        assert s.get(Session, s8).log_purged_at is not None            # Alpha chose 6 months
        assert s.get(Session, other).log_purged_at is None             # Beta kept the default of 24
    assert world.api.client.put(url, json={"retention_months": None}, headers=world.h(world.admin)).json()["retention_months"] is None


def test_old_access_log_rows_and_purged_pending_sessions_are_cleaned_up(world):
    from server.jobs import retention
    pending = old_session(world, world.org_a, world.trainee, 30, status="pending")
    add_session(world, world.org_a, world.trainee)
    world.get(f"/organisations/{world.org_a}/members/{world.trainee}/progress", world.supervisor)
    world.api.seed(lambda s: s.execute(update(AccessLog).values(at=dt.datetime.now(dt.timezone.utc) - dt.timedelta(days=800))))
    world.get(f"/organisations/{world.org_a}/members/{world.trainee}/progress", world.admin)
    done = world.api.seed(lambda s: retention.run(s, config.get_settings()))
    assert done == {"sessions_purged": 1, "pending_rejected": 1, "access_log_deleted": 1, "invitation_emails_cleared": 0}
    with DbSession(world.api.engine) as s:
        assert s.get(Session, pending).status == "rejected" and s.get(Session, pending).reject_reason == "RETENTION_EXPIRED"
    assert len(world.get("/privacy/access-log", world.trainee).json()) == 1


def test_the_email_of_an_invitation_nobody_accepted_is_removed_a_month_after_it_expired(world):
    from server.jobs import retention
    from server.models.accounts import Invitation
    now = dt.datetime.now(dt.timezone.utc)
    def build(s):
        old = accounts.create_invitation(s, world.org_a, "stale@example.org", "trainee", None, world.admin, 14)
        old.expires_at = now - dt.timedelta(days=45)
        fresh = accounts.create_invitation(s, world.org_a, "fresh@example.org", "trainee", None, world.admin, 14)
        recently_expired = accounts.create_invitation(s, world.org_a, "recent@example.org", "trainee", None, world.admin, 14)
        recently_expired.expires_at = now - dt.timedelta(days=3)
        return old.id, fresh.id, recently_expired.id
    old_id, fresh_id, recent_id = world.api.seed(build)
    done = world.api.seed(lambda s: retention.run(s, config.get_settings()))
    assert done["invitation_emails_cleared"] == 1
    with DbSession(world.api.engine) as s:
        assert (s.get(Invitation, old_id).email, s.get(Invitation, old_id).status) == (None, "revoked")
        assert s.get(Invitation, fresh_id).email == "fresh@example.org" and s.get(Invitation, recent_id).email == "recent@example.org"


def test_the_retention_route_is_for_the_scheduler_only(world, monkeypatch):
    assert world.api.client.post("/internal/retention").status_code == 404           # closed while there is no internal key
    monkeypatch.setenv("INTERNAL_KEY", "scheduler-key-for-tests")
    config.get_settings.cache_clear()
    assert world.api.client.post("/internal/retention").status_code == 403
    assert world.api.client.post("/internal/retention", headers={"X-Internal-Key": "wrong"}).status_code == 403
    r = world.api.client.post("/internal/retention", headers={"X-Internal-Key": "scheduler-key-for-tests"})
    assert r.status_code == 200 and set(r.json()) == {"sessions_purged", "pending_rejected", "access_log_deleted", "invitation_emails_cleared"}
    config.get_settings.cache_clear()


def test_the_retention_periods_are_checked(monkeypatch):
    for bad in ("5", "61"):
        monkeypatch.setenv("RETENTION_SESSION_MONTHS", bad)
        with pytest.raises(Exception):
            config.Settings()
    monkeypatch.setenv("RETENTION_SESSION_MONTHS", "24")
    assert config.Settings().retention_access_log_months == 24


# --- T-PR5: nothing personal in the logs ---------------------------------------------------------------------------------------------------------------------------------------

def test_the_cleaner_removes_emails_tokens_and_keys():
    jwt = FAKE_TOKEN
    samples = {
        "person.name+tag@example.org was invited": "[email] was invited",
        f"token {jwt} refused": "token [token] refused",
        "device psd_" + "A" * 43 + " refused": "device [token] refused",
        "key sb_secret_abcDEF123_xyz leaked": "key [key] leaked",
        "Authorization: Bearer abcdefghijklmnop12345": "Authorization: Bearer [token]",
    }
    for raw, expected in samples.items():
        assert clean(raw) == expected
    assert clean("session 123e4567-e89b-12d3-a456-426614174000 stored") == "session 123e4567-e89b-12d3-a456-426614174000 stored"   # ids are not personal on their own


def test_T_PR5_the_log_of_a_whole_day_has_no_email_address(world, caplog):
    caplog.set_level(logging.DEBUG)
    logging.getLogger("procsense.test").warning("someone typed sam@example.org by mistake and a token " + FAKE_TOKEN)
    world.api.client.post(f"/organisations/{world.org_a}/invitations", json={"email": "newcomer@example.org", "role": "trainee"}, headers=world.h(world.admin))
    world.api.client.get("/me", headers={"Authorization": "Bearer " + BAD_TOKEN})                   # a bad token
    world.api.client.get("/me", headers=world.api.headers(world.trainee, "jane.doe@example.org"))
    world.api.client.post("/privacy/delete-my-data", json={"confirm": "DELETE"}, headers=world.api.headers(world.colleague, "colleague@example.org"))
    text = caplog.text
    assert "@" not in text and "example.org" not in text and "eyJ" not in text
    assert "[email]" in text and "[token]" in text   # the safety net did its work on the deliberate mistake


def test_the_new_table_is_closed_to_the_public_roles(migrated):
    with migrated.connect() as conn:
        assert conn.execute(text("select relrowsecurity from pg_class where relname = 'access_log'")).scalar_one() is True
        assert conn.execute(text("select count(*) from pg_policies where tablename = 'access_log'")).scalar_one() == 0
