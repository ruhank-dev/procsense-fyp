"""The headset stand-in (p1-backend-s13): it signs in with a code, uploads a session file and fetches progress, and stops with a plain sentence when the chain breaks."""
from __future__ import annotations

import pytest

from server.repositories import accounts
from server.scripts import headset_sim
from server.tests.files import make_file

pytestmark = pytest.mark.db


@pytest.fixture
def fast_poll(monkeypatch):
    monkeypatch.setenv("DEVICE_POLL_SECONDS", "1")   # asked for before the `api` fixture builds the service


def approver(api, who, approve=True):
    def show(code: str, address: str) -> None:
        assert address.endswith("?code=" + code) and "/device" in address
        r = api.client.post("/auth/device/decision", json={"user_code": code, "approve": approve}, headers=api.headers(who))
        assert r.status_code == 200
    return show


def organisation_with(api, *, consent=True):
    def build(s):
        org = accounts.create_organisation(s, "Gamma", "gamma-org")
        who = api.person(s, consent=consent)
        accounts.add_membership(s, who, org.id, "trainee")
        return org.id, who
    return api.seed(build)


def test_a_session_goes_from_the_simulated_headset_to_the_service_and_progress_comes_back(fast_poll, api, tmp_path):
    org, who = organisation_with(api)
    file = tmp_path / "session.jsonl"
    file.write_text(make_file(), encoding="utf-8")
    sleeps = []
    result = headset_sim.run(api.client, file, "gamma-org", approver(api, who), wait_seconds=0, sleep=sleeps.append)
    assert result["organisation"] == "gamma-org" and result["upload"] == "accepted" and result["status"]["status"] == "pending"   # nothing verifies it here: the engine is not configured in this test
    assert result["progress"]["sessions"] == 0 and sleeps and sleeps[0] == 1.0
    again = headset_sim.run(api.client, file, None, approver(api, who), wait_seconds=0, sleep=lambda s: None)
    assert again["upload"] == "already_stored" and again["session_id"] == result["session_id"]   # the same file twice is stored once


def test_a_person_who_has_not_agreed_gets_a_sentence_telling_them_what_to_do(fast_poll, api, tmp_path):
    org, who = organisation_with(api, consent=False)
    file = tmp_path / "session.jsonl"
    file.write_text(make_file(), encoding="utf-8")
    with pytest.raises(headset_sim.SimError, match="tick the box"):
        headset_sim.run(api.client, file, None, approver(api, who), wait_seconds=0, sleep=lambda s: None)


def test_a_denied_code_stops_the_chain(fast_poll, api, tmp_path):
    org, who = organisation_with(api)
    file = tmp_path / "session.jsonl"
    file.write_text(make_file(), encoding="utf-8")
    with pytest.raises(headset_sim.SimError, match="denied"):
        headset_sim.run(api.client, file, None, approver(api, who, approve=False), wait_seconds=0, sleep=lambda s: None)


def test_an_account_in_no_organisation_or_the_wrong_one_is_told_so(fast_poll, api, tmp_path):
    org, who = organisation_with(api)
    stranger = api.seed(lambda s: api.person(s))
    file = tmp_path / "session.jsonl"
    file.write_text(make_file(), encoding="utf-8")
    with pytest.raises(headset_sim.SimError, match="belongs to no organisation"):
        headset_sim.run(api.client, file, None, approver(api, stranger), wait_seconds=0, sleep=lambda s: None)
    with pytest.raises(headset_sim.SimError, match="not a member of 'other-org'"):
        headset_sim.run(api.client, file, "other-org", approver(api, who), wait_seconds=0, sleep=lambda s: None)
