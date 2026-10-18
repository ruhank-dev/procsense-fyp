"""The read-back ticket of the spoken-order route (features C32, C35): a hearing the trainee has not yet confirmed, signed so the server can stay stateless between the two calls.

    t = issue(user_id, hearing)               # after POST /orders/.../hear: the client keeps this text
    p = open_ticket(t, user_id)               # on POST /orders/.../confirm: the claims, or a TicketError naming why not

The ticket carries the intent, the slots and the action the hearing proposed, the user it was issued to, an expiry (TTL seconds) and a nonce; an HMAC-SHA256 over all of it uses a key made when the process starts (a restart
invalidates every open ticket, which is harmless: the trainee is asked again). A client therefore cannot change the action behind a read-back, move a ticket to another user, use it after it expired or use it twice
once it was decided (the nonce is remembered until it expires). Nothing here releases anything: the route releases an action only when the spoken answer is a yes (server/orders/grammar.py).
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import secrets
import threading
import time

TTL = 60.0
_KEY = secrets.token_bytes(32)
_used: dict[str, float] = {}
_lock = threading.Lock()


class TicketError(ValueError):
    pass


def _sign(payload: bytes) -> str:
    return base64.urlsafe_b64encode(hmac.new(_KEY, payload, hashlib.sha256).digest()).decode("ascii").rstrip("=")


def issue(user_id: str, intent: str | None, slots: dict, action: dict | None, now: float | None = None) -> str:
    now = time.time() if now is None else now
    body = json.dumps({"u": str(user_id), "i": intent, "s": slots, "a": action, "x": round(now + TTL, 3), "n": secrets.token_hex(8)}, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return base64.urlsafe_b64encode(body).decode("ascii").rstrip("=") + "." + _sign(body)


def open_ticket(ticket: str, user_id: str, now: float | None = None) -> dict:
    now = time.time() if now is None else now
    try:
        body_b64, sig = ticket.split(".", 1)
        body = base64.urlsafe_b64decode(body_b64 + "=" * (-len(body_b64) % 4))
        claims = json.loads(body)
    except (ValueError, UnicodeError):
        raise TicketError("TICKET_MALFORMED") from None
    if not hmac.compare_digest(sig, _sign(body)):
        raise TicketError("TICKET_FORGED")
    if claims["u"] != str(user_id):
        raise TicketError("TICKET_NOT_YOURS")
    if now > claims["x"]:
        raise TicketError("TICKET_EXPIRED")
    with _lock:
        for n in [n for n, x in _used.items() if x < now]:
            del _used[n]
        if claims["n"] in _used:
            raise TicketError("TICKET_ALREADY_DECIDED")
    return claims


def decide(claims: dict) -> None:
    """Remember that this ticket was answered (a yes or a no), so it cannot be answered again."""
    with _lock:
        _used[claims["n"]] = claims["x"]
