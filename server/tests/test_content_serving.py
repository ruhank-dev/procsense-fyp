"""Serving approved content packages to the headset (DT-131)."""
from __future__ import annotations

import hashlib
import io
import json
import uuid
import zipfile

import pytest

from server import config
from server.tests.test_sessions_api import setup  # noqa: F401  (two organisations and their people)

pytestmark = pytest.mark.db
PRACTICE, ASSESSMENT, DEV = "0123456789ab-practice", "0123456789ab-assessment", "0123456789ab"


@pytest.fixture
def content(tmp_path, monkeypatch):
    for name, files in ((PRACTICE, {"manifest.json": json.dumps({"content_hash": "h" * 64, "entries": {}}), "cases/A.json": "{}"}), (ASSESSMENT, {"manifest.json": json.dumps({"content_hash": "h" * 64}), "cases/B.json": "{}"}),
                        (DEV, {"manifest.json": "{}", "cases/dev.json": "{}"}), ("not-a-version", {"manifest.json": "{}"})):
        for rel, text in files.items():
            p = tmp_path / name / rel
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(text, encoding="utf-8")
    monkeypatch.setenv("ENGINE_CONTENT_ROOT", str(tmp_path))
    config.get_settings.cache_clear()
    yield tmp_path
    config.get_settings.cache_clear()


def test_the_versions_are_listed_without_the_development_folder(api, setup, content):
    got = api.client.get("/content/versions", headers=api.headers(setup.trainee)).json()["versions"]
    assert [(v["version"], v["mode"], v["files"]) for v in got] == [(ASSESSMENT, "assessment", 2), (PRACTICE, "practice", 2)]


def test_the_archive_is_the_same_bytes_every_time_and_holds_the_files(api, setup, content):
    first = api.client.get(f"/content/{PRACTICE}/archive", headers=api.headers(setup.trainee))
    second = api.client.get(f"/content/{PRACTICE}/archive", headers=api.headers(setup.trainee))
    assert first.status_code == 200 and first.content == second.content
    assert first.headers["x-content-sha256"] == hashlib.sha256(first.content).hexdigest()
    assert sorted(zipfile.ZipFile(io.BytesIO(first.content)).namelist()) == ["cases/A.json", "manifest.json"]
    assert api.client.get(f"/content/{PRACTICE}/manifest", headers=api.headers(setup.trainee)).json()["content_hash"] == "h" * 64


def test_only_named_versions_are_served_and_only_to_members(api, setup, content):
    for bad in (DEV, "not-a-version", "..%2F..%2Fetc", "0123456789ab-practice%2F..%2F"):
        assert api.client.get(f"/content/{bad}/archive", headers=api.headers(setup.trainee)).status_code == 404
    assert api.client.get(f"/content/{PRACTICE}/archive", headers=api.headers(uuid.uuid4())).status_code == 403
    assert api.client.get(f"/content/{PRACTICE}/archive").status_code == 401
    assert api.client.get("/content/versions").status_code == 401


def test_without_a_content_folder_the_answer_is_503(api, setup, monkeypatch):
    monkeypatch.setenv("ENGINE_CONTENT_ROOT", "")
    config.get_settings.cache_clear()
    assert api.client.get("/content/versions", headers=api.headers(setup.trainee)).status_code == 503
    config.get_settings.cache_clear()
