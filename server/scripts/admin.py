"""Platform administration by the team, from a laptop (p1-backend-s08). There is no screen for this on purpose (DT-129).

    uv run --project server python -m server.scripts.admin create-organisation --name "Alpha Hospital" --slug alpha-hospital --admin-email boss@example.com [--send]
    uv run --project server python -m server.scripts.admin grant-platform-admin --user-id <the person's id from Supabase's Authentication > Users page>
    uv run --project server python -m server.scripts.admin add-member --slug alpha-hospital --user-id <id> --role trainee|supervisor|org_admin

The database is the one in DATABASE_URL (server/.env). The first administrator of an organisation is invited by email address: when that address signs in, they become its administrator.
--send also asks Supabase to email the invitation (needs the Supabase address and the secret key in server/.env).
"""
from __future__ import annotations

import argparse
import sys
import uuid

from sqlalchemy import select
from sqlalchemy.orm import Session

from server.auth.supabase_admin import ALREADY_REGISTERED, InviteError, SupabaseAdmin
from server.config import get_settings
from server.database import get_engine
from server.models.accounts import Organisation
from server.repositories import accounts


def create_organisation_with_admin(session: Session, name: str, slug: str, admin_email: str, days: int = 14):
    if accounts.slug_taken(session, slug):
        raise SystemExit(f"the slug '{slug}' is already used")
    org = accounts.create_organisation(session, name.strip(), slug)
    invitation = accounts.create_invitation(session, org.id, admin_email, "org_admin", None, None, days)
    session.commit()
    return org, invitation


def grant_platform_admin(session: Session, user_id: uuid.UUID) -> bool:
    """Makes the person a platform administrator. They must have signed in at least once (that creates the profile)."""
    profile = accounts.get_profile(session, user_id)
    if profile is None:
        return False
    profile.is_platform_admin = True
    session.commit()
    return True


def add_member(session: Session, slug: str, user_id: uuid.UUID, role: str) -> str:
    """Puts a person who has signed in once into an organisation with a role (for set-ups and demos; people are normally invited by email). Returns a sentence saying what happened."""
    org = session.scalars(select(Organisation).where(Organisation.slug == slug)).first()
    if org is None:
        raise SystemExit(f"no organisation with the short name '{slug}'")
    if accounts.get_profile(session, user_id) is None:
        raise SystemExit("no such person: they must sign in once first (that creates their profile)")
    if accounts.get_membership(session, user_id, org.id) is not None:
        accounts.set_member(session, org.id, user_id, role, "active")
        session.commit()
        return f"that person was already a member of {slug}: role is now {role} and the membership is active"
    accounts.add_membership(session, user_id, org.id, role)
    session.commit()
    return f"added to {slug} as {role}"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    org = sub.add_parser("create-organisation")
    org.add_argument("--name", required=True)
    org.add_argument("--slug", required=True)
    org.add_argument("--admin-email", required=True)
    org.add_argument("--send", action="store_true", help="also ask Supabase to email the invitation")
    grant = sub.add_parser("grant-platform-admin")
    grant.add_argument("--user-id", required=True, type=uuid.UUID)
    member = sub.add_parser("add-member")
    member.add_argument("--slug", required=True)
    member.add_argument("--user-id", required=True, type=uuid.UUID)
    member.add_argument("--role", required=True, choices=["trainee", "supervisor", "org_admin"])
    args = parser.parse_args(argv)

    engine = get_engine()
    if engine is None:
        print("DATABASE_URL is not set (server/.env)")
        return 1
    settings = get_settings()
    with Session(engine) as session:
        if args.command == "grant-platform-admin":
            if grant_platform_admin(session, args.user_id):
                print("done: that person is now a platform administrator")
                return 0
            print("no such person: they must sign in once first (that creates their profile)")
            return 1
        if args.command == "add-member":
            print(add_member(session, args.slug, args.user_id, args.role))
            return 0
        created, invitation = create_organisation_with_admin(session, args.name, args.slug, args.admin_email, settings.invitation_days)
        print(f"organisation {created.slug} created; {invitation.email} will become its administrator when they sign in")
        if args.send:
            try:
                result = SupabaseAdmin.from_settings(settings).invite(invitation.email)
                print("invitation email: " + ("that address already has an account, it only has to sign in" if result == ALREADY_REGISTERED else "sent"))
            except InviteError as e:
                print(f"invitation email NOT sent ({e}); the invitation still applies when that address signs in")
    return 0


if __name__ == "__main__":
    sys.exit(main())
