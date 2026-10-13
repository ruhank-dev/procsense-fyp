"""Organisations, members, invitations and cohorts. Every route names its organisation in the address and passes through a role check first."""
from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from server.auth.dependencies import CurrentUser, OrgContext, db_session, get_admin, require_member, require_org_admin, require_platform_admin, require_supervisor
from server.auth.supabase_admin import ALREADY_REGISTERED, InviteError, SupabaseAdmin
from server.config import Settings, get_settings
from server.models.accounts import Invitation
from server.repositories import accounts
from server.schemas.accounts import (CohortCreate, CohortMemberIn, CohortMemberOut, CohortOut, InvitationCreate, InvitationOut, MemberOut, MemberPatch, OrganisationCreate,
                                     OrganisationCreated, OrganisationOut)

router = APIRouter(tags=["organisations"])


def _invitation_out(inv: Invitation, mail: str = "not_sent") -> InvitationOut:
    return InvitationOut(id=inv.id, email=inv.email, role=inv.role, status=inv.status, expires_at=inv.expires_at, mail=mail)


def _send(admin: SupabaseAdmin, email: str) -> str:
    """Asks Supabase to email the invitation. A failure to send is not a failure to invite: the invitation exists and applies when that address signs in."""
    try:
        return "already_registered" if admin.invite(email) == ALREADY_REGISTERED else "sent"
    except InviteError:
        return "not_sent"


@router.post("/organisations", response_model=OrganisationCreated, status_code=201)
def create_organisation(body: OrganisationCreate, user: CurrentUser = Depends(require_platform_admin), session: Session = Depends(db_session),
                        admin: SupabaseAdmin = Depends(get_admin), settings: Settings = Depends(get_settings)) -> OrganisationCreated:
    """Platform administrators (the team) create an organisation and invite its first administrator."""
    if accounts.slug_taken(session, body.slug):
        raise HTTPException(status_code=409, detail="slug_taken")
    org = accounts.create_organisation(session, body.name.strip(), body.slug)
    inv = accounts.create_invitation(session, org.id, body.admin_email, "org_admin", None, user.profile.user_id, settings.invitation_days)
    session.commit()
    return OrganisationCreated(organisation=OrganisationOut(id=org.id, name=org.name, slug=org.slug), invitation=_invitation_out(inv, _send(admin, body.admin_email)))


@router.get("/organisations/{organisation_id}", response_model=OrganisationOut)
def read_organisation(ctx: OrgContext = Depends(require_member)) -> OrganisationOut:
    o = ctx.organisation
    return OrganisationOut(id=o.id, name=o.name, slug=o.slug)


# ---------------------------------------------------------------- members

@router.get("/organisations/{organisation_id}/members", response_model=list[MemberOut])
def list_members(ctx: OrgContext = Depends(require_supervisor), session: Session = Depends(db_session)) -> list[MemberOut]:
    cohort_ids: dict[uuid.UUID, list[uuid.UUID]] = {}
    for c in accounts.cohorts_of(session, ctx.organisation.id):
        for cm in accounts.cohort_members(session, c.id):
            cohort_ids.setdefault(cm.user_id, []).append(c.id)
    return [MemberOut(user_id=p.user_id, pseudonym=p.pseudonym, display_name=p.display_name, role=m.role, status=m.status, cohorts=cohort_ids.get(p.user_id, []))
            for m, p in accounts.members_of(session, ctx.organisation.id)]


@router.patch("/organisations/{organisation_id}/members/{user_id}", response_model=MemberOut)
def update_member(user_id: uuid.UUID, body: MemberPatch, ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session)) -> MemberOut:
    """Change a role or deactivate a person (people are deactivated, never deleted). The last active administrator cannot be demoted or deactivated."""
    current = accounts.get_membership(session, user_id, ctx.organisation.id)
    if current is None:
        raise HTTPException(status_code=404, detail="not_found")
    leaving_admin = current.role == "org_admin" and current.status == "active" and ((body.role is not None and body.role != "org_admin") or body.status == "deactivated")
    if leaving_admin and accounts.active_admin_count(session, ctx.organisation.id) <= 1:
        raise HTTPException(status_code=409, detail="last_administrator")
    accounts.set_member(session, ctx.organisation.id, user_id, body.role, body.status)
    session.commit()
    m, p = next((m, p) for m, p in accounts.members_of(session, ctx.organisation.id) if p.user_id == user_id)
    return MemberOut(user_id=p.user_id, pseudonym=p.pseudonym, display_name=p.display_name, role=m.role, status=m.status)


