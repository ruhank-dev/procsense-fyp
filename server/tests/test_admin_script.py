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


@pytest.mark.skipif(not os.environ.get("LIVE_SUPABASE_URL"), reason="set LIVE_SUPABASE_URL (the project address, which is public) to check the real key set")
def test_the_real_project_publishes_keys_the_verifier_can_use():
    import jwt
    from server.auth.tokens import ALGORITHMS, fetch_jwks
    document = fetch_jwks(os.environ["LIVE_SUPABASE_URL"].rstrip("/") + "/auth/v1/.well-known/jwks.json")
    assert document["keys"], "the project publishes no signing keys"
    for k in document["keys"]:
        assert k["alg"] in ALGORITHMS and k.get("kid")
        assert jwt.PyJWK.from_dict(k).key is not None
