"""The AI audit logger (p1-backend-s05) on a real PostgreSQL, through stand-in AI routes (the real ones come with p1-backend-s06).

T-AU1 completeness: every AI request writes exactly one row, whatever happened to it (an answer, a validator failure, a fallback, an error, a crash, a bad request), and every AI route starts a record.
T-AU2 privacy: no text is stored, only hashes, references and codes.
T-AU3 performance: the logger adds under 5 ms to a request, and a slow database does not slow the reply.
Then the weekly report (JSON and page, from real rows), who may see what, and the privacy duties (export, deletion, retention).
"""
from __future__ import annotations

import datetime as dt
import hashlib
import statistics
import time
import uuid

import pytest
from fastapi import APIRouter, Depends, FastAPI, HTTPException
from fastapi.testclient import TestClient
from pydantic import BaseModel
from sqlalchemy import func, insert, select
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import OrgContext, require_member
from server.jobs import retention
from server.main import create_app
from server.middleware import audit
from server.middleware.audit import AiCallRecord, AuditWriter, audited
from server.models.ai_call import AiCall
from server.models.privacy import AccessLog
from server.repositories import accounts
from server.repositories import privacy as privacy_repository
from server.routers.ai_audit import render_weekly

pytestmark = pytest.mark.db
SENTENCE = "My chest hurts when I breathe in and I am allergic to penicillin"


class Utterance(BaseModel):
    text: str
    outcome: str = "pass"
    session_id: uuid.UUID | None = None


def stand_in_router() -> APIRouter:
    """What a gateway route of p1-backend-s06 will look like, with every outcome a route can have."""
    router = APIRouter()

    @router.post("/classify/organisations/{organisation_id}")
    def classify(body: Utterance, call: AiCallRecord = Depends(audited("classify"))) -> dict:
        call.session_id = body.session_id
        call.set_model("qwen2.5-7b-instruct", "q4_K_M")
        call.set_prompt("classify.v3", context_id="VAR-S1-A")
        call.set_input(body.text)
        if body.outcome == "crash":
            raise RuntimeError("the model host broke")
        if body.outcome == "unavailable":
            raise HTTPException(status_code=503, detail="model_unavailable")
        if body.outcome == "fail":
            call.validated(False, ["UNSUPPORTED_CLAIM", body.text])        # the second "reason" is a sentence: it must not be stored
            call.fell_back("VALIDATION_FAILED")
            call.set_output(ref="line:PAT-VAR-S1-A-ALLERGIES")
            return {"line": "fallback"}
        call.validated(True)
        call.set_output(text="It hurts on the left side.")
        return {"line": "ok"}

    @router.post("/dialogue/organisations/{organisation_id}/patient")
    def patient(body: Utterance, call: AiCallRecord = Depends(audited("dialogue_patient"))) -> dict:
        call.set_input(body.text)
        call.validation_skipped()
        return {"line": "ok"}

    @router.post("/not-ai/organisations/{organisation_id}")
    def plain(body: Utterance, ctx: OrgContext = Depends(require_member)) -> dict:     # the same sign-in as an AI route, without the audit: the yardstick for T-AU3
        return {"line": "ok"}

    return router


@pytest.fixture
def world(api):
    api.client.app.include_router(stand_in_router())

    def build(s: DbSession):
        a, b = accounts.create_organisation(s, "Alpha", "alpha-org"), accounts.create_organisation(s, "Beta", "beta-org")
        people = {n: api.person(s) for n in ("trainee", "colleague", "supervisor", "trainee_b")}
        platform = api.person(s, platform_admin=True)
        for name, org, role in (("trainee", a, "trainee"), ("colleague", a, "trainee"), ("supervisor", a, "supervisor"), ("trainee_b", b, "trainee")):
            accounts.add_membership(s, people[name], org.id, role)
        return a.id, b.id, people, platform
    a, b, people, platform = api.seed(build)

    class W:
        pass
    w = W()
    w.api, w.a, w.b, w.p, w.platform = api, a, b, people, platform

    def call(path, who, org=None, **body):
        return api.client.post(path.format(org=org or a), json={"text": SENTENCE, **body}, headers=api.headers(people[who] if isinstance(who, str) else who))
    w.call = call
    return w


