"""Organisations, people, roles and cohorts (p1-backend-s08). Personal data kept here is minimal: a display name and a pseudonymous id. The email address stays in Supabase Auth,
except that a pending invitation holds the invited address until it is accepted, revoked or expired."""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import Boolean, CheckConstraint, DateTime, ForeignKey, Index, SmallInteger, String, UniqueConstraint, func, text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

from server.database import Base

ROLES = ("trainee", "supervisor", "org_admin")
MEMBER_STATUSES = ("active", "deactivated")
COHORT_ROLES = ("trainee", "supervisor")
INVITATION_STATUSES = ("pending", "accepted", "revoked")
CONSENT_KINDS = ("results", "research")


def _uuid_pk() -> Mapped[uuid.UUID]:
    return mapped_column(UUID(as_uuid=True), primary_key=True, server_default=text("gen_random_uuid()"))


def _created() -> Mapped[dt.datetime]:
    return mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)


def _in(column: str, values: tuple[str, ...]) -> str:
    return f"{column} in (" + ", ".join(f"'{v}'" for v in values) + ")"


class Organisation(Base):
    __tablename__ = "organisation"
    __table_args__ = (CheckConstraint("slug ~ '^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$'", name="slug_format"),
                      CheckConstraint("retention_months is null or retention_months between 6 and 60", name="retention_months_range"))

    id: Mapped[uuid.UUID] = _uuid_pk()
    name: Mapped[str] = mapped_column(String(120), nullable=False)
    slug: Mapped[str] = mapped_column(String(40), nullable=False, unique=True)
    active: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("true"))
    retention_months: Mapped[int | None] = mapped_column(SmallInteger)   # how long the stored session logs are kept; empty = the platform's default (p1-backend-s12)
    created_at: Mapped[dt.datetime] = _created()


class Profile(Base):
    """One row per person. user_id is the Supabase Auth user id (that table lives in another schema, so there is no foreign key)."""
    __tablename__ = "profile"

    user_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    pseudonym: Mapped[str] = mapped_column(String(16), nullable=False, unique=True)
    display_name: Mapped[str] = mapped_column(String(120), nullable=False, server_default=text("''"))
    is_platform_admin: Mapped[bool] = mapped_column(Boolean, nullable=False, server_default=text("false"))
    deleted_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))   # set on the empty stand-in that keeps a deleted person's sessions together (p1-backend-s12)
    created_at: Mapped[dt.datetime] = _created()


class Membership(Base):
    __tablename__ = "membership"
    __table_args__ = (
        UniqueConstraint("user_id", "organisation_id"),
        CheckConstraint(_in("role", ROLES), name="role_values"),
        CheckConstraint(_in("status", MEMBER_STATUSES), name="status_values"),
        Index("ix_membership_organisation_id", "organisation_id"),
    )

    id: Mapped[uuid.UUID] = _uuid_pk()
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), nullable=False)
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    role: Mapped[str] = mapped_column(String(16), nullable=False)
    status: Mapped[str] = mapped_column(String(16), nullable=False, server_default=text("'active'"))
    created_at: Mapped[dt.datetime] = _created()
    deactivated_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))


class Cohort(Base):
    __tablename__ = "cohort"
    __table_args__ = (UniqueConstraint("organisation_id", "name"),)

    id: Mapped[uuid.UUID] = _uuid_pk()
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    name: Mapped[str] = mapped_column(String(80), nullable=False)
    created_at: Mapped[dt.datetime] = _created()


class CohortMember(Base):
    __tablename__ = "cohort_member"
    __table_args__ = (CheckConstraint(_in("role", COHORT_ROLES), name="role_values"),)

    cohort_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("cohort.id"), primary_key=True)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), primary_key=True)
    role: Mapped[str] = mapped_column(String(16), nullable=False)


class Invitation(Base):
    """A person invited into an organisation. The address is kept only while the invitation is pending and is cleared when it is accepted or revoked."""
    __tablename__ = "invitation"
    __table_args__ = (
        CheckConstraint(_in("role", ROLES), name="role_values"),
        CheckConstraint(_in("status", INVITATION_STATUSES), name="status_values"),
        Index("ix_invitation_email_pending", "email", postgresql_where=text("status = 'pending'")),
    )

    id: Mapped[uuid.UUID] = _uuid_pk()
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    email: Mapped[str | None] = mapped_column(String(254))
    role: Mapped[str] = mapped_column(String(16), nullable=False)
    cohort_id: Mapped[uuid.UUID | None] = mapped_column(ForeignKey("cohort.id"))
    invited_by: Mapped[uuid.UUID | None] = mapped_column(ForeignKey("profile.user_id"))
    status: Mapped[str] = mapped_column(String(16), nullable=False, server_default=text("'pending'"))
    created_at: Mapped[dt.datetime] = _created()
    expires_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    accepted_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))


class ConsentRecord(Base):
    """Filled by p1-backend-s12 (consent at first sign-in); the table is created here with the other account tables."""
    __tablename__ = "consent_record"
    __table_args__ = (CheckConstraint(_in("kind", CONSENT_KINDS), name="kind_values"),)

    id: Mapped[uuid.UUID] = _uuid_pk()
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), nullable=False)
    kind: Mapped[str] = mapped_column(String(16), nullable=False)
    text_version: Mapped[str] = mapped_column(String(40), nullable=False)
    granted: Mapped[bool] = mapped_column(Boolean, nullable=False)
    recorded_at: Mapped[dt.datetime] = _created()
