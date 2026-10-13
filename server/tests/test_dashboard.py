"""Reports, dashboard data and the CSV export on a real PostgreSQL (p1-backend-s04): the report is what verification stored, nothing but verified sessions is counted (T-BE5), nothing leaks between organisations,
the export carries pseudonyms only (T-BE3), and the dashboard queries are fast on a year of sessions."""
from __future__ import annotations

import csv
import datetime as dt
import gzip
import io
import time
import uuid
from dataclasses import dataclass

import pytest
from sqlalchemy import insert, update
from sqlalchemy.orm import Session as DbSession

from server.models.accounts import Profile
from server.models.sessions import Session, SessionBehaviour, SessionScoreItem
from server.repositories import accounts
from server.routers.export import COLUMNS, organisation_code

pytestmark = pytest.mark.db
NOW = dt.datetime(2026, 10, 14, 12, 0, tzinfo=dt.timezone.utc)


@dataclass
class World:
    api: object
    org_a: uuid.UUID
    org_b: uuid.UUID
    trainee: uuid.UUID
    colleague: uuid.UUID
    supervisor: uuid.UUID
    admin: uuid.UUID
    trainee_b: uuid.UUID
    supervisor_b: uuid.UUID
    platform: uuid.UUID
    cohort_a: uuid.UUID
    cohort_b: uuid.UUID

    def h(self, who) -> dict:
        return self.api.headers(who)

    def get(self, url, who, **params):
        return self.api.client.get(url, headers=self.h(who), params=params)


@pytest.fixture
def world(api) -> World:
    def build(s: DbSession) -> World:
        a, b = accounts.create_organisation(s, "Alpha", "alpha-org"), accounts.create_organisation(s, "Beta", "beta-org")
        p = {n: api.person(s) for n in ("trainee", "colleague", "supervisor", "admin", "trainee_b", "supervisor_b")}
        platform = api.person(s, platform_admin=True)
        for name, org, role in (("trainee", a, "trainee"), ("colleague", a, "trainee"), ("supervisor", a, "supervisor"), ("admin", a, "org_admin"), ("trainee_b", b, "trainee"), ("supervisor_b", b, "supervisor")):
            accounts.add_membership(s, p[name], org.id, role)
        ca, cb = accounts.create_cohort(s, a.id, "Cohort 1"), accounts.create_cohort(s, b.id, "Cohort B")
        accounts.set_cohort_member(s, ca.id, p["trainee"], "trainee")
        accounts.set_cohort_member(s, cb.id, p["trainee_b"], "trainee")
        s.get(Profile, p["trainee"]).display_name = "Jane Doe"
        return World(api, a.id, b.id, p["trainee"], p["colleague"], p["supervisor"], p["admin"], p["trainee_b"], p["supervisor_b"], platform, ca.id, cb.id)
    return api.seed(build)


def add_session(world: World, org, user, *, case="VAR-S1-A", mode="practice", status="verified", result="SUCCESS", score=70.0, days_ago=1, behaviours=None, critical=(), html=True,
                end_state="DISCHARGE_READY", content="abc123abc123-practice") -> uuid.UUID:
    sid = uuid.uuid4()
    behaviours = behaviours if behaviours is not None else {"b01": (3, 1)}

    def build(s: DbSession):
        facts = {"schema": "debrief_facts.v1", "case": case, "mode": mode, "end_state": end_state, "result": result, "items": [], "competencies": [], "timeline": [{"seq": 1}]}
        verified = status == "verified"
        s.add(Session(id=sid, organisation_id=org, user_id=user, case_id=case, mode=mode, seed="1", content_version=content, engine_version="0.0.1", log_hash="a" * 64, log_entries=3, ended_step=10,
                      log_gz=gzip.compress(b"x"), status=status, received_at=NOW - dt.timedelta(days=days_ago), verified_at=NOW if verified else None,
                      end_state=end_state if verified else None, result=result if verified else None, process_score=score if verified else None,
                      report_json=facts if verified else None, report_html_gz=gzip.compress(b"<html><body>report of " + sid.hex.encode() + b"</body></html>") if verified and html else None))
        s.flush()
        for ordinal, item in enumerate(critical):
            s.add(SessionScoreItem(session_id=sid, ordinal=ordinal, item_id=item, behaviour="b01", severity="critical", status="critical", points=-5, step=1, evidence=[1]))
        for name, (credits, penalties) in behaviours.items():
            s.add(SessionBehaviour(session_id=sid, behaviour=name, credits=credits, penalties=penalties, net=credits - penalties))
    world.api.seed(build)
    return sid