def rows(api) -> list[AiCall]:
    assert audit.get_writer().flush(10)
    with DbSession(api.engine) as s:
        return list(s.scalars(select(AiCall).order_by(AiCall.at)))


# ---------------------------------------------------------------- T-AU1 completeness

def test_T_AU1_every_ai_request_writes_exactly_one_row_whatever_happened(world):
    sid = uuid.uuid4()
    ok = world.call("/classify/organisations/{org}", "trainee", session_id=str(sid))
    failed = world.call("/classify/organisations/{org}", "trainee", outcome="fail")
    unavailable = world.call("/classify/organisations/{org}", "trainee", outcome="unavailable")
    bad = world.api.client.post(f"/classify/organisations/{world.a}", json={"no_text": 1}, headers=world.api.headers(world.p["trainee"]))
    skipped = world.call("/dialogue/organisations/{org}/patient", "trainee")
    with TestClient(world.api.client.app, raise_server_exceptions=False) as crashing:
        crashed = crashing.post(f"/classify/organisations/{world.a}", json={"text": SENTENCE, "outcome": "crash"}, headers=world.api.headers(world.p["trainee"]))
    assert [r.status_code for r in (ok, failed, unavailable, bad, skipped, crashed)] == [200, 200, 503, 422, 200, 500]

    by_id = {r.id: r for r in rows(world.api)}
    assert len(by_id) == 6
    assert all(r.organisation_id == world.a and r.user_id == world.p["trainee"] for r in by_id.values())
    for reply in (ok, failed, unavailable, bad, skipped):
        assert by_id[uuid.UUID(reply.headers["x-ai-call-id"])].status_code == reply.status_code   # the id the headset mirrors into its own log
    # a crash is answered by the framework's outermost error handler, outside the audit, so that reply carries no id; its row is still written
    assert "x-ai-call-id" not in crashed.headers
    answered = {uuid.UUID(r.headers["x-ai-call-id"]) for r in (ok, failed, unavailable, bad, skipped)}
    (crash_row,) = [r for i, r in by_id.items() if i not in answered]
    first = by_id[uuid.UUID(ok.headers["x-ai-call-id"])]
    assert (first.task, first.route, first.session_id, first.model, first.model_version, first.prompt_id, first.context_id, first.validation, first.fallback) == \
        ("classify", f"/classify/organisations/{world.a}", sid, "qwen2.5-7b-instruct", "q4_K_M", "classify.v3", "VAR-S1-A", "passed", False)
    fail = by_id[uuid.UUID(failed.headers["x-ai-call-id"])]
    assert (fail.validation, fail.reasons, fail.fallback, fail.fallback_reason, fail.output_ref) == ("failed", ["UNSUPPORTED_CLAIM", "UNCODED_REASON"], True, "VALIDATION_FAILED",
                                                                                                    "line:PAT-VAR-S1-A-ALLERGIES")
    assert by_id[uuid.UUID(unavailable.headers["x-ai-call-id"])].validation == "error"        # a server-side failure the route did not judge
    assert by_id[uuid.UUID(bad.headers["x-ai-call-id"])].validation == "not_reported"
    assert (crash_row.status_code, crash_row.validation) == (500, "error")
    assert by_id[uuid.UUID(skipped.headers["x-ai-call-id"])].validation == "skipped"


def test_T_AU1_a_request_that_is_not_signed_in_or_not_a_member_starts_no_record(world):
    assert world.api.client.post(f"/classify/organisations/{world.a}", json={"text": "x"}).status_code == 401
    assert world.call("/classify/organisations/{org}", "trainee_b").status_code == 404     # not a member of Alpha
    assert rows(world.api) == []


def test_T_AU1_every_ai_route_of_the_service_starts_a_record_and_the_check_finds_one_that_does_not(world):
    app = create_app()
    paths = [p for p, _, _ in audit.api_routes(app.routes)]
    assert "/organisations/{organisation_id}/sessions" in paths and "/organisations/{organisation_id}/ai-calls" in paths   # the check walks the included routers
    assert audit.unaudited_ai_routes(app) == []
    assert audit.unaudited_ai_routes(world.api.client.app) == []     # the stand-in AI routes are audited

    careless = FastAPI()
    router = APIRouter()

    @router.post("/llm/generate")
    def generate() -> dict:
        return {}
    careless.include_router(router)
    careless.include_router(APIRouter(dependencies=[Depends(audited("debrief"))]))
    assert audit.unaudited_ai_routes(careless) == ["POST /llm/generate"]


