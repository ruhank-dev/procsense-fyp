"""The one place the service uses Supabase's server-side (secret) key: sending an invitation email through Supabase Auth's admin interface (p1-backend-s08).

The secret key is sent in the `apikey` header only (Supabase's new secret keys are not tokens and do not go in `Authorization: Bearer`). It is never logged and never returned.
If Supabase refuses an invitation because the address already has an account, that is not an error here: the person simply signs in and the invitation is applied.
"""
from __future__ import annotations

import httpx

from server.config import Settings

ALREADY_REGISTERED = "already_registered"


class InviteError(Exception):
    pass


class DeleteUserError(Exception):
    pass


class SupabaseAdmin:
    def __init__(self, base_url: str, secret_key: str, transport: httpx.BaseTransport | None = None, timeout: float = 10.0):
        self._base = base_url.rstrip("/")
        self._key = secret_key
        self._client = httpx.Client(transport=transport, timeout=timeout)

    @classmethod
    def from_settings(cls, settings: Settings) -> "SupabaseAdmin":
        return cls(settings.supabase_url, settings.supabase_secret_key.get_secret_value())

    @property
    def configured(self) -> bool:
        return bool(self._base and self._key)

    def invite(self, email: str, redirect_to: str | None = None) -> str:
        """Asks Supabase to email an invitation. Returns "sent" or ALREADY_REGISTERED; raises InviteError for anything else (the reason never contains the key)."""
        if not self.configured:
            raise InviteError("supabase_not_configured")
        body: dict = {"email": email}
        if redirect_to:
            body["redirect_to"] = redirect_to
        try:
            response = self._client.post(f"{self._base}/auth/v1/invite", json=body, headers={"apikey": self._key})
        except httpx.HTTPError as e:
            raise InviteError("supabase_unreachable") from e
        if response.status_code in (200, 201):
            return "sent"
        text = response.text.lower()
        if response.status_code in (400, 409, 422) and ("already" in text and ("registered" in text or "exists" in text)):
            return ALREADY_REGISTERED
        raise InviteError(f"supabase_refused_{response.status_code}")

    def delete_user(self, user_id: str) -> None:
        """Removes the person's account (and with it their email address and password) from Supabase Auth (p1-backend-s12). A person who is already gone counts as deleted."""
        if not self.configured:
            raise DeleteUserError("supabase_not_configured")
        try:
            response = self._client.delete(f"{self._base}/auth/v1/admin/users/{user_id}", headers={"apikey": self._key})
        except httpx.HTTPError as e:
            raise DeleteUserError("supabase_unreachable") from e
        if response.status_code not in (200, 204, 404):
            raise DeleteUserError(f"supabase_refused_{response.status_code}")
