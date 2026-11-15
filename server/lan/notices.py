"""Server push for the optional headset socket (P3-NET-12): who is listening for which session, and a thread-safe way to tell them.

Nothing here is needed for play: request and response stay the default, and a headset that never opens the socket (or has it blocked) loses only the early warning."""
from __future__ import annotations

import asyncio
import threading

from server.schemas.headset_notice import Notice

MAX_QUEUE = 50


class NoticeHub:
    def __init__(self) -> None:
        self._lock = threading.Lock()
        self._listeners: list[tuple[str, asyncio.AbstractEventLoop, asyncio.Queue]] = []

    def attach(self, session_id: str) -> asyncio.Queue:
        queue: asyncio.Queue = asyncio.Queue(maxsize=MAX_QUEUE)
        with self._lock:
            self._listeners.append((session_id, asyncio.get_running_loop(), queue))
        return queue

    def detach(self, queue: asyncio.Queue) -> None:
        with self._lock:
            self._listeners = [x for x in self._listeners if x[2] is not queue]

    def listeners(self, session_id: str | None = None) -> int:
        with self._lock:
            return sum(1 for s, _, _ in self._listeners if session_id in (None, s))

    def publish(self, notice: Notice, to_all: bool = False) -> int:
        """Sends the notice to the listeners of its session (all sessions when to_all). Callable from any thread; a full queue drops the notice. Returns how many listeners were told."""
        with self._lock:
            targets = [(loop, q) for s, loop, q in self._listeners if to_all or s == notice.session_id]
        for loop, queue in targets:
            try:
                loop.call_soon_threadsafe(_put, queue, notice)
            except RuntimeError:   # that connection's loop is already closed
                pass
        return len(targets)

    def shutdown(self) -> int:
        return self.publish(Notice(kind="shutdown", session_id="*", detail="server_stopping"), to_all=True)


def _put(queue: asyncio.Queue, notice: Notice) -> None:
    if not queue.full():
        queue.put_nowait(notice)


_hub = NoticeHub()


def get_hub() -> NoticeHub:
    return _hub
