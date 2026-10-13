"""Checking a Supabase access token (p1-backend-s08).

The signature is checked against the project's public keys (the JWKS address), never against a shared secret: only ES256 and RS256 are accepted, so a token signed with `none`, or with HS256 using
a public key as the secret, is refused. The issuer, the audience and the expiry are checked (a few seconds of clock difference are allowed), and `sub` must be a UUID.
The key set is cached for a few minutes; a token naming a key we do not have causes one refresh (so a key rotation works) and no more than one refresh per minute (so garbage cannot make us fetch all day).
"""
from __future__ import annotations

import time
import uuid
from collections.abc import Callable
from dataclasses import dataclass, field

import httpx
import jwt

ALGORITHMS = ["ES256", "RS256"]
MIN_REFETCH_SECONDS = 60


class TokenError(Exception):
    """The token is not acceptable. The message is for the log, never for the caller (the reply is a plain 401)."""


@dataclass(frozen=True)
class Principal:
    """Who the token says is calling. Roles and organisations are NOT taken from it: they are read from the database on every request."""
    user_id: uuid.UUID
    email: str
    claims: dict = field(repr=False)
    device_session_id: uuid.UUID | None = None   # set when the caller is a headset signed in with a device code (p1-backend-s10); None for a Supabase login


def fetch_jwks(url: str, timeout: float = 5.0) -> dict:
    response = httpx.get(url, timeout=timeout)
    response.raise_for_status()
    return response.json()


class TokenVerifier:
    def __init__(self, issuer: str, audience: str, jwks_url: str, cache_seconds: int = 600, leeway: int = 10,
                 fetcher: Callable[[str], dict] = fetch_jwks, clock: Callable[[], float] = time.monotonic):
        self._issuer, self._audience, self._url = issuer, audience, jwks_url
        self._cache_seconds, self._leeway, self._fetch, self._clock = cache_seconds, leeway, fetcher, clock
        self._keys: dict[str, dict] = {}
        self._fetched_at: float | None = None

    def _refresh(self) -> None:
        document = self._fetch(self._url)
        self._keys = {k["kid"]: k for k in document.get("keys", []) if "kid" in k}
        self._fetched_at = self._clock()

    def _key_for(self, kid: str):
        now = self._clock()
        if self._fetched_at is None or now - self._fetched_at > self._cache_seconds:
            self._refresh()
        if kid not in self._keys and self._fetched_at is not None and now - self._fetched_at >= MIN_REFETCH_SECONDS:
            self._refresh()   # a rotated key: look once more, but not more than once a minute
        if kid not in self._keys:
            raise TokenError("unknown signing key")
        return jwt.PyJWK.from_dict(self._keys[kid]).key

    def verify(self, token: str) -> Principal:
        try:
            header = jwt.get_unverified_header(token)
        except jwt.PyJWTError as e:
            raise TokenError("not a token") from e
        if header.get("alg") not in ALGORITHMS:
            raise TokenError(f"algorithm {header.get('alg')!r} is not accepted")
        kid = header.get("kid")
        if not kid:
            raise TokenError("no key id")
        try:
            key = self._key_for(kid)
            claims = jwt.decode(token, key, algorithms=ALGORITHMS, audience=self._audience, issuer=self._issuer, leeway=self._leeway,
                                options={"require": ["exp", "sub", "aud", "iss"]})
        except httpx.HTTPError as e:
            raise TokenError("the key set could not be fetched") from e
        except jwt.PyJWTError as e:
            raise TokenError(str(e)) from e
        try:
            user_id = uuid.UUID(str(claims["sub"]))
        except ValueError as e:
            raise TokenError("sub is not a UUID") from e
        return Principal(user_id=user_id, email=str(claims.get("email", "")).strip().lower(), claims=claims)
