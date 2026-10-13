"""A small per-person rate limit (p1-backend-s02). In memory, so it is per running instance: enough for one free-plan container; a second instance would need a shared store (a paid-plan concern)."""
from __future__ import annotations

import time
from collections import defaultdict, deque
from collections.abc import Callable


class RateLimiter:
    def __init__(self, limit: int, window_seconds: float = 3600.0, clock: Callable[[], float] = time.monotonic):
        self.limit, self.window, self._clock = limit, window_seconds, clock
        self._hits: dict[object, deque[float]] = defaultdict(deque)

    def allow(self, key: object) -> bool:
        """Counts one use for `key`; False when `key` has already used up its allowance inside the window."""
        now = self._clock()
        hits = self._hits[key]
        while hits and now - hits[0] > self.window:
            hits.popleft()
        if len(hits) >= self.limit:
            return False
        hits.append(now)
        return True