# ---------------------------------------------------------------- T-AU2 no raw text

def test_T_AU2_no_text_is_stored_only_hashes_references_and_codes(world):
    world.call("/classify/organisations/{org}", "trainee")
    world.call("/classify/organisations/{org}", "trainee", outcome="fail")
    stored = rows(world.api)
    assert stored[0].input_hash == hashlib.sha256(SENTENCE.encode("utf-8")).hexdigest()
    assert stored[0].output_ref == "sha256:" + hashlib.sha256(b"It hurts on the left side.").hexdigest()
    with DbSession(world.api.engine) as s:
        everything = " ".join(str(v) for r in s.execute(select(AiCall.__table__)).all() for v in r)
    for word in ("chest", "breathe", "penicillin", "hurts", "left side"):
        assert word not in everything


def test_T_AU2_a_name_that_looks_like_text_is_hashed_and_a_reason_that_is_not_a_code_is_replaced():
    r = AiCallRecord(organisation_id=uuid.uuid4(), user_id=uuid.uuid4(), task="debrief", route="/debrief")
    r.set_model("meta-llama/Llama-3.1-8B", "Q4_K_M")
    r.set_prompt("You are a patient with chest pain", context_id="ctx 1")
    r.validated(False, ["CONTRADICTS_CASE", "the patient said she has no allergies", "lower_case"])
    r.fell_back("model was slow")
    assert r.model == "meta-llama/Llama-3.1-8B"
    assert r.prompt_id.startswith("sha256:") and len(r.prompt_id) <= 64 and r.context_id.startswith("sha256:")
    assert r.reasons == ["CONTRADICTS_CASE", "UNCODED_REASON", "UNCODED_REASON"] and r.fallback_reason == "UNCODED_REASON"


def test_T_AU2_the_table_has_no_column_that_could_hold_a_sentence():
    widths = {c.name: getattr(c.type, "length", None) for c in AiCall.__table__.columns if c.type.python_type is str}
    assert all(w is not None and w <= 80 for w in widths.values()), widths


# ---------------------------------------------------------------- T-AU3 overhead

def _median_ms(client, path: str, headers: dict, n: int) -> float:
    took = []
    for _ in range(n):
        t = time.perf_counter()
        assert client.post(path, json={"text": SENTENCE, "outcome": "pass"}, headers=headers).status_code == 200
        took.append((time.perf_counter() - t) * 1000)
    return statistics.median(took)


def test_T_AU3_the_logger_adds_under_5_ms_and_a_slow_database_does_not_slow_the_reply(world, monkeypatch):
    headers = world.api.headers(world.p["trainee"])
    audited_path, plain_path = f"/dialogue/organisations/{world.a}/patient", f"/not-ai/organisations/{world.a}"
    _median_ms(world.api.client, audited_path, headers, 10)    # warm up
    _median_ms(world.api.client, plain_path, headers, 10)
    overhead = _median_ms(world.api.client, audited_path, headers, 100) - _median_ms(world.api.client, plain_path, headers, 100)
    print(f"T-AU3: the audit added {overhead:.2f} ms (median of 100)")
    assert overhead < 5, f"the audit added {overhead:.2f} ms"

    def slow_engine():
        time.sleep(1.0)
        return world.api.engine
    writer = audit.get_writer()
    assert writer.flush(10)
    before = writer.written
    monkeypatch.setattr(writer, "_engine_factory", slow_engine)
    t = time.perf_counter()
    assert world.call("/dialogue/organisations/{org}/patient", "trainee").status_code == 200
    assert (time.perf_counter() - t) < 0.5        # the reply did not wait for the slow write
    assert writer.flush(10) and writer.written == before + 1


