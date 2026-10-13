"""The web pages in a real browser (p1-backend-s11): sign in, see only what the role allows, open the overview and a report, approve a headset code, sign out.

The service runs for real on a local port with the pretend sign-in server and sample data of `browser_stack.py`. Needs Playwright and a browser: CI installs Chromium; on a laptop set BROWSER_CHANNEL=msedge (or chrome)
to use the browser that is already installed. Without one the test is skipped, unless REQUIRE_BROWSER=1 (CI), when a missing browser is an error.
"""
from __future__ import annotations

import os
import socket
import threading
import time

import pytest

from server.tests.browser_stack import HOSTILE, PASSWORD

pytestmark = pytest.mark.db


@pytest.fixture
def stack(db_url):
    from server import config, database
    saved = dict(os.environ)
    from server.tests import browser_stack
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    import uvicorn
    server = uvicorn.Server(uvicorn.Config(browser_stack.build(port, db_url), host="127.0.0.1", port=port, log_level="warning"))
    thread = threading.Thread(target=server.run, daemon=True)
    thread.start()
    for _ in range(100):
        if server.started:
            break
        time.sleep(0.1)
    assert server.started, "the service did not start"
    yield f"http://127.0.0.1:{port}"
    server.should_exit = True
    thread.join(timeout=10)
    os.environ.clear()
    os.environ.update(saved)
    config.get_settings.cache_clear()
    database.get_engine.cache_clear()


@pytest.fixture
def browser():
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        if os.environ.get("REQUIRE_BROWSER") == "1":
            pytest.fail("Playwright is not installed but REQUIRE_BROWSER=1")
        pytest.skip("Playwright is not installed")
    with sync_playwright() as p:
        try:
            launched = p.chromium.launch(channel=os.environ.get("BROWSER_CHANNEL") or None)
        except Exception as e:   # no browser to run
            if os.environ.get("REQUIRE_BROWSER") == "1":
                raise
            pytest.skip(f"no browser to run ({str(e).splitlines()[0]}); set BROWSER_CHANNEL=msedge or run `playwright install chromium`")
        yield launched
        launched.close()


def sign_in(page, base, email, path="/"):
    page.goto(base + path)
    page.get_by_label("Email").fill(email)
    page.get_by_label("Password").fill(PASSWORD)
    page.get_by_role("button", name="Sign in").click()


def links(page) -> list[str]:
    return page.locator("nav a").all_inner_texts()