def rows_of(response) -> list[dict]:
    assert response.status_code == 200, response.text
    return list(csv.DictReader(io.StringIO(response.text)))


# --- the report --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

def test_the_report_is_what_verification_stored_and_says_the_weights_are_provisional(world):
    sid = add_session(world, world.org_a, world.trainee)
    r = world.get(f"/organisations/{world.org_a}/sessions/{sid}/report", world.trainee)
    assert r.status_code == 200
    body = r.json()
    assert body["facts"]["schema"] == "debrief_facts.v1" and body["facts"]["timeline"] == [{"seq": 1}]
    assert (body["case_id"], body["content_version"], body["engine_version"], body["result"], body["process_score"]) == ("VAR-S1-A", "abc123abc123-practice", "0.0.1", "SUCCESS", 70.0)
    assert body["process_score_provisional"] is True and body["trainee"].startswith("T-") and "Jane" not in r.text


def test_the_report_is_seen_by_its_owner_and_the_supervisors_of_the_organisation_only(world):
    sid = add_session(world, world.org_a, world.trainee)
    url = f"/organisations/{world.org_a}/sessions/{sid}/report"
    assert world.get(url, world.trainee).status_code == 200
    assert world.get(url, world.supervisor).status_code == 200 and world.get(url, world.admin).status_code == 200
    assert world.get(url, world.colleague).status_code == 404      # a colleague: not found, not forbidden
    assert world.get(url, world.supervisor_b).status_code == 404   # a supervisor of another organisation
    assert world.get(f"/organisations/{world.org_b}/sessions/{sid}/report", world.supervisor_b).status_code == 404   # asking through their own organisation does not help either
    assert world.api.client.get(url).status_code == 401


def test_a_session_that_is_not_verified_has_no_report(world):
    for status in ("pending", "rejected", "unverifiable"):
        sid = add_session(world, world.org_a, world.trainee, status=status)
        r = world.get(f"/organisations/{world.org_a}/sessions/{sid}/report", world.trainee)
        assert (r.status_code, r.json()["detail"]) == (409, {"code": "not_verified", "status": status})
    assert world.get(f"/organisations/{world.org_a}/sessions/{uuid.uuid4()}/report", world.trainee).status_code == 404


def test_the_html_report_is_a_page_that_cannot_run_or_load_anything(world):
    sid = add_session(world, world.org_a, world.trainee)
    r = world.get(f"/organisations/{world.org_a}/sessions/{sid}/report.html", world.trainee)
    assert r.status_code == 200 and r.headers["content-type"].startswith("text/html") and sid.hex in r.text
    assert "default-src 'none'" in r.headers["content-security-policy"] and r.headers["x-content-type-options"] == "nosniff" and r.headers["cache-control"] == "no-store"
    assert world.get(f"/organisations/{world.org_a}/sessions/{sid}/report.html", world.colleague).status_code == 404
    bare = add_session(world, world.org_a, world.trainee, html=False)
    assert world.get(f"/organisations/{world.org_a}/sessions/{bare}/report.html", world.trainee).status_code == 404


# --- who sees which list ---------------------------------------------------------------------------------------------------------------------------------------------------------------