def test_a_full_queue_drops_the_row_and_says_so_and_a_failing_database_is_retried_then_counted():
    class Broken:
        def begin(self):
            raise RuntimeError("database down")
    w = AuditWriter(1, engine_factory=lambda: Broken())
    w._start = lambda: None                      # no thread: the queue stays full
    row = AiCallRecord(organisation_id=uuid.uuid4(), user_id=uuid.uuid4(), task="tts", route="/tts").row(200, 3)
    assert w.submit(row) and not w.submit(row) and w.dropped == 1
    w._write([row])
    assert w.failed == 1 and w.written == 0


# ---------------------------------------------------------------- the weekly report

def _seed_week(world, monday: dt.datetime) -> None:
    def make(task, org, who, latency, validation="passed", reasons=(), fallback=False, fallback_reason="", status=200, days=0):
        return dict(id=uuid.uuid4(), organisation_id=org, user_id=world.p[who], task=task, route="/" + task, validation=validation, reasons=list(reasons), fallback=fallback,
                    fallback_reason=fallback_reason, status_code=status, latency_ms=latency, at=monday + dt.timedelta(days=days, hours=9), model="m", model_version="1", prompt_id="p",
                    context_id="", input_hash="", output_ref="")
    data = [make("classify", world.a, "trainee", ms) for ms in range(10, 110, 10)]              # 10 calls, 10..100 ms
    data += [make("classify", world.a, "colleague", 400, "failed", ["UNSUPPORTED_CLAIM"], True, "VALIDATION_FAILED"),
             make("classify", world.a, "colleague", 900, "failed", ["UNSUPPORTED_CLAIM", "CONTRADICTS_CASE"], True, "VALIDATION_FAILED", days=6),
             make("debrief", world.a, "trainee", 2500, "not_reported", (), True, "MODEL_TIMEOUT", 504, days=2),
             make("classify", world.b, "trainee_b", 50),
             make("classify", world.a, "trainee", 50, days=7)]                                    # next week: not counted
    world.api.seed(lambda s: s.execute(insert(AiCall), data))


def test_the_weekly_report_counts_fallbacks_reasons_and_latency_percentiles_for_one_organisation(world):
    monday = dt.datetime(2026, 10, 12, tzinfo=dt.timezone.utc)
    _seed_week(world, monday)
    r = world.api.client.get(f"/organisations/{world.a}/ai-audit/weekly", params={"week": "2026-10-15"}, headers=world.api.headers(world.p["supervisor"]))
    assert r.status_code == 200, r.text
    rep = r.json()
    assert (rep["week_start"], rep["week_end"], rep["calls"], rep["people"], rep["organisations"], rep["fallbacks"]) == ("2026-10-12", "2026-10-18", 13, 2, 1, 3)
    classify, debrief = rep["tasks"]
    assert (classify["task"], classify["calls"], classify["fallbacks"], classify["fallback_rate"], classify["server_errors"]) == ("classify", 12, 2, round(2 / 12, 4), 0)
    assert classify["validation"] == {"passed": 10, "failed": 2, "skipped": 0, "error": 0, "not_reported": 0}
    assert classify["latency_ms"]["p50"] == 65.0 and classify["latency_ms"]["max"] == 900 and classify["latency_ms"]["p95"] > 400
    assert (debrief["server_errors"], debrief["fallback_rate"]) == (1, 1.0)
    assert rep["failure_reasons"] == [{"task": "classify", "reason": "UNSUPPORTED_CLAIM", "count": 2}, {"task": "classify", "reason": "CONTRADICTS_CASE", "count": 1}]
    assert rep["fallback_reasons"] == [{"task": "classify", "reason": "VALIDATION_FAILED", "count": 2}, {"task": "debrief", "reason": "MODEL_TIMEOUT", "count": 1}]

    page = world.api.client.get(f"/organisations/{world.a}/ai-audit/weekly.html", params={"week": "2026-10-12"}, headers=world.api.headers(world.p["supervisor"]))
    assert page.status_code == 200 and page.headers["content-type"].startswith("text/html")
    assert "AI audit: Alpha" in page.text and "UNSUPPORTED_CLAIM" in page.text and "MODEL_TIMEOUT" in page.text and "<script" not in page.text
    assert "frame-ancestors 'none'" in page.headers["content-security-policy"]