def test_the_pages_work_in_a_real_browser(stack, browser):
    page = browser.new_page()
    dialogs, errors = [], []
    page.on("dialog", lambda d: (dialogs.append(d.message), d.dismiss()))
    page.on("pageerror", lambda e: errors.append(str(e)))
    base = stack

    # a wrong password is refused with a plain message and nothing else happens
    page.goto(base + "/")
    page.get_by_label("Email").fill("trainee@alpha.test")
    page.get_by_label("Password").fill("not-the-password")
    page.get_by_role("button", name="Sign in").click()
    page.get_by_text("Email or password is not right.").wait_for()

    # a trainee: own pages only, and asking for the overview by address shows their own progress
    sign_in(page, base, "trainee@alpha.test")
    page.get_by_role("heading", name="My progress").wait_for()
    assert links(page) == ["My progress", "Sessions", "Sign in a headset", "Privacy"]
    page.goto(base + "/")      # a reload signs a person out: the token lives in memory only
    page.get_by_role("heading", name="Sign in").wait_for()
    sign_in(page, base, "trainee@alpha.test")
    page.get_by_role("heading", name="My progress").wait_for()
    page.evaluate("location.hash = '#/overview'")
    page.get_by_role("heading", name="My progress").wait_for()
    page.get_by_role("link", name="Sessions").click()
    page.get_by_role("heading", name="My sessions").wait_for()
    assert page.locator("main td", has_text="T-").count() == 0   # a trainee's own list has no 'Person' column

    # a supervisor: the overview of their own organisation, names shown as text, a report in a sandboxed frame
    page.get_by_role("button", name="Sign out").click()
    page.get_by_role("heading", name="Sign in").wait_for()
    sign_in(page, base, "supervisor@alpha.test")
    page.get_by_role("heading", name="My progress").wait_for()
    assert links(page) == ["My progress", "Sessions", "Overview", "Sign in a headset", "Privacy"]
    page.get_by_role("link", name="Overview").click()
    page.get_by_role("heading", name="Overview of Alpha Hospital").wait_for()
    assert page.locator(".tile .n").first.inner_text() == "9"   # the nine of this organisation; the two of the other one are not counted
    page.get_by_role("link", name="Sessions").click()
    page.get_by_role("heading", name="Sessions in the organisation").wait_for()
    assert page.locator("main td", has_text=HOSTILE).count() >= 1 and page.locator("main img").count() == 0
    page.get_by_role("link", name="Open report").first.click()
    page.get_by_role("heading", name="Report: VAR-S1-A").wait_for()
    frame = page.locator("iframe.report")
    frame.wait_for()
    assert frame.get_attribute("sandbox") == "" and "ran" != page.title()   # the script inside the report did not run
    page.get_by_role("button", name="Sign out").click()

    # an organisation administrator also sees People
    sign_in(page, base, "admin@alpha.test")
    page.get_by_role("heading", name="My progress").wait_for()
    assert links(page) == ["My progress", "Sessions", "Overview", "People", "Sign in a headset", "Privacy"]
    page.get_by_role("link", name="People").click()
    page.get_by_role("heading", name="People").wait_for()
    assert page.locator("main img").count() == 0
    page.get_by_role("button", name="Sign out").click()

    # another organisation sees only its own figures (T-BE5 through the browser)
    sign_in(page, base, "supervisor@beta.test")
    page.get_by_role("heading", name="My progress").wait_for()
    page.get_by_role("link", name="Overview").click()
    page.get_by_role("heading", name="Overview of Beta Clinic").wait_for()
    assert page.locator(".tile .n").first.inner_text() == "2"
    page.get_by_role("button", name="Sign out").click()

    # a person sees who opened their report (the supervisor above did)
    sign_in(page, base, "trainee@alpha.test")
    page.get_by_role("heading", name="My progress").wait_for()
    page.get_by_role("link", name="Privacy").click()
    page.get_by_role("heading", name="Privacy").wait_for()
    page.get_by_text("Opened a session report").wait_for()
    page.get_by_role("button", name="Sign out").click()

    # a person who has not agreed yet is asked first and cannot skip the required question; then download and delete their data
    sign_in(page, base, "colleague@alpha.test")
    page.get_by_role("heading", name="Your data and your choices").wait_for()
    page.get_by_role("button", name="Save my choices").click()
    page.get_by_text("You can use ProcSense only if you agree").wait_for()
    page.get_by_label("needed to store my sessions").check()
    page.get_by_role("button", name="Save my choices").click()
    page.get_by_role("heading", name="My progress").wait_for()
    page.get_by_role("link", name="Privacy").click()
    page.get_by_role("heading", name="Privacy").wait_for()
    with page.expect_download() as download:
        page.get_by_role("button", name="Download my data").click()
    assert download.value.suggested_filename == "my-procsense-data.json"
    page.get_by_label("Type DELETE to confirm").fill("DELETE")
    page.get_by_role("button", name="Delete my data").click()
    page.get_by_text("Your data was deleted and you were signed out.").wait_for()
    page.get_by_role("heading", name="Sign in").wait_for()

    # a headset code approved on the web is collected by the headset
    started = page.request.post(base + "/auth/device/start", data={"device_name": "Quest test"}).json()
    sign_in(page, base, "trainee@alpha.test", path="/device?code=" + started["user_code"])
    page.get_by_role("heading", name="Sign in a headset").wait_for()
    assert page.get_by_label("Code from the headset").input_value() == started["user_code"]
    page.get_by_role("button", name="Approve").click()
    page.get_by_text('Approved. "Quest test" is signing in as you.').wait_for()
    time.sleep(started["interval"] + 0.5)
    tokens = page.request.post(base + "/auth/device/token", data={"device_code": started["device_code"]})
    assert tokens.status == 200 and tokens.json()["access_token"].startswith("psd_")

    assert dialogs == [] and errors == []
    page.close()