# ---------------------------------------------------------------- invitations

@router.get("/organisations/{organisation_id}/invitations", response_model=list[InvitationOut])
def list_invitations(ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session)) -> list[InvitationOut]:
    return [_invitation_out(i) for i in accounts.pending_for_organisation(session, ctx.organisation.id)]


@router.post("/organisations/{organisation_id}/invitations", response_model=InvitationOut, status_code=201)
def invite(body: InvitationCreate, ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session),
           admin: SupabaseAdmin = Depends(get_admin), settings: Settings = Depends(get_settings)) -> InvitationOut:
    if body.cohort_id is not None and accounts.get_cohort(session, ctx.organisation.id, body.cohort_id) is None:
        raise HTTPException(status_code=404, detail="cohort_not_found")
    inv = accounts.create_invitation(session, ctx.organisation.id, body.email, body.role, body.cohort_id, ctx.user.profile.user_id, settings.invitation_days)
    session.commit()
    return _invitation_out(inv, _send(admin, body.email))


@router.delete("/organisations/{organisation_id}/invitations/{invitation_id}", status_code=204)
def revoke_invitation(invitation_id: uuid.UUID, ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session)) -> None:
    if not accounts.revoke_pending_invitations(session, ctx.organisation.id, invitation_id):
        raise HTTPException(status_code=404, detail="not_found")
    session.commit()


# ---------------------------------------------------------------- cohorts

def _cohort_out(session: Session, cohort) -> CohortOut:
    return CohortOut(id=cohort.id, name=cohort.name, members=[CohortMemberOut(user_id=m.user_id, role=m.role) for m in accounts.cohort_members(session, cohort.id)])


@router.get("/organisations/{organisation_id}/cohorts", response_model=list[CohortOut])
def list_cohorts(ctx: OrgContext = Depends(require_supervisor), session: Session = Depends(db_session)) -> list[CohortOut]:
    return [_cohort_out(session, c) for c in accounts.cohorts_of(session, ctx.organisation.id)]


@router.post("/organisations/{organisation_id}/cohorts", response_model=CohortOut, status_code=201)
def create_cohort(body: CohortCreate, ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session)) -> CohortOut:
    try:
        cohort = accounts.create_cohort(session, ctx.organisation.id, body.name.strip())
        session.commit()
    except IntegrityError:
        session.rollback()
        raise HTTPException(status_code=409, detail="cohort_name_taken") from None
    return _cohort_out(session, cohort)


@router.put("/organisations/{organisation_id}/cohorts/{cohort_id}/members/{user_id}", response_model=CohortOut)
def set_cohort_member(cohort_id: uuid.UUID, user_id: uuid.UUID, body: CohortMemberIn, ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session)) -> CohortOut:
    cohort = accounts.get_cohort(session, ctx.organisation.id, cohort_id)
    member = accounts.get_membership(session, user_id, ctx.organisation.id)
    if cohort is None or member is None or member.status != "active":
        raise HTTPException(status_code=404, detail="not_found")
    if body.role == "supervisor" and accounts.ROLE_RANK[member.role] < accounts.ROLE_RANK["supervisor"]:
        raise HTTPException(status_code=409, detail="member_cannot_supervise")
    accounts.set_cohort_member(session, cohort.id, user_id, body.role)
    session.commit()
    return _cohort_out(session, cohort)


@router.delete("/organisations/{organisation_id}/cohorts/{cohort_id}/members/{user_id}", status_code=204)
def remove_cohort_member(cohort_id: uuid.UUID, user_id: uuid.UUID, ctx: OrgContext = Depends(require_org_admin), session: Session = Depends(db_session)) -> None:
    cohort = accounts.get_cohort(session, ctx.organisation.id, cohort_id)
    if cohort is None or not accounts.remove_cohort_member(session, cohort.id, user_id):
        raise HTTPException(status_code=404, detail="not_found")
    session.commit()
