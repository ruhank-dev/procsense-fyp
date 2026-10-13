"""Database access for accounts (ORM only). Every function that touches organisation data takes the organisation, so a missing scope is a missing argument, not a forgotten filter."""
from __future__ import annotations

import datetime as dt
import secrets
import uuid

from sqlalchemy import func, select, update
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.orm import Session

from server.models.accounts import Cohort, CohortMember, Invitation, Membership, Organisation, Profile

PSEUDONYM_ALPHABET = "23456789abcdefghjkmnpqrstuvwxyz"   # no 0, o, 1, l, i: easy to read aloud
ROLE_RANK = {"trainee": 0, "supervisor": 1, "org_admin": 2}


def new_pseudonym() -> str:
    return "T-" + "".join(secrets.choice(PSEUDONYM_ALPHABET) for _ in range(8))


# ---------------------------------------------------------------- people

def get_profile(session: Session, user_id: uuid.UUID) -> Profile | None:
    return session.get(Profile, user_id)


def ensure_profile(session: Session, user_id: uuid.UUID) -> Profile:
    """The person's profile, made on first sign-in. Safe when two requests arrive together (the primary key decides) and retries on the (very unlikely) clash of two pseudonyms."""
    for _ in range(5):
        session.execute(insert(Profile).values(user_id=user_id, pseudonym=new_pseudonym()).on_conflict_do_nothing())
        session.flush()
        profile = session.get(Profile, user_id)
        if profile is not None:
            return profile
    raise RuntimeError("could not make a pseudonym")


def memberships_of(session: Session, user_id: uuid.UUID) -> list[tuple[Membership, Organisation]]:
    rows = session.execute(select(Membership, Organisation).join(Organisation, Organisation.id == Membership.organisation_id)
                           .where(Membership.user_id == user_id).order_by(Organisation.name)).all()
    return [(m, o) for m, o in rows]


def get_membership(session: Session, user_id: uuid.UUID, organisation_id: uuid.UUID) -> Membership | None:
    return session.scalars(select(Membership).where(Membership.user_id == user_id, Membership.organisation_id == organisation_id)).first()


# ---------------------------------------------------------------- organisations

def create_organisation(session: Session, name: str, slug: str) -> Organisation:
    org = Organisation(name=name, slug=slug)
    session.add(org)
    session.flush()
    return org


def get_organisation(session: Session, organisation_id: uuid.UUID) -> Organisation | None:
    return session.get(Organisation, organisation_id)


def slug_taken(session: Session, slug: str) -> bool:
    return session.scalar(select(func.count()).select_from(Organisation).where(Organisation.slug == slug)) > 0


def add_membership(session: Session, user_id: uuid.UUID, organisation_id: uuid.UUID, role: str) -> Membership:
    existing = get_membership(session, user_id, organisation_id)
    if existing is not None:
        existing.role, existing.status, existing.deactivated_at = role, "active", None
        session.flush()
        return existing
    m = Membership(user_id=user_id, organisation_id=organisation_id, role=role)
    session.add(m)
    session.flush()
    return m


def members_of(session: Session, organisation_id: uuid.UUID) -> list[tuple[Membership, Profile]]:
    rows = session.execute(select(Membership, Profile).join(Profile, Profile.user_id == Membership.user_id)
                           .where(Membership.organisation_id == organisation_id).order_by(Profile.pseudonym)).all()
    return [(m, p) for m, p in rows]


def active_admin_count(session: Session, organisation_id: uuid.UUID) -> int:
    return session.scalar(select(func.count()).select_from(Membership).where(Membership.organisation_id == organisation_id, Membership.role == "org_admin", Membership.status == "active"))


def set_member(session: Session, organisation_id: uuid.UUID, user_id: uuid.UUID, role: str | None, status: str | None) -> Membership | None:
    m = get_membership(session, user_id, organisation_id)
    if m is None:
        return None
    if role is not None:
        m.role = role
    if status is not None:
        m.status = status
        m.deactivated_at = dt.datetime.now(dt.timezone.utc) if status == "deactivated" else None
    session.flush()
    return m


# ---------------------------------------------------------------- cohorts

def create_cohort(session: Session, organisation_id: uuid.UUID, name: str) -> Cohort:
    c = Cohort(organisation_id=organisation_id, name=name)
    session.add(c)
    session.flush()
    return c


def cohorts_of(session: Session, organisation_id: uuid.UUID) -> list[Cohort]:
    return list(session.scalars(select(Cohort).where(Cohort.organisation_id == organisation_id).order_by(Cohort.name)))


def get_cohort(session: Session, organisation_id: uuid.UUID, cohort_id: uuid.UUID) -> Cohort | None:
    return session.scalars(select(Cohort).where(Cohort.id == cohort_id, Cohort.organisation_id == organisation_id)).first()


def cohort_members(session: Session, cohort_id: uuid.UUID) -> list[CohortMember]:
    return list(session.scalars(select(CohortMember).where(CohortMember.cohort_id == cohort_id)))


def set_cohort_member(session: Session, cohort_id: uuid.UUID, user_id: uuid.UUID, role: str) -> None:
    stmt = insert(CohortMember).values(cohort_id=cohort_id, user_id=user_id, role=role)
    session.execute(stmt.on_conflict_do_update(index_elements=["cohort_id", "user_id"], set_={"role": role}))
    session.flush()


def remove_cohort_member(session: Session, cohort_id: uuid.UUID, user_id: uuid.UUID) -> bool:
    row = session.get(CohortMember, (cohort_id, user_id))
    if row is None:
        return False
    session.delete(row)
    session.flush()
    return True


# ---------------------------------------------------------------- invitations

def create_invitation(session: Session, organisation_id: uuid.UUID, email: str, role: str, cohort_id: uuid.UUID | None,
                      invited_by: uuid.UUID | None, days: int) -> Invitation:
    inv = Invitation(organisation_id=organisation_id, email=email.strip().lower(), role=role, cohort_id=cohort_id, invited_by=invited_by,
                     expires_at=dt.datetime.now(dt.timezone.utc) + dt.timedelta(days=days))
    session.add(inv)
    session.flush()
    return inv


def pending_invitations_for(session: Session, email: str) -> list[Invitation]:
    if not email:
        return []
    now = dt.datetime.now(dt.timezone.utc)
    return list(session.scalars(select(Invitation).where(Invitation.email == email.strip().lower(), Invitation.status == "pending", Invitation.expires_at > now)))


def accept_invitation(session: Session, inv: Invitation, user_id: uuid.UUID) -> None:
    add_membership(session, user_id, inv.organisation_id, inv.role)
    if inv.cohort_id is not None:
        set_cohort_member(session, inv.cohort_id, user_id, "supervisor" if inv.role == "supervisor" else "trainee")
    inv.status, inv.email, inv.accepted_at = "accepted", None, dt.datetime.now(dt.timezone.utc)   # the address is cleared once it has done its job
    session.flush()


def revoke_pending_invitations(session: Session, organisation_id: uuid.UUID, invitation_id: uuid.UUID) -> bool:
    result = session.execute(update(Invitation).where(Invitation.id == invitation_id, Invitation.organisation_id == organisation_id, Invitation.status == "pending")
                             .values(status="revoked", email=None))
    return result.rowcount > 0


def pending_for_organisation(session: Session, organisation_id: uuid.UUID) -> list[Invitation]:
    return list(session.scalars(select(Invitation).where(Invitation.organisation_id == organisation_id, Invitation.status == "pending").order_by(Invitation.created_at)))
