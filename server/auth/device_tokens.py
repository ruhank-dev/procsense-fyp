"""Random values for the device-code login (p1-backend-s10). The values are shown to the headset once; the database keeps only their SHA-256."""
from __future__ import annotations

import hashlib
import secrets

ACCESS_PREFIX = "psd_"
REFRESH_PREFIX = "psr_"
# Letters and digits a person can read aloud or type without doubt: no vowels (so no words), and none of 0 O 1 I L U Y.
USER_CODE_ALPHABET = "BCDFGHJKMNPQRSTVWXZ2345679"
USER_CODE_LENGTH = 8


def new_secret(prefix: str = "") -> str:
    return prefix + secrets.token_urlsafe(32)


def hash_secret(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def new_user_code() -> str:
    return "".join(secrets.choice(USER_CODE_ALPHABET) for _ in range(USER_CODE_LENGTH))


def show_user_code(code: str) -> str:
    return code[:4] + "-" + code[4:]


def clean_user_code(text: str) -> str:
    """What a person typed, made comparable: capitals, with spaces and dashes dropped."""
    return "".join(c for c in text.upper() if c.isalnum())