def test_a_trainee_lists_only_their_own_sessions_whatever_they_ask_for(world):
    mine = add_session(world, world.org_a, world.trainee)
    add_session(world, world.org_a, world.colleague)
    url = f"/organisations/{world.org_a}/sessions"
    page = world.get(url, world.trainee).json()
    assert [i["session_id"] for i in page["items"]] == [str(mine)] and page["total"] == 1 and page["items"][0]["display_name"] is None
    assert world.get(url, world.trainee, trainee_id=str(world.colleague)).json()["total"] == 1   # the filter cannot widen it


def test_a_supervisor_lists_the_organisation_with_names_and_filters(world):
    add_session(world, world.org_a, world.trainee, case="VAR-S1-A", result="SUCCESS")
    add_session(world, world.org_a, world.colleague, case="VAR-S2-A", mode="assessment", result="FAIL")
    add_session(world, world.org_b, world.trainee_b)
    url = f"/organisations/{world.org_a}/sessions"
    everything = world.get(url, world.supervisor).json()
    assert everything["total"] == 2 and {i["display_name"] for i in everything["items"]} == {"Jane Doe", None}
    assert world.get(url, world.supervisor, mode="assessment").json()["total"] == 1
    assert world.get(url, world.supervisor, result="FAIL").json()["items"][0]["case_id"] == "VAR-S2-A"
    assert world.get(url, world.supervisor, case_id="VAR-S1-A").json()["total"] == 1
    assert world.get(url, world.supervisor, cohort_id=str(world.cohort_a)).json()["total"] == 1
    assert world.get(url, world.supervisor, trainee_id=str(world.colleague)).json()["total"] == 1
    assert world.get(url, world.supervisor, limit=1).json()["items"].__len__() == 1 and world.get(url, world.supervisor, limit=1, offset=1).json()["items"].__len__() == 1
    assert world.get(url, world.supervisor, since=(NOW - dt.timedelta(hours=1)).isoformat()).json()["total"] == 0


def test_pending_uploads_are_listed_only_when_asked_for(world):
    add_session(world, world.org_a, world.trainee, status="pending")
    url = f"/organisations/{world.org_a}/sessions"
    assert world.get(url, world.trainee).json()["total"] == 0
    assert world.get(url, world.trainee, status="pending").json()["total"] == 1 and world.get(url, world.trainee, status="any").json()["total"] == 1


# --- progress and overview --------------------------------------------------------------------------------------------------------------------------------------------------------------

def test_a_person_sees_their_own_progress_and_unverified_sessions_never_count(world):
    add_session(world, world.org_a, world.trainee, days_ago=10, result="FAIL", score=40, behaviours={"b01": (1, 3), "b02": (2, 0)}, critical=["critical.wrong_patient"])
    add_session(world, world.org_a, world.trainee, days_ago=3, result="SUCCESS", score=80, behaviours={"b01": (4, 0)}, critical=["critical.wrong_patient"])
    add_session(world, world.org_a, world.trainee, status="pending")
    add_session(world, world.org_a, world.trainee, status="rejected", score=99)
    add_session(world, world.org_a, world.colleague, score=10)
    body = world.get(f"/organisations/{world.org_a}/progress", world.trainee).json()
    s = body["summary"]
    assert s["sessions"] == 2 and s["trainees"] == 1 and s["average_process_score"] == 60.0 and s["process_score_provisional"] is True
    assert {r["result"]: r["count"] for r in s["results"]} == {"FAIL": 1, "SUCCESS": 1}
    assert s["critical_items"] == [{"item_id": "critical.wrong_patient", "count": 2}]
    by = {b["behaviour"]: b for b in s["behaviours"]}
    assert (by["b01"]["sessions"], by["b01"]["credits"], by["b01"]["penalties"], by["b01"]["net"], by["b01"]["average_net"]) == (2, 5, 3, 2, 1.0)
    assert [b["behaviour"] for b in s["behaviours"]] == ["b01", "b02"]   # lowest average first: the one that needs work
    assert [p["process_score"] for p in body["series"]] == [40.0, 80.0] and body["trainee"].startswith("T-")   # oldest first


