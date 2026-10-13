"""Checking Supabase access tokens (T-AC2). No database needed."""
import time
import uuid

import jwt
import pytest
from cryptography.hazmat.primitives.asymmetric import ec

from server.auth.tokens import Principal, TokenError, TokenVerifier
from server.tests.support import AUDIENCE, ISSUER, SigningKey

USER = uuid.uuid4()


class Clock:
    def __init__(self):
        self.now = 1000.0

    def __call__(self) -> float:
        return self.now


def make(key: SigningKey, fetches: list | None = None, clock: Clock | None = None, jwks=None) -> TokenVerifier:
    def fetcher(url):
        if fetches is not None:
            fetches.append(url)
        return jwks() if callable(jwks) else (jwks or key.jwks())
    return TokenVerifier(ISSUER, AUDIENCE, ISSUER + "/.well-known/jwks.json", cache_seconds=600, leeway=10, fetcher=fetcher, clock=clock or Clock())


def test_a_valid_token_gives_the_person_and_the_lower_cased_email():
    key = SigningKey()
    p = make(key).verify(key.token(USER, "Moiz@Example.COM"))
    assert isinstance(p, Principal) and p.user_id == USER and p.email == "moiz@example.com"


def test_an_expired_token_is_refused_but_a_few_seconds_of_clock_difference_is_tolerated():
    key = SigningKey()
    v = make(key)
    with pytest.raises(TokenError):
        v.verify(key.token(USER, expires_in=-60))
    assert v.verify(key.token(USER, expires_in=-5)).user_id == USER   # inside the 10 s leeway


def test_a_token_for_another_audience_or_issuer_is_refused():
    key = SigningKey()
    v = make(key)
    with pytest.raises(TokenError):
        v.verify(key.token(USER, audience="anon"))
    with pytest.raises(TokenError):
        v.verify(key.token(USER, issuer="https://evil.example/auth/v1"))


def test_a_token_signed_with_another_key_is_refused():
    key, forger = SigningKey(), SigningKey()   # same key id, different key
    with pytest.raises(TokenError):
        make(key).verify(forger.token(USER))


def test_the_none_algorithm_is_refused():
    key = SigningKey()
    token = jwt.encode({"iss": ISSUER, "aud": AUDIENCE, "sub": str(USER), "exp": int(time.time()) + 600}, key=None, algorithm="none", headers={"kid": key.kid})
    with pytest.raises(TokenError, match="not accepted"):
        make(key).verify(token)


def test_a_symmetric_token_that_uses_the_public_key_as_its_secret_is_refused():
    # the classic algorithm-confusion attack: sign with HS256 using the (public) key text as the shared secret. The library will not even make such a token, so it is built by hand.
    import base64
    import hashlib
    import hmac
    import json
    from cryptography.hazmat.primitives import serialization

    key = SigningKey()
    public_pem = key.private.public_key().public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo)
    b64 = lambda raw: base64.urlsafe_b64encode(raw).rstrip(b"=").decode()
    head = b64(json.dumps({"alg": "HS256", "typ": "JWT", "kid": key.kid}).encode())
    body = b64(json.dumps({"iss": ISSUER, "aud": AUDIENCE, "sub": str(USER), "exp": int(time.time()) + 600}).encode())
    signature = b64(hmac.new(public_pem, f"{head}.{body}".encode(), hashlib.sha256).digest())
    with pytest.raises(TokenError, match="not accepted"):
        make(key).verify(f"{head}.{body}.{signature}")


def test_a_token_without_the_required_claims_or_with_a_bad_subject_is_refused():
    key = SigningKey()
    v = make(key)
    now = int(time.time())
    no_sub = jwt.encode({"iss": ISSUER, "aud": AUDIENCE, "exp": now + 600}, key.private, algorithm="ES256", headers={"kid": key.kid})
    with pytest.raises(TokenError):
        v.verify(no_sub)
    with pytest.raises(TokenError, match="UUID"):
        v.verify(key.token("not-a-uuid"))
    with pytest.raises(TokenError):
        v.verify("this is not a token")
    with pytest.raises(TokenError):
        v.verify(jwt.encode({"iss": ISSUER, "aud": AUDIENCE, "sub": str(USER)}, key.private, algorithm="ES256", headers={"kid": key.kid}))   # no exp


def test_the_key_set_is_cached_and_refreshed_after_it_expires():
    key, fetches, clock = SigningKey(), [], Clock()
    v = make(key, fetches, clock)
    for _ in range(5):
        v.verify(key.token(USER))
    assert len(fetches) == 1
    clock.now += 601
    v.verify(key.token(USER))
    assert len(fetches) == 2


def test_a_rotated_key_is_found_with_one_refresh_and_garbage_cannot_cause_a_fetch_every_time():
    old, new, fetches, clock = SigningKey("key-old"), SigningKey("key-new"), [], Clock()
    published = {"doc": old.jwks()}
    v = make(old, fetches, clock, jwks=lambda: published["doc"])
    v.verify(old.token(USER))
    published["doc"] = {"keys": old.jwks()["keys"] + new.jwks()["keys"]}
    clock.now += 61   # more than a minute since the last fetch
    assert v.verify(new.token(USER)).user_id == USER
    assert len(fetches) == 2
    before = len(fetches)
    for _ in range(5):
        with pytest.raises(TokenError, match="unknown signing key"):
            v.verify(SigningKey("key-garbage").token(USER))
    assert len(fetches) - before <= 1   # at most one more look inside a minute


def test_a_key_set_that_cannot_be_fetched_is_a_refusal_not_a_crash():
    import httpx
    def broken(url):
        raise httpx.ConnectError("down")
    v = TokenVerifier(ISSUER, AUDIENCE, ISSUER + "/.well-known/jwks.json", fetcher=broken)
    with pytest.raises(TokenError, match="could not be fetched"):
        v.verify(SigningKey().token(USER))
