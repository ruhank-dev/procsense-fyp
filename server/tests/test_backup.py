"""The backup (p1-backend-s13, DT-160): the encryption refuses a wrong key, a changed file and a cut-off file; a real dump of a migrated database restores into a throwaway PostgreSQL with the same row counts; a suspiciously small backup fails the run."""
from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path

import pytest

from server.scripts import backup

KEY = "a-test-key-that-is-long-enough-1234"   # test value, protects nothing


def _docker_ready() -> bool:
    try:
        return subprocess.run(["docker", "info"], capture_output=True).returncode == 0
    except FileNotFoundError:
        return False


needs_docker = pytest.mark.skipif(not _docker_ready(), reason="Docker is not running: the backup tools are PostgreSQL's own, run from the postgres:17 image")


def test_encryption_round_trip_over_several_pieces(tmp_path: Path):
    data = bytes(range(256)) * 9000          # about 2.3 MiB: three pieces
    (tmp_path / "plain").write_bytes(data)
    backup.encrypt_file(tmp_path / "plain", tmp_path / "enc", KEY)
    assert data[:100] not in (tmp_path / "enc").read_bytes()
    backup.decrypt_file(tmp_path / "enc", tmp_path / "back", KEY)
    assert (tmp_path / "back").read_bytes() == data


def test_empty_input_round_trips(tmp_path: Path):
    (tmp_path / "plain").write_bytes(b"")
    backup.encrypt_file(tmp_path / "plain", tmp_path / "enc", KEY)
    backup.decrypt_file(tmp_path / "enc", tmp_path / "back", KEY)
    assert (tmp_path / "back").read_bytes() == b""


def test_a_wrong_key_a_changed_byte_and_a_cut_file_are_refused(tmp_path: Path):
    (tmp_path / "plain").write_bytes(b"x" * (2 * backup.PIECE + 5))
    backup.encrypt_file(tmp_path / "plain", tmp_path / "enc", KEY)
    good = (tmp_path / "enc").read_bytes()
    with pytest.raises(backup.BackupError, match="key is wrong or the file was changed"):
        backup.decrypt_file(tmp_path / "enc", tmp_path / "out", KEY + "x")
    changed = bytearray(good); changed[len(good) // 2] ^= 1
    (tmp_path / "changed").write_bytes(bytes(changed))
    with pytest.raises(backup.BackupError, match="changed"):
        backup.decrypt_file(tmp_path / "changed", tmp_path / "out", KEY)
    first_piece = len(backup.MAGIC) + 16 + 1 + 4 + backup.PIECE + 16
    (tmp_path / "cut").write_bytes(good[:first_piece])           # cut cleanly after a whole piece
    with pytest.raises(backup.BackupError, match="cut off"):
        backup.decrypt_file(tmp_path / "cut", tmp_path / "out", KEY)
    (tmp_path / "other").write_bytes(b"not a backup at all")
    with pytest.raises(backup.BackupError, match="not a ProcSense backup"):
        backup.decrypt_file(tmp_path / "other", tmp_path / "out", KEY)


def test_a_short_key_is_refused(tmp_path: Path):
    (tmp_path / "plain").write_bytes(b"x")
    with pytest.raises(backup.BackupError, match="BACKUP_KEY"):
        backup.encrypt_file(tmp_path / "plain", tmp_path / "enc", "short")


def test_a_much_smaller_backup_is_not_trusted(tmp_path: Path):
    (tmp_path / "x.manifest.json").write_text(json.dumps({"bytes": 1000, "rows": 500}))
    assert backup.compare_with_previous({"bytes": 900, "rows": 480}, tmp_path) is None
    assert "less than half" in backup.compare_with_previous({"bytes": 400, "rows": 480}, tmp_path)
    assert "rows" in backup.compare_with_previous({"bytes": 900, "rows": 100}, tmp_path)
    assert backup.compare_with_previous({"bytes": 1, "rows": 0}, tmp_path / "nothing") is None   # the first backup has nothing to compare with


@pytest.mark.db
@needs_docker
def test_a_real_dump_restores_into_a_throwaway_database_with_the_same_counts(api, db_url, tmp_path: Path):
    def seed(session):
        for _ in range(5):
            api.person(session)
    api.seed(seed)

    manifest = backup.dump(db_url, tmp_path, KEY)
    assert manifest["tables"]["profile"] == 5 and manifest["tables"]["consent_record"] == 10
    assert manifest["migration"] and manifest["bytes"] > 0
    file = tmp_path / manifest["file"]
    assert b"PGDMP" not in file.read_bytes()                 # the dump itself is not readable in the stored file
    assert not list(tmp_path.glob("*.dump"))                 # the plain dump is not left behind

    result = backup.drill(file, KEY, tmp_path)
    assert result["restored"] and result["rows"] == manifest["rows"] and result["tables"] == len(manifest["tables"])

    with pytest.raises(backup.BackupError, match="key is wrong"):
        backup.drill(file, KEY + "x", tmp_path)
    assert not list(tmp_path.glob("drill-*"))                # no plain copy is left behind after a failure either

    # a database that lost most of its rows since the last backup fails the run
    previous = tmp_path / "previous"; previous.mkdir()
    (previous / "old.manifest.json").write_text(json.dumps({"bytes": manifest["bytes"] * 10, "rows": 1000}))
    with pytest.raises(backup.BackupError, match="less than half"):
        backup.dump(db_url, tmp_path / "second", KEY, previous)


@pytest.mark.db
def test_the_monitor_reports_size_and_warns_over_eighty_percent(migrated, db_url, monkeypatch):
    report = backup.monitor(db_url)
    assert report["limit_mb"] == 500 and 0 < report["database_mb"] < 100 and report["connections"] >= 1 and report["warn"] is False
    monkeypatch.setattr(backup, "FREE_LIMIT_BYTES", 1024 * 1024)       # a 1 MB "limit": the test database is over it
    assert backup.monitor(db_url)["warn"] is True
