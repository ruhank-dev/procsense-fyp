"""Consent, the access log, export, deletion and retention (p1-backend-s12). ORM only.

Deleting a person keeps their sessions: the sessions are moved to a new empty stand-in profile (a fresh random id, a fresh pseudonym, no name, no email, no link to the sign-in account), so every total in
every organisation stays exactly as it was and only the identity is gone.
"""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import delete, func, select, update
from sqlalchemy.orm import Session as DbSession

from server import privacy
from server.models.accounts import Cohort, CohortMember, ConsentRecord, Invitation, Membership, Organisation, Profile
from server.models.device_session import DeviceLogin, DeviceSession
from server.models.privacy import AccessLog
from server.models.sessions import LearnerProfile, Session, SessionBehaviour, SessionScoreItem
from server.repositories import accounts


def _now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


# ---------------------------------------------------------------- consent

def latest_consents(db: DbSession, user_id: uuid.UUID) -> dict[str, ConsentRecord]:
    """The person's most recent decision for each kind."""
    rows = db.scalars(select(ConsentRecord).where(ConsentRecord.user_id == user_id).distinct(ConsentRecord.kind).order_by(ConsentRecord.kind, ConsentRecord.recorded_at.desc(), ConsentRecord.id))
    return {r.kind: r for r in rows}


def has_current(db: DbSession, user_id: uuid.UUID, kind: str) -> bool:
    row = latest_consents(db, user_id).get(kind)
    return row is not None and row.granted and row.text_version == privacy.CONSENT_VERSION


def record_consent(db: DbSession, user_id: uuid.UUID, kind: str, granted: bool) -> ConsentRecord:
    """Adds an answer. Its time is always later than the person's previous answer to the same question, so "the latest answer" is never a tie, even for two answers in the same clock tick."""
    when = _now()
    last = db.scalar(select(func.max(ConsentRecord.recorded_at)).where(ConsentRecord.user_id == user_id, ConsentRecord.kind == kind))
    if last is not None and when <= last:
        when = last + dt.timedelta(microseconds=1)
    row = ConsentRecord(user_id=user_id, kind=kind, text_version=privacy.CONSENT_VERSION, granted=granted, recorded_at=when)
    db.add(row)
    db.flush()
    return row


def research_consenting_users():
    """A sub-query of the people whose latest decision on research use is yes (any version of the text they agreed to is accepted until they say no)."""
    latest = select(ConsentRecord.user_id, ConsentRecord.granted).where(ConsentRecord.kind == "research").distinct(ConsentRecord.user_id).order_by(ConsentRecord.user_id, ConsentRecord.recorded_at.desc(), ConsentRecord.id).subquery()
    return select(latest.c.user_id).where(latest.c.granted.is_(True))


# ---------------------------------------------------------------- access log

def log_access(db: DbSession, *, organisation_id: uuid.UUID, viewer: Profile, viewer_role: str, subject_id: uuid.UUID | None, action: str, session_id: uuid.UUID | None = None, detail: str = "") -> None:
    """One row for one look (the same look again within five minutes is the same row). A person looking at their own data is not a look worth recording."""
    if subject_id is not None and subject_id == viewer.user_id:
        return
    recent = select(AccessLog.id).where(AccessLog.viewer_id == viewer.user_id, AccessLog.subject_id == subject_id if subject_id is not None else AccessLog.subject_id.is_(None), AccessLog.action == action,
                                        AccessLog.session_id == session_id if session_id is not None else AccessLog.session_id.is_(None), AccessLog.at > _now() - dt.timedelta(minutes=5)).limit(1)
    if db.scalar(recent) is not None:
        return   # one look is one row: a page that asks for the same thing twice (the report and its page) is not two looks
    db.add(AccessLog(organisation_id=organisation_id, viewer_id=viewer.user_id, viewer_pseudonym=viewer.pseudonym, viewer_role=viewer_role, subject_id=subject_id, action=action, session_id=session_id, detail=detail[:200]))


def access_log_about(db: DbSession, subject_id: uuid.UUID, limit: int = 200) -> list[dict]:
    rows = db.execute(select(AccessLog, Organisation.name, Profile.display_name).join(Organisation, Organisation.id == AccessLog.organisation_id)
                      .outerjoin(Profile, Profile.user_id == AccessLog.viewer_id).where(AccessLog.subject_id == subject_id).order_by(AccessLog.at.desc()).limit(limit)).all()
    return [dict(at=a.at, organisation=org, viewer=(name or a.viewer_pseudonym), viewer_pseudonym=a.viewer_pseudonym, viewer_role=a.viewer_role, action=a.action, session_id=a.session_id) for a, org, name in rows]


# ---------------------------------------------------------------- export

