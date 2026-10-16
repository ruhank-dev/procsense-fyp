"""EXP-BE-3 (p1-backend-s13, item 7): loads synthetic sessions across synthetic organisations, then measures the database size and every dashboard query.

    uv run --project server python -m server.scripts.load_synthetic_sessions --database-url postgresql://... --engine '["dotnet", "artifacts/bin/ProcSense.Cli/Release/net9.0/procsense-sim.dll"]'
        [--sessions 1000] [--organisations 5] [--trainees 20] [--weeks 26] [--out load_test.json]
    uv run --project server python -m server.scripts.load_synthetic_sessions --database-url postgresql://... --cleanup

How the sessions are made, so the sizes are real:
  1. the content is packaged for practice (tools/content/package_versions.py) and the real engine plays every P0 case along each scripted path of the golden suite (correct, novice, dangerous, edge);
  2. each session file is stored exactly as an upload stores it (sessionfile.parse_session_file, repositories.sessions.insert_session) and verified by the service's own worker with the real
     engine, so every template has its score items, behaviours, report and report page as a real one does;
  3. the verified templates are copied, with new ids, to the number asked for, spread over the trainees of every organisation and over the weeks (a copy keeps its template's header, so it is
     a stand-in for size and query load, not a session that would verify again).
Then it reports the database size, the size of each table, and the time of each dashboard query (repositories.dashboard) for every organisation: the median and the slowest of 5 runs.

Everything it creates is marked: organisations with the slug prefix "load-test-", people with the display name "Load test". --cleanup removes all of it and nothing else.
A database that is not on this computer is refused unless --allow-remote is given (the live database holds real trainees' data: ask the team first).
"""
from __future__ import annotations

import argparse
import datetime as dt
import gzip
import json
import random
import statistics
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path
from urllib.parse import urlparse

from sqlalchemy import delete, func, select, text
from sqlalchemy.orm import Session as DbSession

from server.config import _psycopg_url, get_settings
from server.database import make_engine
from server.models.accounts import Cohort, CohortMember, ConsentRecord, Membership, Organisation, Profile
from server.models.privacy import AccessLog
from server.models.sessions import LearnerProfile, Session, SessionBehaviour, SessionEvidence, SessionScoreItem
from server.repositories import accounts, dashboard
from server.repositories.sessions import insert_session
from server.sessionfile import parse_session_file
from server.verification import worker
from server.verification.runner import EngineRunner

ROOT = Path(__file__).resolve().parents[2]
PREFIX = "load-test-"
NAME = "Load test"
PATHS = ("correct", "novice", "dangerous", "edge")
RUNS = 5


def _local(url: str) -> bool:
    return (urlparse(url.replace("postgresql+psycopg", "postgresql")).hostname or "") in ("localhost", "127.0.0.1", "::1")


def _p0_cases() -> list[str]:
    return sorted(p.name[: -len("_correct.actions.json")] for p in (ROOT / "tests" / "golden").glob("*_correct.actions.json") if "-" in p.name and p.name[0].isupper())


def _content(out: Path) -> tuple[Path, str]:
    done = subprocess.run(["uv", "run", "--project", "tools", "tools/content/package_versions.py", "--out", str(out)], cwd=ROOT, capture_output=True, text=True)
    if done.returncode != 0:
        raise SystemExit(done.stdout + done.stderr)
    practice = [d for d in out.iterdir() if d.is_dir() and d.name.endswith("practice")]
    if len(practice) != 1:
        raise SystemExit(f"expected one practice package in {out}, found {[d.name for d in practice]}")
    return out, practice[0].name


