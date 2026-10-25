"""The generation history table (DT-201): written when a session is verified, read as the generator's own entries, exported with the person and deleted with them."""
from __future__ import annotations

import pytest
import uuid
from sqlalchemy import func, select
from sqlalchemy.orm import Session as DbSession

from server.generation import history as H
from server.generation import objectives as ob
from server.models.generation_history import GenerationHistory
from server.models.sessions import Session
from server.repositories import generation_history as table
from server.repositories import privacy as privacy_repository
from server.tests.test_verification import sweep, world  # noqa: F401  (an organisation, a trainee and the stand-in engine)

pytestmark = pytest.mark.db


def rows(world, sid=None):
    with DbSession(world.api.engine) as s:
        q = select(GenerationHistory)
        if sid:
            q = q.where(GenerationHistory.session_id == sid)
        return s.scalars(q).all()


def test_a_verified_session_is_remembered_with_its_behaviours_and_no_text(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    sid = world.upload()
    assert rows(world) == []                      # nothing before the verdict
    sweep(world)
    (row,) = rows(world, sid)
    assert (row.user_id, row.organisation_id, row.tier, row.package_id, row.fingerprint) == (world.trainee, world.org, "P0", "", "")
    assert row.behaviours == {"b16": [1, 0], "b13": [0, 1]}      # a behaviour with no items is not kept
    assert row.case_id == row.story and row.ended_at.tzinfo is not None


def test_verifying_again_replaces_the_row_and_a_rejected_session_leaves_none(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    sid = world.upload()
    sweep(world)
    with DbSession(world.api.engine) as s:
        s.get(Session, sid).status = "pending"
        s.commit()
    sweep(world)
    assert len(rows(world, sid)) == 1
    monkeypatch.setenv("FAKE_ENGINE_MODE", "hash_mismatch")
    other = world.upload()
    sweep(world)
    assert rows(world, other) == []


def test_the_entries_are_the_generators_own_type_and_feed_the_objectives(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    world.upload()
    sweep(world)
    with DbSession(world.api.engine) as s:
        pseudonym = s.scalar(select(__import__("server.models.accounts", fromlist=["Profile"]).Profile.pseudonym).where(__import__("server.models.accounts", fromlist=["Profile"]).Profile.user_id == world.trainee))
        got = table.entries(s, world.trainee, pseudonym)
    assert len(got) == 1 and isinstance(got[0], H.HistoryEntry) and got[0].trainee == pseudonym
    assert H.session_records(got)[0].tier == "P0" and H.session_records(got)[0].behaviours == {"b16": (1, 0), "b13": (0, 1)}
    assert H.recent_fingerprints(got) == [] and H.novelty_history(got) == []
    assert isinstance(H.session_records(got)[0], ob.SessionRecord)


def test_a_generated_session_keeps_its_package_fingerprint_and_levels(world):
    sid = "6f1c1c1e-0000-4000-8000-000000000001"
    entry = H.HistoryEntry("T-ABCD2345", sid, "GEN-1", "SC-4", "P1", {"b01": [2, 1]}, "2026-10-26T08:00:00Z", "pkg-1", "a" * 64, "fp-1", {"patient": ["a", "b"]})
    with DbSession(world.api.engine) as s:
        table.record(s, world.org, world.trainee, entry)
        table.record(s, world.org, world.trainee, entry)        # twice: still one row
        s.commit()
        back = table.entries(s, world.trainee, "T-ABCD2345")
    assert back == [entry]
    assert H.recent_fingerprints(back) == ["fp-1"] and H.novelty_history(back) == [{"patient": ["a", "b"]}]


def test_the_export_holds_the_entries_and_deleting_the_person_removes_them(world, monkeypatch):
    monkeypatch.setenv("FAKE_ENGINE_MODE", "ok")
    world.upload()
    sweep(world)
    with DbSession(world.api.engine) as s:
        from server.models.accounts import Profile
        data = privacy_repository.export_person(s, s.get(Profile, world.trainee))
        assert len(data["generation_history"]) == 1 and "trainee" not in data["generation_history"][0]
        privacy_repository.delete_person(s, world.trainee)
        s.commit()
        assert s.scalar(select(func.count()).select_from(GenerationHistory)) == 0


# --- DT-199: a package built on request from the person's own history ----------------------------------------------------------------------------------------------------------------

def test_a_package_is_built_on_request_stored_by_its_hash_and_steered_by_the_history(world, monkeypatch):
    from server import config
    from server.repositories import sessions as repository
    headers = world.api.headers(world.trainee)
    assert world.api.client.post("/scenario-packages/generate", json={"scenario": "S1", "level": "advanced"}, headers=headers).status_code == 503      # off by default
    monkeypatch.setenv("LIVE_GENERATION", "true")
    config.get_settings.cache_clear()
    first = world.api.client.post("/scenario-packages/generate", json={"scenario": "S1", "level": "advanced"}, headers=headers)
    assert first.status_code == 200, first.text
    offer = first.json()
    with DbSession(world.api.engine) as s:
        raw = repository.get_package(s, offer["package_hash"])
    assert raw is not None and offer["case_id"].startswith("GEN-")
    # the story just played is not offered again: record a session of that spine, then ask for the next one
    entry = H.HistoryEntry("T-ABCD2345", "6f1c1c1e-0000-4000-8000-000000000002", offer["case_id"], offer["spine"], "P0", {"b01": [1, 3]}, "2026-10-26T08:00:00Z")
    with DbSession(world.api.engine) as s:
        table.record(s, world.org, world.trainee, entry)
        s.commit()
    second = world.api.client.post("/scenario-packages/generate", json={"scenario": "S1", "level": "advanced"}, headers=headers)
    assert second.status_code == 200 and second.json()["spine"] != offer["spine"]
    assert world.api.client.post("/scenario-packages/generate", json={"scenario": "S1", "level": "advanced"}, headers=world.api.headers(uuid.uuid4())).status_code == 403
    config.get_settings.cache_clear()