def export_person(db: DbSession, profile: Profile) -> dict:
    """Everything held about one person, in a form they can keep. The stored session logs are left out (they are large and are the person's own recorded actions: available on request, DT-153); the results and reports are in."""
    uid = profile.user_id
    memberships = [dict(organisation=o.name, role=m.role, status=m.status, since=m.created_at) for m, o in accounts.memberships_of(db, uid)]
    cohorts = [dict(cohort=c.name, role=cm.role) for cm, c in db.execute(select(CohortMember, Cohort).join(Cohort, Cohort.id == CohortMember.cohort_id).where(CohortMember.user_id == uid))]
    consents = [dict(kind=c.kind, granted=c.granted, text_version=c.text_version, at=c.recorded_at) for c in db.scalars(select(ConsentRecord).where(ConsentRecord.user_id == uid).order_by(ConsentRecord.recorded_at))]
    sessions = []
    for s in db.scalars(select(Session).where(Session.user_id == uid).order_by(Session.received_at)):
        items = [dict(item=i.item_id, behaviour=i.behaviour, severity=i.severity, status=i.status, points=float(i.points), step=i.step, evidence=i.evidence)
                 for i in db.scalars(select(SessionScoreItem).where(SessionScoreItem.session_id == s.id).order_by(SessionScoreItem.ordinal))]
        behaviours = [dict(behaviour=b.behaviour, credits=b.credits, penalties=b.penalties, net=b.net) for b in db.scalars(select(SessionBehaviour).where(SessionBehaviour.session_id == s.id))]
        sessions.append(dict(session_id=s.id, organisation=db.scalar(select(Organisation.name).where(Organisation.id == s.organisation_id)), case=s.case_id, mode=s.mode, status=s.status, received_at=s.received_at,
                             verified_at=s.verified_at, result=s.result, end_state=s.end_state, process_score=float(s.process_score) if s.process_score is not None else None, content_version=s.content_version,
                             engine_version=s.engine_version, score_items=items, behaviours=behaviours, report=s.report_json, log_removed_at=s.log_purged_at))
    devices = [dict(name=d.device_name, signed_in_at=d.created_at, last_used_at=d.last_used_at, revoked_at=d.revoked_at) for d in db.scalars(select(DeviceSession).where(DeviceSession.user_id == uid))]
    return dict(exported_at=_now(), notice="Everything ProcSense holds about you. Your email address is held by the sign-in service and is not part of this file.",
                profile=dict(pseudonym=profile.pseudonym, display_name=profile.display_name, created_at=profile.created_at), memberships=memberships, cohorts=cohorts, consents=consents, sessions=sessions,
                devices=devices, who_looked_at_my_data=access_log_about(db, uid, 1000))


# ---------------------------------------------------------------- deletion

class LastAdministrator(Exception):
    def __init__(self, organisation: str):
        super().__init__(organisation)
        self.organisation = organisation


def delete_person(db: DbSession, user_id: uuid.UUID) -> uuid.UUID:
    """Cuts every link between the person and what they leave behind (all in the caller's transaction; the caller commits after the sign-in account is deleted too). Returns the stand-in's id."""
    for m, o in accounts.memberships_of(db, user_id):
        if m.role == "org_admin" and m.status == "active" and accounts.active_admin_count(db, o.id) <= 1:
            raise LastAdministrator(o.name)
    stand_in = Profile(user_id=uuid.uuid4(), pseudonym=accounts.new_pseudonym(), display_name="", deleted_at=_now())
    db.add(stand_in)
    db.flush()
    db.execute(update(Session).where(Session.user_id == user_id).values(user_id=stand_in.user_id))
    db.execute(update(Invitation).where(Invitation.invited_by == user_id).values(invited_by=None))
    db.execute(update(AccessLog).where(AccessLog.viewer_id == user_id).values(viewer_id=None, viewer_pseudonym="deleted"))
    db.execute(delete(AccessLog).where(AccessLog.subject_id == user_id))
    for table in (Membership, CohortMember, ConsentRecord, DeviceSession, DeviceLogin, LearnerProfile):
        db.execute(delete(table).where(table.user_id == user_id))
    db.execute(delete(Profile).where(Profile.user_id == user_id))
    db.flush()
    return stand_in.user_id


# ---------------------------------------------------------------- retention

def purge_sessions(db: DbSession, default_months: int, now: dt.datetime | None = None) -> int:
    """Removes the stored log and report page of sessions older than their organisation's retention period; the results, score items and behaviours stay so totals do not change. Returns how many sessions were purged."""
    now = now or _now()
    count = 0
    for org_id, months in db.execute(select(Organisation.id, Organisation.retention_months)).all():
        limit = now - dt.timedelta(days=30 * (months or default_months))
        rows = db.execute(update(Session).where(Session.organisation_id == org_id, Session.received_at < limit, Session.log_purged_at.is_(None))
                          .values(log_gz=b"", report_html_gz=None, header_line="", log_purged_at=now).returning(Session.id)).all()
        count += len(rows)
    return count


def purge_access_log(db: DbSession, months: int, now: dt.datetime | None = None) -> int:
    now = now or _now()
    return len(db.execute(delete(AccessLog).where(AccessLog.at < now - dt.timedelta(days=30 * months)).returning(AccessLog.id)).all())


def reject_purged_pending(db: DbSession) -> int:
    """Pending sessions that were purged can never be verified; they are rejected so they do not wait forever."""
    return len(db.execute(update(Session).where(Session.log_purged_at.is_not(None), Session.status == "pending").values(status="rejected", reject_reason="RETENTION_EXPIRED").returning(Session.id)).all())


def clear_stale_invitations(db: DbSession, days_after_expiry: int = 30, now: dt.datetime | None = None) -> int:
    """An invitation nobody accepted keeps the invited email address; a month after it expired the address is removed and the invitation closed."""
    now = now or _now()
    return len(db.execute(update(Invitation).where(Invitation.status == "pending", Invitation.expires_at < now - dt.timedelta(days=days_after_expiry))
                          .values(email=None, status="revoked").returning(Invitation.id)).all())