def test_a_supervisor_sees_a_persons_progress_and_a_trainee_cannot_see_anyone_elses(world):
    add_session(world, world.org_a, world.trainee, score=55)
    url = f"/organisations/{world.org_a}/members/{world.trainee}/progress"
    assert world.get(url, world.supervisor).json()["summary"]["average_process_score"] == 55.0
    assert world.get(url, world.colleague).status_code == 403
    assert world.get(url, world.trainee).status_code == 403
    assert world.get(f"/organisations/{world.org_a}/members/{world.trainee_b}/progress", world.supervisor).status_code == 404   # not a member of this organisation
    assert world.get(f"/organisations/{world.org_a}/members/{world.trainee}/progress", world.supervisor_b).status_code == 404


def test_the_organisation_overview_counts_weeks_results_critical_items_and_weak_behaviours(world):
    add_session(world, world.org_a, world.trainee, days_ago=1, result="SUCCESS", behaviours={"b01": (5, 0), "b07": (0, 4)}, critical=["critical.a", "critical.b"])
    add_session(world, world.org_a, world.colleague, days_ago=2, result="FAIL", behaviours={"b01": (1, 1), "b07": (0, 2)}, critical=["critical.a"])
    add_session(world, world.org_a, world.colleague, days_ago=40, result="SUCCESS_WITH_DELAY", behaviours={})
    add_session(world, world.org_a, world.colleague, status="pending", critical=["critical.zzz"])
    add_session(world, world.org_b, world.trainee_b, critical=["critical.b_only"])
    o = world.get(f"/organisations/{world.org_a}/overview", world.supervisor).json()
    assert o["sessions"] == 3 and o["trainees"] == 2
    assert sum(w["sessions"] for w in o["per_week"]) == 3 and len(o["per_week"]) >= 2 and all(dt.date.fromisoformat(w["week"]).weekday() == 0 for w in o["per_week"])
    assert {r["result"]: r["count"] for r in o["results"]} == {"SUCCESS": 1, "FAIL": 1, "SUCCESS_WITH_DELAY": 1}
    assert o["critical_items"][0] == {"item_id": "critical.a", "count": 2} and {c["item_id"] for c in o["critical_items"]} == {"critical.a", "critical.b"}
    assert o["behaviours"][0]["behaviour"] == "b07" and o["behaviours"][0]["average_net"] == -3.0   # the weakest first
    assert world.get(f"/organisations/{world.org_a}/overview", world.admin).status_code == 200


def test_the_overview_can_be_narrowed_to_a_cohort_a_mode_or_a_person(world):
    add_session(world, world.org_a, world.trainee, mode="practice")
    add_session(world, world.org_a, world.trainee, mode="assessment")
    add_session(world, world.org_a, world.colleague, mode="practice")
    base = f"/organisations/{world.org_a}"
    assert world.get(f"{base}/cohorts/{world.cohort_a}/overview", world.supervisor).json()["sessions"] == 2
    assert world.get(f"{base}/overview", world.supervisor, cohort_id=str(world.cohort_a), mode="assessment").json()["sessions"] == 1
    assert world.get(f"{base}/overview", world.supervisor, trainee_id=str(world.colleague)).json()["sessions"] == 1
    assert world.get(f"{base}/overview", world.supervisor, mode="research").status_code == 422


def test_a_trainee_cannot_open_the_overviews(world):
    base = f"/organisations/{world.org_a}"
    for url in (f"{base}/overview", f"{base}/cohorts/{world.cohort_a}/overview", f"{base}/export.csv"):
        assert world.get(url, world.trainee).status_code == 403


def test_an_empty_organisation_gives_zeros_not_errors(world):
    o = world.get(f"/organisations/{world.org_a}/overview", world.supervisor).json()
    assert (o["sessions"], o["trainees"], o["average_process_score"], o["per_week"], o["results"], o["critical_items"], o["behaviours"]) == (0, 0, None, [], [], [], [])