def test_the_platform_report_covers_every_organisation_and_only_the_platform_administrator_sees_it(world):
    _seed_week(world, dt.datetime(2026, 10, 12, tzinfo=dt.timezone.utc))
    r = world.api.client.get("/ai-audit/weekly", params={"week": "2026-10-12"}, headers=world.api.headers(world.platform))
    assert r.status_code == 200 and (r.json()["calls"], r.json()["organisations"]) == (14, 2)
    assert world.api.client.get("/ai-audit/weekly.html", params={"week": "2026-10-12"}, headers=world.api.headers(world.platform)).status_code == 200
    assert world.api.client.get("/ai-audit/weekly", headers=world.api.headers(world.p["supervisor"])).status_code == 403
    assert world.api.client.get(f"/organisations/{world.a}/ai-audit/weekly", headers=world.api.headers(world.p["trainee"])).status_code == 403
    assert world.api.client.get(f"/organisations/{world.b}/ai-audit/weekly", headers=world.api.headers(world.p["supervisor"])).status_code == 404


def test_the_weekly_page_escapes_what_it_shows_and_says_when_a_week_is_empty():
    report = dict(week_start=dt.date(2026, 10, 12), week_end=dt.date(2026, 10, 18), calls=0, people=0, organisations=0, fallbacks=0, fallback_rate=None, tasks=[], failure_reasons=[],
                  fallback_reasons=[])
    page = render_weekly(report, "<b>Alpha</b>")
    assert "&lt;b&gt;Alpha&lt;/b&gt;" in page and "<b>Alpha" not in page and page.count("none this week") == 3


# ---------------------------------------------------------------- who sees which calls

def test_a_trainee_sees_only_their_own_calls_and_a_supervisor_looking_at_one_person_is_logged(world):
    world.call("/classify/organisations/{org}", "trainee")
    world.call("/classify/organisations/{org}", "colleague")
    rows(world.api)
    mine = world.api.client.get(f"/organisations/{world.a}/ai-calls", params={"trainee_id": str(world.p["colleague"])}, headers=world.api.headers(world.p["trainee"])).json()
    assert mine["total"] == 1 and mine["items"][0]["input_hash"] == hashlib.sha256(SENTENCE.encode()).hexdigest()
    all_calls = world.api.client.get(f"/organisations/{world.a}/ai-calls", headers=world.api.headers(world.p["supervisor"])).json()
    assert all_calls["total"] == 2
    one = world.api.client.get(f"/organisations/{world.a}/ai-calls", params={"trainee_id": str(world.p["colleague"])}, headers=world.api.headers(world.p["supervisor"])).json()
    assert one["total"] == 1
    with DbSession(world.api.engine) as s:
        assert s.scalars(select(AccessLog.action).where(AccessLog.subject_id == world.p["colleague"])).all() == ["view_ai_calls"]
    assert world.api.client.get(f"/organisations/{world.b}/ai-calls", headers=world.api.headers(world.p["supervisor"])).status_code == 404


# ---------------------------------------------------------------- privacy

def test_the_calls_go_with_the_data_export_move_to_the_stand_in_on_deletion_and_go_after_the_retention_period(world):
    world.call("/classify/organisations/{org}", "trainee")
    world.call("/classify/organisations/{org}", "colleague")
    rows(world.api)
    exported = world.api.client.get("/privacy/export", headers=world.api.headers(world.p["trainee"]))
    assert exported.status_code == 200 and len(exported.json()["ai_calls"]) == 1

    with DbSession(world.api.engine) as s:
        stand_in = privacy_repository.delete_person(s, world.p["trainee"])
        s.commit()
        assert s.scalar(select(func.count()).select_from(AiCall).where(AiCall.user_id == world.p["trainee"])) == 0
        assert s.scalar(select(func.count()).select_from(AiCall).where(AiCall.user_id == stand_in)) == 1

        s.execute(AiCall.__table__.update().where(AiCall.user_id == world.p["colleague"]).values(at=dt.datetime(2025, 1, 1, tzinfo=dt.timezone.utc)))
        s.commit()
        assert retention.run(s)["ai_calls_deleted"] == 1
        assert s.scalar(select(func.count()).select_from(AiCall)) == 1
