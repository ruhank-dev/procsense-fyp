"""Request ids in the server's log (P3-API-05). The headset numbers every request (LanHttp sends `X-Request-Id`) and may put a `request_id` in a spoken turn's body; the speech, order and dialogue routes write
one line per call to `procsense.requests` naming the route, the id and whether the same person sent the same id to the same route moments ago (a retry or a double press). The line carries no words, no user
id and no token. A duplicate is only marked: refusing it is the job of the headset's gateway and the engine (one `submit_patient`, single-use order tickets), so a retry after a lost answer still gets one."""
from __future__ import annotations

import hashlib
import logging
import re
import threading
from collections import OrderedDict

from server.schemas.voice_turn import REQUEST_ID_PATTERN, TurnFields

log = logging.getLogger("procsense.requests")
_VALID = re.compile(REQUEST_ID_PATTERN)
WINDOW = 512                                  # how many recent (person, route, id) keys are remembered


class RecentIds:
    def __init__(self, size: int = WINDOW):
        self._size, self._seen, self._lock = size, OrderedDict(), threading.Lock()

    def seen(self, key: str) -> bool:
        """True when this key was noted before (and still remembered); notes it either way."""
        with self._lock:
            if key in self._seen:
                self._seen.move_to_end(key)
                return True
            self._seen[key] = None
            if len(self._seen) > self._size:
                self._seen.popitem(last=False)
            return False

    def clear(self) -> None:
        with self._lock:
            self._seen.clear()


RECENT = RecentIds()


def choose(body_id: str | None, header_id: str | None) -> str:
    """The body's id when given (already validated by the schema), else a well-formed header id, else empty."""
    if body_id:
        return body_id
    return header_id if header_id and _VALID.match(header_id) else ""


def note(route: str, user_id, fields: TurnFields | None, header_id: str | None) -> str:
    """Writes the line and returns the id used ("" when the client sent none)."""
    request_id = choose(fields.request_id if fields else None, header_id)
    if not request_id:
        log.info("request route=%s request_id=- duplicate=false", route)
        return ""
    who = hashlib.sha256(str(user_id).encode("utf-8")).hexdigest()[:16]
    duplicate = RECENT.seen(f"{who}|{route}|{request_id}")
    log.info("request route=%s request_id=%s duplicate=%s interaction_mode=%s session_mode=%s", route, request_id, str(duplicate).lower(),
             (fields.interaction_mode if fields else None) or "-", (fields.session_mode if fields else None) or "-")
    return request_id
