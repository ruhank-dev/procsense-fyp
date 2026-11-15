"""LAN pairing for the networked Quest demonstration (P3-NET-07).

The laptop shows a six-digit code. The headset sends it once and receives a random device token; only the token's hash is stored, so the file cannot be used to
call the server. Wrong codes are rate limited. A token can be revoked from the laptop. Nothing here is used unless Settings.lan_profile is on."""
from __future__ import annotations

import hashlib
import hmac
import json
import secrets
import threading
import time
from pathlib import Path

CODE_DIGITS = 6
MAX_WRONG = 5
LOCKOUT_SECONDS = 60.0


def _hash(token: str) -> str:
    return hashlib.sha256(token.encode("utf-8")).hexdigest()


class PairingStore:
    """In-memory pairing code plus a small JSON file of hashed device tokens, so a laptop restart keeps every paired headset."""

    def __init__(self, path: Path | str | None = None, clock=time.monotonic):
        self._path = Path(path) if path else None
        self._clock = clock
        self._lock = threading.Lock()
        self._code = self._new_code()
        self._wrong = 0
        self._locked_until = 0.0
        self._devices: dict[str, dict] = {}     # token hash -> {"id", "name"}
        if self._path and self._path.exists():
            try:
                self._devices = json.loads(self._path.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                self._devices = {}

    @staticmethod
    def _new_code() -> str:
        return f"{secrets.randbelow(10 ** CODE_DIGITS):0{CODE_DIGITS}d}"

    @property
    def code(self) -> str:
        return self._code

    def rotate_code(self) -> str:
        with self._lock:
            self._code = self._new_code()
            return self._code

    def _save(self) -> None:
        if self._path:
            self._path.parent.mkdir(parents=True, exist_ok=True)
            self._path.write_text(json.dumps(self._devices, indent=1), encoding="utf-8")

    def exchange(self, code: str, device_name: str = "headset") -> tuple[str, str] | None:
        """Returns (device_id, token) when the code is right, else None. Five wrong codes lock the exchange for a minute (guessing is not possible in practice)."""
        with self._lock:
            now = self._clock()
            if now < self._locked_until:
                return None
            if not hmac.compare_digest(str(code).strip(), self._code):
                self._wrong += 1
                if self._wrong >= MAX_WRONG:
                    self._wrong = 0
                    self._locked_until = now + LOCKOUT_SECONDS
                return None
            self._wrong = 0
            token = secrets.token_urlsafe(32)
            device_id = secrets.token_hex(4)
            self._devices[_hash(token)] = {"id": device_id, "name": str(device_name)[:40]}
            self._save()
            return device_id, token

    def locked(self) -> bool:
        return self._clock() < self._locked_until

    def verify(self, token: str) -> bool:
        return self.device_id_for(token) is not None

    def device_id_for(self, token: str) -> str | None:
        """The id of the paired headset this token belongs to, else None (P3-NET-11 derives the demonstration identity from it)."""
        entry = self._devices.get(_hash(token)) if token else None
        return entry["id"] if entry else None

    def devices(self) -> list[dict]:
        return list(self._devices.values())

    def revoke(self, device_id: str) -> bool:
        with self._lock:
            for key, value in list(self._devices.items()):
                if value["id"] == device_id:
                    del self._devices[key]
                    self._save()
                    return True
        return False


_store: PairingStore | None = None


def get_store() -> PairingStore:
    global _store
    if _store is None:
        from server.config import get_settings
        path = get_settings().lan_pairing_file
        _store = PairingStore(path or None)
    return _store


def reset_store() -> None:
    global _store
    _store = None
