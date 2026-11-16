"""P3-NET-06: the discovery beacon's datagram, its parsing, and one real broadcast received on the loopback."""
from __future__ import annotations

import socket

from server.lan import beacon


def test_message_carries_the_address_and_no_secret():
    doc = beacon.parse(beacon.message("http://192.168.1.20:8000", name="laptop"))
    assert doc == {"service": "procsense", "url": "http://192.168.1.20:8000", "api_version": 1, "name": "laptop"}
    assert "code" not in doc and "token" not in doc


def test_other_datagrams_are_ignored():
    assert beacon.parse(b"hello") is None
    assert beacon.parse(b'{"service":"other","url":"x"}') is None
    assert beacon.parse(b'{"service":"procsense"}') is None
    assert beacon.parse(b"\xff\xfe") is None


def test_a_beacon_is_received():
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as rx:
        rx.bind(("127.0.0.1", 0))
        rx.settimeout(3)
        port = rx.getsockname()[1]
        b = beacon.Beacon(["http://127.0.0.1:8000"], port=port, interval=0.1, target="127.0.0.1")
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as tx:
            b.send_once(tx)
        data, _ = rx.recvfrom(2048)
    assert beacon.parse(data)["url"] == "http://127.0.0.1:8000"
    assert b.sent == 1