def _play(engine: list[str], content_dir: Path, case: str, actions: Path, seed: str, tmp: Path) -> str | None:
    log = tmp / f"{case}-{actions.stem}-{seed}.session.jsonl"
    done = subprocess.run([*engine, "run", "--case", case, "--seed", seed, "--actions", str(actions), "--out", str(tmp / "result.json"), "--log", str(log), "--mode", "practice",
                           "--content", str(content_dir), "--session-id", str(uuid.uuid4())], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return log.read_text(encoding="utf-8") if done.returncode == 0 and log.exists() else None


def _person(db: DbSession, org_id: uuid.UUID, role: str) -> uuid.UUID:
    uid = uuid.uuid4()
    db.add(Profile(user_id=uid, pseudonym=accounts.new_pseudonym(), display_name=NAME))
    db.flush()
    accounts.add_membership(db, uid, org_id, role)
    return uid


def _organisations(db: DbSession, count: int, trainees: int) -> list[tuple[uuid.UUID, uuid.UUID, list[uuid.UUID]]]:
    """Each organisation with one administrator, one cohort holding all its trainees, and the trainees."""
    out = []
    for i in range(count):
        org = accounts.create_organisation(db, f"{NAME} hospital {i + 1}", f"{PREFIX}{i + 1}-{uuid.uuid4().hex[:6]}")
        _person(db, org.id, "org_admin")
        cohort = accounts.create_cohort(db, org.id, "Load test cohort")
        people = [_person(db, org.id, "trainee") for _ in range(trainees)]
        for p in people:
            accounts.set_cohort_member(db, cohort.id, p, "trainee")
        out.append((org.id, cohort.id, people))
    db.commit()
    return out


def _templates(db: DbSession, runner: EngineRunner, settings, engine: list[str], content_dir: Path, org_id: uuid.UUID, user_id: uuid.UUID) -> list[uuid.UUID]:
    """Plays and verifies one session per P0 case and scripted path; returns the ids of the verified ones."""
    made, verified = [], []
    with tempfile.TemporaryDirectory() as tmp:
        for case in _p0_cases():
            for path in PATHS:
                actions = ROOT / "tests" / "golden" / f"{case}_{path}.actions.json"
                if not actions.exists():
                    continue
                file_text = _play(engine, content_dir, case, actions, "42", Path(tmp))
                if file_text is None:
                    print(f"  the engine could not play {case} {path}")
                    continue
                parsed = parse_session_file(file_text)
                insert_session(db, parsed, org_id, user_id)
                db.commit()
                made.append(parsed.header.session_id)
    for sid in made:
        worker.verify_one(db, runner, settings, sid)
        row = db.get(Session, sid)
        if row.status == "verified":
            verified.append(sid)
        else:
            print(f"  template {row.case_id} was {row.status} ({row.reject_reason})")
    return verified


def _copy(db: DbSession, template: Session, items: list, behaviours: list, org_id: uuid.UUID, user_id: uuid.UUID, received: dt.datetime) -> None:
    sid = uuid.uuid4()
    skip = {"id", "organisation_id", "user_id", "received_at", "verified_at"}
    values = {c.key: getattr(template, c.key) for c in Session.__mapper__.column_attrs if c.key not in skip}
    db.add(Session(id=sid, organisation_id=org_id, user_id=user_id, received_at=received, verified_at=received + dt.timedelta(seconds=30), **values))
    db.flush()
    for it in items:
        db.add(SessionScoreItem(session_id=sid, ordinal=it.ordinal, item_id=it.item_id, behaviour=it.behaviour, severity=it.severity, status=it.status, points=it.points, step=it.step,
                                evidence=it.evidence))
    for b in behaviours:
        db.add(SessionBehaviour(session_id=sid, behaviour=b.behaviour, credits=b.credits, penalties=b.penalties, net=b.net))


def load(db: DbSession, runner: EngineRunner, settings, engine: list[str], content_root: Path, version: str, sessions: int, organisations: int, trainees: int, weeks: int) -> dict:
    orgs = _organisations(db, organisations, trainees)
    started = time.perf_counter()
    templates = _templates(db, runner, settings, engine, content_root / version, orgs[0][0], orgs[0][2][0])
    made_in = time.perf_counter() - started
    if not templates:
        raise SystemExit("no template verified: nothing to load")
    print(f"  {len(templates)} template sessions played and verified by the real engine in {made_in:.0f} s")
    rows = [(db.get(Session, t), list(db.scalars(select(SessionScoreItem).where(SessionScoreItem.session_id == t))),
             list(db.scalars(select(SessionBehaviour).where(SessionBehaviour.session_id == t)))) for t in templates]
    rnd = random.Random(42)
    now = dt.datetime.now(dt.timezone.utc)
    for n in range(sessions - len(templates)):
        org_id, _, people = orgs[n % organisations]
        template, items, behaviours = rows[n % len(rows)]
        _copy(db, template, items, behaviours, org_id, rnd.choice(people), now - dt.timedelta(minutes=rnd.randrange(weeks * 7 * 24 * 60)))
        if n % 100 == 99:
            db.commit()
    db.commit()
    return {"templates": len(templates), "template_seconds": round(made_in, 1), "organisations": [str(o[0]) for o in orgs]}


def _time(fn) -> dict:
    took = []
    for _ in range(RUNS):
        t = time.perf_counter()
        fn()
        took.append((time.perf_counter() - t) * 1000)
    return {"median_ms": round(statistics.median(took), 1), "max_ms": round(max(took), 1)}


def measure(db: DbSession) -> dict:
    sizes = {name: int(size) for name, size in db.execute(text(
        "select c.relname, pg_total_relation_size(c.oid) from pg_class c join pg_namespace n on n.oid = c.relnamespace "
        "where n.nspname = 'public' and c.relkind = 'r' order by 2 desc")).all()}
    database = int(db.scalar(text("select pg_database_size(current_database())")))
    orgs = list(db.scalars(select(Organisation).where(Organisation.slug.like(PREFIX + "%"))))
    queries: dict[str, list[dict]] = {}
    per_org = []
    for org in orgs:
        cohort = db.scalar(select(Cohort.id).where(Cohort.organisation_id == org.id))
        trainee = db.scalar(select(CohortMember.user_id).where(CohortMember.cohort_id == cohort))
        some = db.scalar(select(Session.id).where(Session.organisation_id == org.id).order_by(Session.received_at.desc()).limit(1))
        none, mine, theirs = dashboard.Filters(), dashboard.Filters(user_id=trainee), dashboard.Filters(cohort_id=cohort)
        per_org.append(db.scalar(select(func.count()).select_from(Session).where(Session.organisation_id == org.id)))
        checks = {
            "summary (organisation)": lambda: dashboard.summary(db, org.id, none),
            "summary (cohort)": lambda: dashboard.summary(db, org.id, theirs),
            "summary (one trainee)": lambda: dashboard.summary(db, org.id, mine),
            "list_sessions (first page of 50)": lambda: dashboard.list_sessions(db, org.id, none, "verified", 50, 0, False),
            "list_sessions (last page of 50)": lambda: dashboard.list_sessions(db, org.id, none, "verified", 50, 150, False),
            "series (one trainee)": lambda: dashboard.series(db, org.id, mine),
            "report_row": lambda: dashboard.report_row(db, org.id, some),
            "export_rows (whole organisation)": lambda: list(dashboard.export_rows(db, org.id, none)),
        }
        for name, fn in checks.items():
            queries.setdefault(name, []).append(_time(fn))
        db.rollback()
    platform = _time(lambda: list(dashboard.export_rows(db, None, dashboard.Filters())))
    total = db.scalar(select(func.count()).select_from(Session).join(Organisation, Organisation.id == Session.organisation_id).where(Organisation.slug.like(PREFIX + "%")))
    session_bytes = sizes.get("session", 0) + sizes.get("session_score_item", 0) + sizes.get("session_behaviour", 0)
    worst = {name: max(r["max_ms"] for r in runs) for name, runs in queries.items()}
    return {"database_bytes": database, "database_mb": round(database / 1e6, 1), "table_bytes": sizes, "load_test_sessions": total, "sessions_per_organisation": per_org,
            "bytes_per_session": round(session_bytes / total) if total else None,
            "sessions_that_fit_in_400_mb": int((400e6 - (database - session_bytes)) / (session_bytes / total)) if total else None,
            "queries_slowest_ms_over_organisations": worst, "export_rows (platform, every organisation)": platform, "queries": queries}


def cleanup(db: DbSession) -> dict:
    orgs = list(db.scalars(select(Organisation.id).where(Organisation.slug.like(PREFIX + "%"))))
    people = list(db.scalars(select(Profile.user_id).where(Profile.display_name == NAME)))
    ids = select(Session.id).where(Session.organisation_id.in_(orgs))
    for model in (SessionScoreItem, SessionBehaviour, SessionEvidence):
        db.execute(delete(model).where(model.session_id.in_(ids)))
    gone = db.execute(delete(Session).where(Session.organisation_id.in_(orgs))).rowcount
    db.execute(delete(AccessLog).where(AccessLog.organisation_id.in_(orgs)))
    db.execute(delete(LearnerProfile).where(LearnerProfile.organisation_id.in_(orgs)))
    db.execute(delete(CohortMember).where(CohortMember.cohort_id.in_(select(Cohort.id).where(Cohort.organisation_id.in_(orgs)))))
    db.execute(delete(Cohort).where(Cohort.organisation_id.in_(orgs)))
    db.execute(delete(Membership).where(Membership.organisation_id.in_(orgs)))
    db.execute(delete(ConsentRecord).where(ConsentRecord.user_id.in_(people)))
    db.execute(delete(Profile).where(Profile.user_id.in_(people)))
    db.execute(delete(Organisation).where(Organisation.id.in_(orgs)))
    db.commit()
    return {"organisations": len(orgs), "people": len(people), "sessions": gone}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--database-url", default="", help="default: DATABASE_URL")
    parser.add_argument("--engine", default="", help='the engine command as a JSON list, e.g. ["dotnet", "artifacts/bin/ProcSense.Cli/Release/net9.0/procsense-sim.dll"]')
    parser.add_argument("--sessions", type=int, default=1000)
    parser.add_argument("--organisations", type=int, default=5)
    parser.add_argument("--trainees", type=int, default=20, help="per organisation")
    parser.add_argument("--weeks", type=int, default=26, help="the sessions are spread over this many weeks before now")
    parser.add_argument("--out", type=Path, default=None, help="also write the measurements here as JSON")
    parser.add_argument("--measure-only", action="store_true", help="measure what is already loaded")
    parser.add_argument("--cleanup", action="store_true", help="remove everything a load test created, and nothing else")
    parser.add_argument("--allow-remote", action="store_true", help="allow a database that is not on this computer")
    args = parser.parse_args(argv)
    sys.stdout.reconfigure(encoding="utf-8")
    settings = get_settings()
    url = _psycopg_url(args.database_url, "--database-url") if args.database_url else settings.database_url.get_secret_value()
    if not url:
        raise SystemExit("no database: pass --database-url or set DATABASE_URL")
    if not _local(url) and not args.allow_remote:
        raise SystemExit("this database is not on this computer: pass --allow-remote only when the team has agreed to load synthetic data into it")
    eng = make_engine(url, pool_size=2)
    with DbSession(eng) as db:
        if args.cleanup:
            print("removed: " + json.dumps(cleanup(db)))
            return 0
        report: dict = {"date": dt.date.today().isoformat()}
        if not args.measure_only:
            if not args.engine:
                raise SystemExit("pass --engine (the engine plays and verifies the template sessions)")
            if db.scalar(select(func.count()).select_from(Organisation).where(Organisation.slug.like(PREFIX + "%"))):
                raise SystemExit("a load test is already loaded here: run with --cleanup first, or --measure-only")
            # the verifier runs the engine from a temporary folder, so a path in the command is made absolute
            engine = [str((ROOT / a).resolve()) if (ROOT / a).exists() else a for a in json.loads(args.engine)]
            with tempfile.TemporaryDirectory() as tmp:
                content_root, version = _content(Path(tmp) / "content")
                runner = EngineRunner(engine, str(content_root), 120)
                report["load"] = load(db, runner, settings, engine, content_root, version, args.sessions, args.organisations, args.trainees, args.weeks)
            db.execute(text("analyze"))
            db.commit()
        report["measure"] = measure(db)
    m = report["measure"]
    print(f"database: {m['database_mb']} MB with {m['load_test_sessions']} load-test sessions ({m['bytes_per_session']} bytes each with its score rows; about {m['sessions_that_fit_in_400_mb']} fit in 400 MB)")
    for name, ms in m["queries_slowest_ms_over_organisations"].items():
        print(f"  {name}: slowest {ms} ms")
    print(f"  export_rows (platform): median {m['export_rows (platform, every organisation)']['median_ms']} ms")
    if args.out:
        args.out.write_text(json.dumps(report, indent=1, default=str) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
