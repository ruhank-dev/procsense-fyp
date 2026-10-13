"""Helpers shared by the account tests: a signing key and JWKS for test tokens, a fake invitation sender, and a small client wrapper."""
from __future__ import annotations

import time
import uuid
from dataclasses import dataclass, field

import jwt
from cryptography.hazmat.primitives.asymmetric import ec

SUPABASE_URL = "https://testproject.supabase.co"
ISSUER = SUPABASE_URL + "/auth/v1"
AUDIENCE = "authenticated"


class SigningKey:
    """A test P-256 key pair standing in for the project's signing key."""
    def __init__(self, kid: str = "test-key-1"):
        self.kid = kid
        self.private = ec.generate_private_key(ec.SECP256R1())

    def jwks(self) -> dict:
        public = jwt.algorithms.ECAlgorithm.to_jwk(self.private.public_key(), as_dict=True)
        public.update({"kid": self.kid, "alg": "ES256", "use": "sig"})
        return {"keys": [public]}

    def token(self, user_id: uuid.UUID | str, email: str = "", *, issuer: str = ISSUER, audience: str = AUDIENCE, expires_in: int = 3600, extra: dict | None = None,
              kid: str | None = None, algorithm: str = "ES256", key=None) -> str:
        now = int(time.time())
        claims = {"iss": issuer, "aud": audience, "sub": str(user_id), "iat": now, "exp": now + expires_in, "role": "authenticated"}
        if email:
            claims["email"] = email
        claims.update(extra or {})
        return jwt.encode(claims, key or self.private, algorithm=algorithm, headers={"kid": kid or self.kid})


@dataclass
class FakeAdmin:
    """Stands in for Supabase's invitation call; remembers what it was asked to send."""
    sent: list[str] = field(default_factory=list)
    deleted: list[str] = field(default_factory=list)
    delete_fails: bool = False
    result: str = "sent"
    configured: bool = True

    def invite(self, email: str, redirect_to: str | None = None) -> str:
        self.sent.append(email)
        if self.result == "error":
            from server.auth.supabase_admin import InviteError
            raise InviteError("supabase_unreachable")
        return self.result

    def delete_user(self, user_id: str) -> None:
        if self.delete_fails:
            from server.auth.supabase_admin import DeleteUserError
            raise DeleteUserError("supabase_unreachable")
        self.deleted.append(str(user_id))
