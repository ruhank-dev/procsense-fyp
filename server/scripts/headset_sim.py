"""A stand-in for the headset's server half (p1-backend-s13): signs in with a code, uploads a session file, waits for the verdict and fetches the person's progress.

    uv run --project server python -m server.scripts.headset_sim --url https://procsense-xxxx.onrender.com --file demo_session.jsonl [--org procsense-demo] [--wait 180]

It does what the VR application will do, so the whole chain (sign in, upload, verification, dashboard) can be shown and tested before the headset code exists, and a developer can try the service from a laptop.
It prints a code and an address; open the address in a browser where you are signed in and approve the code. Nothing secret is printed, stored or taken from the command line: the tokens live only in this process.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from collections.abc import Callable
from pathlib import Path

import httpx


class SimError(Exception):
    """The chain broke at a named step; the message says where and why in words a person can act on."""


def _json(response: httpx.Response) -> dict:
    try:
        return response.json()
    except ValueError:
        return {}


def sign_in(client: httpx.Client, show_code: Callable[[str, str], None], sleep: Callable[[float], None] = time.sleep, give_up_after: float = 600.0) -> str:
    """The device-code flow: ask for a code, show it, poll until a person approves it. Returns the access token."""
    started = client.post("/auth/device/start", json={"device_name": "Headset simulator"})
    if started.status_code != 200:
        raise SimError(f"could not start a sign-in ({started.status_code})")
    info = started.json()
    show_code(info["user_code"], info["verification_uri_complete"])
    interval, waited = float(info["interval"]), 0.0
    while waited <= min(give_up_after, float(info["expires_in"])):
        sleep(interval)
        waited += interval
        r = client.post("/auth/device/token", json={"device_code": info["device_code"]})
        if r.status_code == 200:
            return r.json()["access_token"]
        error = _json(r).get("detail")
        if error == "authorization_pending":
            continue
        if error == "slow_down":
            interval += 5
            continue
        if error == "access_denied":
            raise SimError("the code was denied on the web page")
        raise SimError("the code expired before anyone approved it" if error == "expired_token" else f"sign-in failed ({r.status_code} {error})")
    raise SimError("nobody approved the code in time")


def pick_organisation(client: httpx.Client, headers: dict, slug: str | None) -> dict:
    r = client.get("/me", headers=headers)
    if r.status_code != 200:
        raise SimError(f"could not read the account ({r.status_code})")
    active = [m for m in r.json()["memberships"] if m["status"] == "active"]
    if not active:
        raise SimError("this account belongs to no organisation yet: an administrator has to invite it")
    chosen = next((m for m in active if m["slug"] == slug), None) if slug else active[0]
    if chosen is None:
        raise SimError(f"this account is not a member of '{slug}' (it is in: {', '.join(m['slug'] for m in active)})")
    return chosen


def upload(client: httpx.Client, headers: dict, organisation_id: str, text: str) -> dict:
    r = client.post(f"/organisations/{organisation_id}/sessions", content=text.encode("utf-8"), headers={**headers, "Content-Type": "application/x-ndjson"})
    detail = _json(r).get("detail")
    if r.status_code == 403 and isinstance(detail, dict) and detail.get("code") == "consent_required":
        raise SimError("this person has not agreed to the storing of their results yet: sign in on the web page once and tick the box")
    if r.status_code not in (200, 202):
        raise SimError(f"the upload was refused ({r.status_code} {detail})")
    return r.json()


def wait_for_verdict(client: httpx.Client, headers: dict, organisation_id: str, session_id: str, seconds: float, sleep: Callable[[float], None] = time.sleep) -> dict:
    deadline = time.monotonic() + seconds
    while True:
        r = client.get(f"/organisations/{organisation_id}/sessions/{session_id}", headers=headers)
        if r.status_code != 200:
            raise SimError(f"could not read the session's status ({r.status_code})")
        status = r.json()
        if status["status"] != "pending" or time.monotonic() >= deadline:
            return status
        sleep(5)


def run(client: httpx.Client, file: Path, organisation_slug: str | None, show_code: Callable[[str, str], None], wait_seconds: float = 180, sleep: Callable[[float], None] = time.sleep) -> dict:
    text = file.read_text(encoding="utf-8")
    token = sign_in(client, show_code, sleep)
    headers = {"Authorization": f"Bearer {token}"}
    organisation = pick_organisation(client, headers, organisation_slug)
    sent = upload(client, headers, organisation["organisation_id"], text)
    session_id = sent["session"]["session_id"]
    status = wait_for_verdict(client, headers, organisation["organisation_id"], session_id, wait_seconds, sleep) if wait_seconds > 0 else sent["session"]
    progress = client.get(f"/organisations/{organisation['organisation_id']}/progress", headers=headers)
    return {"organisation": organisation["slug"], "upload": sent["outcome"], "session_id": session_id, "status": status, "progress": progress.json().get("summary") if progress.status_code == 200 else None}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--url", required=True, help="the service address, e.g. https://procsense-xxxx.onrender.com")
    parser.add_argument("--file", required=True, type=Path, help="a session file written by the engine (see task demo-session)")
    parser.add_argument("--org", help="the organisation's short name when the account is in several")
    parser.add_argument("--wait", type=float, default=180, help="seconds to wait for the verdict (0 = do not wait)")
    args = parser.parse_args(argv)

    def show(code: str, address: str) -> None:
        print(f"\nOpen  {address}\nsign in on that page, and approve the code  {code}\n(waiting for you to approve it)\n", flush=True)

    with httpx.Client(base_url=args.url.rstrip("/"), timeout=120.0) as client:   # the first call can take a minute while the free service wakes
        try:
            result = run(client, args.file, args.org, show, args.wait)
        except SimError as e:
            print(f"STOPPED: {e}")
            return 1
    print(json.dumps(result, indent=1, default=str))
    return 0 if result["status"]["status"] in ("pending", "verified") else 2


if __name__ == "__main__":
    sys.exit(main())