# --- T-BE5: nothing crosses organisations ---------------------------------------------------------------------------------------------------------------------------------------------

def test_no_endpoint_of_organisation_a_answers_a_person_of_organisation_b_and_no_total_includes_the_other(world):
    sid = add_session(world, world.org_a, world.trainee, critical=["critical.a_only"])
    add_session(world, world.org_b, world.trainee_b, critical=["critical.b_only"])
    a, b = f"/organisations/{world.org_a}", f"/organisations/{world.org_b}"
    urls = [f"{a}/sessions", f"{a}/sessions/{sid}/report", f"{a}/sessions/{sid}/report.html", f"{a}/progress", f"{a}/members/{world.trainee}/progress", f"{a}/overview",
            f"{a}/cohorts/{world.cohort_a}/overview", f"{a}/export.csv"]
    for url in urls:
        assert world.get(url, world.supervisor_b).status_code == 404, url
        assert world.get(url, world.trainee_b).status_code == 404, url
    assert world.get(f"{b}/overview", world.supervisor_b).json()["critical_items"] == [{"item_id": "critical.b_only", "count": 1}]
    assert world.get(f"{a}/overview", world.supervisor).json()["critical_items"] == [{"item_id": "critical.a_only", "count": 1}]
    assert world.get(f"{b}/overview", world.supervisor_b, cohort_id=str(world.cohort_a)).status_code == 404   # a cohort of another organisation
    assert world.get(f"{b}/sessions", world.supervisor_b, cohort_id=str(world.cohort_a)).status_code == 404
    assert world.get(f"{b}/cohorts/{world.cohort_a}/overview", world.supervisor_b).status_code == 404
    assert world.get(f"{b}/sessions/{sid}/report", world.supervisor_b).status_code == 404
    assert world.get(f"{b}/sessions", world.supervisor_b, trainee_id=str(world.trainee)).json()["total"] == 0   # a person of A, asked through B


# --- the export ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

def test_the_export_is_pseudonymous_documented_and_counts_verified_sessions_only(world):
    add_session(world, world.org_a, world.trainee, score=61.5, critical=["critical.x"], behaviours={"b01": (4, 1), "b02": (2, 2)})
    add_session(world, world.org_a, world.colleague, status="pending")
    add_session(world, world.org_b, world.trainee_b)
    r = world.get(f"/organisations/{world.org_a}/export.csv", world.supervisor)
    assert r.headers["content-type"].startswith("text/csv") and "attachment" in r.headers["content-disposition"] and r.headers["cache-control"] == "no-store"
    rows = rows_of(r)
    assert list(rows[0].keys()) == COLUMNS and len(rows) == 1
    row = rows[0]
    assert row["organisation"] == "alpha-org" and row["trainee"].startswith("T-") and row["process_score"] == "61.50" and row["process_score_provisional"] == "true"
    assert (row["credits"], row["penalties"], row["critical_items"], row["mode"], row["content_version"]) == ("6", "3", "1", "practice", "abc123abc123-practice")
    assert "Jane" not in r.text and "@" not in r.text   # T-BE3: no names, no emails
    assert len(rows_of(world.get(f"/organisations/{world.org_a}/export.csv", world.supervisor, cohort_id=str(world.cohort_a)))) == 1
    assert len(rows_of(world.get(f"/organisations/{world.org_a}/export.csv", world.supervisor, mode="assessment"))) == 0


def test_a_cell_a_spreadsheet_would_run_as_a_formula_is_made_harmless(world):
    add_session(world, world.org_a, world.trainee, case="=HYPERLINK(1)", result="+cmd")
    row = rows_of(world.get(f"/organisations/{world.org_a}/export.csv", world.supervisor))[0]
    assert row["case_id"] == "'=HYPERLINK(1)" and row["result"] == "'+cmd"


