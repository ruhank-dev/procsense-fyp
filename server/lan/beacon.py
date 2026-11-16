"""P3-NET-06: the laptop's discovery beacon for the networked Quest demonstration.

While the server runs in the LAN profile, the laptop broadcasts one small UDP datagram a second on port 47800 so a headset on the same Wi-Fi can list it without typing the address.
The datagram carries no secret (the pairing code is still typed on the headset): the service name, the server address, the API version and the laptop's name. It is sent only when
LAN_PROFILE is on, and never on the hosted service. Manual entry and `adb reverse` remain.
"""
from __future__ import annotations

import json
import socket
import threading

PORT = 47800
SERVICE = "procsense"
INTERVAL = 1.0


def message(url: str, api_version: int = 1, name: str | None = None) -> bytes:
    return json.dumps({"service": SERVICE, "url": url, "api_version": api_version, "name": name or socket.gethostname()}, separators=(",", ":")).encode("utf-8")


def parse(data: bytes) -> dict | None:
    """The beacon a headset would read from a datagram, or None for anything else."""
    try:
        doc = json.loads(data.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None
    if not isinstance(doc, dict) or doc.get("service") != SERVICE or not isinstance(doc.get("url"), str):
        return None
    return doc


class Beacon:
    """Broadcasts the addresses once a second on a daemon thread until stopped."""

    def __init__(self, urls: list[str], port: int = PORT, interval: float = INTERVAL, target: str = "255.255.255.255"):
        self.urls, self.port, self.interval, self.target = urls, port, interval, target
        self.sent = 0
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None

    def send_once(self, sock: socket.socket) -> None:
        for url in self.urls:
            sock.sendto(message(url), (self.target, self.port))
            self.sent += 1

    def _run(self) -> None:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            while not self._stop.is_set():
                try:
                    self.send_once(sock)
                except OSError:
                    pass   # no network for a moment: try again next second
                self._stop.wait(self.interval)

    def start(self) -> "Beacon":
        self._thread = threading.Thread(target=self._run, name="procsense-beacon", daemon=True)
        self._thread.start()
        return self

    def stop(self) -> None:
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=2)
