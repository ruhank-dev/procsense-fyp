"""Who am I: the signed-in person's profile and organisations."""
from __future__ import annotations

from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from server.auth.dependencies import CurrentUser, current_user, db_session
from server.repositories import accounts
from server.schemas.accounts import MeOut, MembershipOut, MePatch

router = APIRouter(tags=["me"])


def _me(user: CurrentUser, session: Session) -> MeOut:
    rows = accounts.memberships_of(session, user.profile.user_id)
    return MeOut(user_id=user.profile.user_id, pseudonym=user.profile.pseudonym, display_name=user.profile.display_name, is_platform_admin=user.profile.is_platform_admin,
                 memberships=[MembershipOut(organisation_id=o.id, organisation_name=o.name, slug=o.slug, role=m.role, status=m.status) for m, o in rows])


@router.get("/me", response_model=MeOut)
def read_me(user: CurrentUser = Depends(current_user), session: Session = Depends(db_session)) -> MeOut:
    return _me(user, session)


@router.patch("/me", response_model=MeOut)
def update_me(body: MePatch, user: CurrentUser = Depends(current_user), session: Session = Depends(db_session)) -> MeOut:
    """The display name is what supervisors and admins of the same organisation see; exports and session data carry only the pseudonymous id."""
    user.profile.display_name = body.display_name.strip()
    session.commit()
    return _me(user, session)
