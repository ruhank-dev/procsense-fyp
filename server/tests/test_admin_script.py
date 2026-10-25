"""The team's platform-administration script, and (optionally) a look at the real Supabase project's key set."""
import os
import uuid

import pytest
from sqlalchemy import select
from sqlalchemy.orm import Session

from server.models.accounts import Invitation
from server.repositories import accounts
from server.scripts import admin


@pytest.mark.db
def test_an_organisation_is_created_with_an_invitation_for_its_first_administrator(migrated):
    with Session(migrated) as s:
        org, inv = admin.create_organisation_with_admin(s, "Alpha Hospital", "alpha-hospital", "Boss@Example.com")
        assert inv.role == "org_admin" and inv.email == "boss@example.com" and inv.organisation_id == org.id
        with pytest.raises(SystemExit):
            admin.create_organisation_with_admin(s, "Another", "alpha-hospital", "x@example.com")
    with Session(migrated) as s:
        assert len(s.scalars(select(Invitation)).all()) == 1


@pytest.mark.db
def test_granting_platform_admin_needs_a_profile_so_the_person_signs_in_once_first(migrated):
    uid = uuid.uuid4()
    with Session(migrated) as s:
        assert admin.grant_platform_admin(s, uid) is False
        accounts.ensure_profile(s, uid)
        s.commit()
        assert admin.grant_platform_admin(s, uid) is True
        assert accounts.get_profile(s, uid).is_platform_admin is True


@pytest.mark.db
def test_add_member_puts_a_person_who_has_signed_in_into_an_organisation_and_can_change_their_role(migrated):
    uid = uuid.uuid4()
    with Session(migrated) as s:
        org, _ = admin.create_organisation_with_admin(s, "Alpha Hospital", "alpha-hospital", "boss@example.com")
        with pytest.raises(SystemExit, match="sign in once first"):
            admin.add_member(s, "alpha-hospital", uid, "trainee")
        accounts.ensure_profile(s, uid)
        s.commit()
        with pytest.raises(SystemExit, match="no organisation"):
            admin.add_member(s, "nowhere", uid, "trainee")
        assert admin.add_member(s, "alpha-hospital", uid, "trainee") == "added to alpha-hospital as trainee"
        assert "role is now supervisor" in admin.add_member(s, "alpha-hospital", uid, "supervisor")
        assert accounts.get_membership(s, uid, org.id).role == "supervisor"


@pytest.mark.skipif(not os.environ.get("LIVE_SUPABASE_URL"), reason="set LIVE_SUPABASE_URL (the project address, which is public) to check the real key set")
def test_the_real_project_publishes_keys_the_verifier_can_use():
    import jwt
    from server.auth.tokens import ALGORITHMS, fetch_jwks
    document = fetch_jwks(os.environ["LIVE_SUPABASE_URL"].rstrip("/") + "/auth/v1/.well-known/jwks.json")
    assert document["keys"], "the project publishes no signing keys"
    for k in document["keys"]:
        assert k["alg"] in ALGORITHMS and k.get("kid")
        assert jwt.PyJWK.from_dict(k).key is not None


@pytest.mark.db
def test_delete_person_cuts_the_links_removes_the_account_and_refuses_the_last_administrator(migrated):
    """DT-156: the team's deletion for a person who cannot sign in. The sign-in service is a stand-in that records the call."""
    from server.auth.supabase_admin import DeleteUserError
    from server.models.accounts import Profile

    class Service:
        def __init__(self, fail=False):
            self.fail, self.deleted = fail, []

        def delete_user(self, user_id):
            if self.fail:
                raise DeleteUserError("down")
            self.deleted.append(user_id)

    with Session(migrated) as s:
        org, _ = admin.create_organisation_with_admin(s, "Alpha", "alpha", "boss@example.com")
        admin_id, trainee_id = uuid.uuid4(), uuid.uuid4()
        for uid in (admin_id, trainee_id):
            s.add(Profile(user_id=uid, pseudonym=accounts.new_pseudonym(), display_name=""))
        s.flush()
        accounts.add_membership(s, admin_id, org.id, "org_admin")
        accounts.add_membership(s, trainee_id, org.id, "trainee")
        s.commit()
        refused = Service()
        assert admin.delete_person(s, refused, admin_id).startswith("not deleted: they are the last administrator") and refused.deleted == []
        down = Service(fail=True)
        assert admin.delete_person(s, down, trainee_id).startswith("not deleted: the sign-in service refused")
        assert s.get(Profile, trainee_id) is not None                  # nothing was cut
        ok = Service()
        assert admin.delete_person(s, ok, trainee_id).startswith("deleted") and ok.deleted == [str(trainee_id)]
        s.expire_all()
        assert s.get(Profile, trainee_id) is None
