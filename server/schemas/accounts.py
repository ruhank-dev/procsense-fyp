"""Request and reply shapes for accounts, organisations, invitations and cohorts."""
from __future__ import annotations

import datetime as dt
import re
import uuid
from typing import Literal

from pydantic import BaseModel, Field, field_validator

Role = Literal["trainee", "supervisor", "org_admin"]
CohortRole = Literal["trainee", "supervisor"]
EMAIL = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")


def _email(value: str) -> str:
    value = value.strip().lower()
    if len(value) > 254 or not EMAIL.match(value):
        raise ValueError("not an email address")
    return value


class MembershipOut(BaseModel):
    organisation_id: uuid.UUID
    organisation_name: str
    slug: str
    role: Role
    status: Literal["active", "deactivated"]


class MeOut(BaseModel):
    user_id: uuid.UUID
    pseudonym: str
    display_name: str
    is_platform_admin: bool
    memberships: list[MembershipOut]


class MePatch(BaseModel):
    display_name: str = Field(min_length=0, max_length=120)


class OrganisationCreate(BaseModel):
    name: str = Field(min_length=2, max_length=120)
    slug: str = Field(pattern=r"^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$")
    admin_email: str

    _check_email = field_validator("admin_email")(_email)


class OrganisationOut(BaseModel):
    id: uuid.UUID
    name: str
    slug: str


class InvitationCreate(BaseModel):
    email: str
    role: Role = "trainee"
    cohort_id: uuid.UUID | None = None

    _check_email = field_validator("email")(_email)


class InvitationOut(BaseModel):
    id: uuid.UUID
    email: str | None
    role: Role
    status: Literal["pending", "accepted", "revoked"]
    expires_at: dt.datetime
    mail: Literal["sent", "already_registered", "not_sent"] = "not_sent"


class OrganisationCreated(BaseModel):
    organisation: OrganisationOut
    invitation: InvitationOut


class MemberOut(BaseModel):
    user_id: uuid.UUID
    pseudonym: str
    display_name: str
    role: Role
    status: Literal["active", "deactivated"]
    cohorts: list[uuid.UUID] = []


class MemberPatch(BaseModel):
    role: Role | None = None
    status: Literal["active", "deactivated"] | None = None


class CohortCreate(BaseModel):
    name: str = Field(min_length=1, max_length=80)


class CohortMemberOut(BaseModel):
    user_id: uuid.UUID
    role: CohortRole


class CohortOut(BaseModel):
    id: uuid.UUID
    name: str
    members: list[CohortMemberOut] = []


class CohortMemberIn(BaseModel):
    role: CohortRole = "trainee"