def test_the_platform_export_covers_every_organisation_with_a_code_instead_of_the_name(world):
    from server.repositories import privacy
    world.api.seed(lambda s: [privacy.record_consent(s, u, "research", True) for u in (world.trainee, world.trainee_b)])   # the research export holds only people who agreed (T-PR4 is in test_privacy)
    add_session(world, world.org_a, world.trainee)
    add_session(world, world.org_b, world.trainee_b)
    r = world.get("/export.csv", world.platform)
    rows = rows_of(r)
    assert {row["organisation"] for row in rows} == {organisation_code(world.org_a), organisation_code(world.org_b)}
    assert "alpha" not in r.text and "beta" not in r.text and "Jane" not in r.text
    assert organisation_code(world.org_a) == organisation_code(world.org_a) != organisation_code(world.org_b)
    for who in (world.supervisor, world.admin, world.trainee):
        assert world.get("/export.csv", who).status_code == 403
    assert world.api.client.get("/export.csv").status_code == 401


def test_a_headset_token_cannot_open_the_overviews_or_the_export(world):
    from server.auth import device_tokens
    from server.repositories import devices
    def make(s: DbSession):
        return devices.issue(s, world.supervisor, "Headset", 15, 30).access_token
    token = world.api.seed(make)
    h = {"Authorization": "Bearer " + token}
    base = f"/organisations/{world.org_a}"
    assert world.api.client.get(f"{base}/overview", headers=h).status_code == 403
    assert world.api.client.get(f"{base}/export.csv", headers=h).status_code == 403
    assert world.api.client.get(f"{base}/progress", headers=h).status_code == 200   # a headset can show its own person's progress
    assert token.startswith(device_tokens.ACCESS_PREFIX)


# --- speed (EXP-BE-3 in miniature) ---------------------------------------------------------------------------------------------------------------------------------------------------

def test_the_dashboard_answers_within_a_second_on_a_year_of_sessions(world):
    users = [world.trainee, world.colleague]
    rows, items, behaviours = [], [], []
    for i in range(4000):
        sid = uuid.uuid4()
        rows.append(dict(id=sid, organisation_id=world.org_a, user_id=users[i % 2], case_id=f"VAR-S{1 + i % 2}-A", mode="practice", seed="1", content_version="v", engine_version="0.0.1",
                         log_hash="a" * 64, log_entries=3, ended_step=1, log_gz=b"x", status="verified" if i % 10 else "pending", received_at=NOW - dt.timedelta(hours=i * 2),
                         result="SUCCESS" if i % 3 else "FAIL", end_state="E", process_score=50 + i % 40))
        items.append(dict(session_id=sid, ordinal=0, item_id=f"critical.c{i % 15}", behaviour="b01", severity="critical", status="critical", points=-5, step=1, evidence=[1]))
        behaviours.extend(dict(session_id=sid, behaviour=f"b{j:02d}", credits=3, penalties=i % 4, net=3 - i % 4) for j in range(1, 8))
    world.api.seed(lambda s: (s.execute(insert(Session), rows), s.execute(insert(SessionScoreItem), items), s.execute(insert(SessionBehaviour), behaviours)))
    base = f"/organisations/{world.org_a}"
    for url, params in ((f"{base}/overview", {}), (f"{base}/overview", {"cohort_id": str(world.cohort_a)}), (f"{base}/progress", {}), (f"{base}/sessions", {"limit": 50})):
        started = time.perf_counter()
        response = world.get(url, world.supervisor if "progress" not in url else world.trainee, **params)
        assert response.status_code == 200 and time.perf_counter() - started < 1.0, (url, params)
    assert world.get(f"{base}/overview", world.supervisor).json()["sessions"] == 3600
    started = time.perf_counter()
    assert len(world.get(f"{base}/export.csv", world.supervisor).text.splitlines()) == 3601
    assert time.perf_counter() - started < 5.0
