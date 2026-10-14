"""Backup, restore and monitor of the hosted database (p1-backend-s13, DT-160). The free plan keeps no backups, so this is the only copy.

    uv run --project server python -m server.scripts.backup dump --url <session pooler address> --out backups [--previous backups_old]
    uv run --project server python -m server.scripts.backup drill --file backups/procsense-....dump.enc
    uv run --project server python -m server.scripts.backup restore --file backups/procsense-....dump.enc --url <empty database>
    uv run --project server python -m server.scripts.backup monitor --url <session pooler address>

The key is read from the environment variable BACKUP_KEY (never from a command line). What is in the backup: the application tables of the schema `public` (people, sessions, scores, consent, access log, migration version).
Accounts and passwords are Supabase's own and are not in it (DT-130). PostgreSQL's own tools are run from the `postgres:17` image with Docker, so no PostgreSQL has to be installed (the same version as the hosted database).
The file is encrypted (AES-256-GCM in 1 MiB pieces; the key is derived from BACKUP_KEY with scrypt), so a stolen artifact is unreadable, and a changed or cut-off file is refused rather than restored half.
"""
from __future__ import annotations

import argparse
import json
import os
import secrets
import struct
import subprocess
import sys
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path

import psycopg
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.kdf.scrypt import Scrypt

PG_IMAGE = "postgres:17"
MAGIC = b"PSBK1"
PIECE = 1024 * 1024
FREE_LIMIT_BYTES = 500 * 1024 * 1024
WARN_AT = 0.8
SHRINK_LIMIT = 0.5          # a dump under half the size of the last one is not trusted
MIN_ROWS_TO_COMPARE = 100


class BackupError(Exception):
    pass


# ---- encryption ----------------------------------------------------------------------------------------------------------------------------

def _key_from(passphrase: str, salt: bytes) -> bytes:
    if len(passphrase) < 16:
        raise BackupError("BACKUP_KEY is missing or too short (use at least 16 characters, e.g. the output of: python -c \"import secrets; print(secrets.token_urlsafe(32))\")")
    return Scrypt(salt=salt, length=32, n=2 ** 15, r=8, p=1).derive(passphrase.encode())


def _nonce(counter: int) -> bytes:
    return struct.pack(">4xQ", counter)


def encrypt_file(source: Path, target: Path, passphrase: str) -> None:
    salt = secrets.token_bytes(16)
    aes = AESGCM(_key_from(passphrase, salt))
    with source.open("rb") as src, target.open("wb") as dst:
        dst.write(MAGIC + salt)
        counter, piece = 0, src.read(PIECE)
        while True:
            following = src.read(PIECE)
            last = b"\x01" if not following else b"\x00"      # the flag is authenticated, so a file cut after a piece is detected
            box = aes.encrypt(_nonce(counter), piece, last)
            dst.write(last + struct.pack(">I", len(box)) + box)
            if not following:
                break
            counter, piece = counter + 1, following


def decrypt_file(source: Path, target: Path, passphrase: str) -> None:
    with source.open("rb") as src, target.open("wb") as dst:
        head = src.read(len(MAGIC) + 16)
        if len(head) != len(MAGIC) + 16 or not head.startswith(MAGIC):
            raise BackupError("this is not a ProcSense backup file")
        aes = AESGCM(_key_from(passphrase, head[len(MAGIC):]))
        counter, seen_last = 0, False
        while True:
            flag = src.read(1)
            if not flag:
                break
            if seen_last:
                raise BackupError("the backup file has data after its end")
            size = struct.unpack(">I", src.read(4))[0]
            box = src.read(size)
            try:
                dst.write(aes.decrypt(_nonce(counter), box, flag))
            except Exception:
                raise BackupError("the backup file cannot be decrypted: the key is wrong or the file was changed") from None
            seen_last, counter = flag == b"\x01", counter + 1
        if not seen_last:
            raise BackupError("the backup file is cut off")


def key_from_environment() -> str:
    return os.environ.get("BACKUP_KEY", "")


# ---- PostgreSQL tools (from the postgres:17 image) -------------------------------------------------------------------------------------------

def _docker(args: list[str], stdin=None, stdout=None, check=True, timeout=1800) -> subprocess.CompletedProcess:
    try:
        return subprocess.run(["docker", *args], stdin=stdin, stdout=stdout, stderr=subprocess.PIPE, check=check, timeout=timeout)
    except FileNotFoundError:
        raise BackupError("Docker is not installed or not on PATH (it runs PostgreSQL's own backup tools)") from None
    except subprocess.CalledProcessError as error:
        raise BackupError(f"docker {' '.join(args[:3])} failed: {error.stderr.decode(errors='replace')[-600:]}") from None


def _plain(url: str) -> str:
    """psycopg wants postgresql://; SQLAlchemy-style addresses with +psycopg are accepted too."""
    return url.replace("postgresql+psycopg://", "postgresql://", 1)


def _reach(url: str) -> tuple[str, list[str]]:
    """A container cannot reach the host's 127.0.0.1 by that name: on Linux it shares the host's network, elsewhere it uses host.docker.internal (this matters for tests and for restoring into a local database)."""
    from urllib.parse import urlsplit
    host = urlsplit(url).hostname
    if host not in ("localhost", "127.0.0.1", "::1"):
        return url, []
    if sys.platform.startswith("linux"):
        return url, ["--network", "host"]
    return url.replace(host, "host.docker.internal", 1), []


def tables_and_counts(cursor) -> dict[str, int]:
    cursor.execute("select tablename from pg_tables where schemaname = 'public' order by tablename")
    names = [row[0] for row in cursor.fetchall()]
    counts = {}
    for name in names:
        cursor.execute(f'select count(*) from public."{name}"')
        counts[name] = cursor.fetchone()[0]
    return counts


def dump(url: str, out_dir: Path, passphrase: str, previous_dir: Path | None = None) -> dict:
    """Takes one consistent snapshot, dumps the schema `public` with it, counts the rows with the same snapshot, encrypts, and checks the result against the last backup."""
    url = _plain(url)
    out_dir.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")
    plain, encrypted, manifest_path = out_dir / f"procsense-{stamp}.dump", out_dir / f"procsense-{stamp}.dump.enc", out_dir / f"procsense-{stamp}.manifest.json"
    started = time.monotonic()
    with psycopg.connect(url, autocommit=False) as connection:
        cursor = connection.cursor()
        cursor.execute("begin isolation level repeatable read read only")
        cursor.execute("select pg_export_snapshot()")
        snapshot = cursor.fetchone()[0]
        counts = tables_and_counts(cursor)
        if "alembic_version" not in counts:
            raise BackupError("the database has no application tables (wrong address, or migrations were never applied)")
        cursor.execute("select version_num from alembic_version")
        version = cursor.fetchone()[0]
        with plain.open("wb") as handle:
            reach, network = _reach(url)
            _docker(["run", "--rm", *network, PG_IMAGE, "pg_dump", "--format=custom", "--schema=public", "--no-owner", "--no-privileges", f"--snapshot={snapshot}", reach], stdout=handle)
        connection.rollback()
    plain_size = plain.stat().st_size
    if plain_size < 1024:
        plain.unlink()
        raise BackupError(f"the dump is only {plain_size} bytes: something is wrong, nothing was kept")
    encrypt_file(plain, encrypted, passphrase)
    plain.unlink()
    manifest = {"taken_at": datetime.now(timezone.utc).isoformat(timespec="seconds"), "file": encrypted.name, "bytes": encrypted.stat().st_size, "dump_bytes": plain_size,
                "migration": version, "tables": counts, "rows": sum(counts.values()), "seconds": round(time.monotonic() - started, 1)}
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    if previous_dir is not None:
        problem = compare_with_previous(manifest, previous_dir)
        if problem:
            raise BackupError(problem)
    return manifest


def compare_with_previous(manifest: dict, previous_dir: Path) -> str | None:
    """A backup much smaller than the last one means something was lost (or the dump was cut short); the run fails so a person looks."""
    olds = sorted(previous_dir.glob("*.manifest.json")) if previous_dir.is_dir() else []
    if not olds:
        return None
    old = json.loads(olds[-1].read_text(encoding="utf-8"))
    if old["bytes"] > 0 and manifest["bytes"] < SHRINK_LIMIT * old["bytes"]:
        return f"the backup is {manifest['bytes']} bytes, less than half of the last one ({old['bytes']} bytes): check that nothing was deleted before trusting it"
    if old["rows"] >= MIN_ROWS_TO_COMPARE and manifest["rows"] < SHRINK_LIMIT * old["rows"]:
        return f"the database has {manifest['rows']} rows, less than half of the {old['rows']} the last backup had: check that nothing was deleted"
    return None


def restore(file: Path, url: str, passphrase: str, work_dir: Path) -> None:
    work_dir.mkdir(parents=True, exist_ok=True)
    plain = work_dir / f"restore-{uuid.uuid4().hex[:8]}.dump"
    try:
        decrypt_file(file, plain, passphrase)
        with plain.open("rb") as handle:
            reach, network = _reach(_plain(url))
            _docker(["run", "--rm", "-i", *network, PG_IMAGE, "pg_restore", "--no-owner", "--no-privileges", "--clean", "--if-exists", "--exit-on-error", "-d", reach], stdin=handle)
    finally:
        plain.unlink(missing_ok=True)


def drill(file: Path, passphrase: str, work_dir: Path) -> dict:
    """Restores the backup into a throwaway PostgreSQL and compares every table's row count with the manifest written when the dump was taken."""
    manifest_path = file.with_name(file.name.replace(".dump.enc", ".manifest.json"))
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    name, password = f"procsense-drill-{uuid.uuid4().hex[:8]}", secrets.token_hex(8)
    started = time.monotonic()
    _docker(["run", "-d", "--rm", "--name", name, "-e", f"POSTGRES_PASSWORD={password}", "-p", "127.0.0.1::5432", PG_IMAGE])
    try:
        port = _docker(["port", name, "5432/tcp"], stdout=subprocess.PIPE).stdout.decode().strip().splitlines()[0].rsplit(":", 1)[1]
        local = f"postgresql://postgres:{password}@127.0.0.1:{port}/postgres"
        for _ in range(60):
            try:
                psycopg.connect(local, connect_timeout=2).close()
                break
            except psycopg.OperationalError:
                time.sleep(1)
        else:
            raise BackupError("the throwaway PostgreSQL did not start")
        work_dir.mkdir(parents=True, exist_ok=True)
        plain = work_dir / f"drill-{uuid.uuid4().hex[:8]}.dump"
        try:
            decrypt_file(file, plain, passphrase)
            with plain.open("rb") as handle:
                _docker(["exec", "-i", name, "pg_restore", "--no-owner", "--no-privileges", "--clean", "--if-exists", "--exit-on-error", "-U", "postgres", "-d", "postgres"], stdin=handle)
        finally:
            plain.unlink(missing_ok=True)
        with psycopg.connect(local) as connection:
            restored = tables_and_counts(connection.cursor())
    finally:
        subprocess.run(["docker", "rm", "-f", name], capture_output=True)
    expected = manifest["tables"]
    wrong = {t: (expected.get(t), restored.get(t)) for t in sorted(set(expected) | set(restored)) if expected.get(t) != restored.get(t)}
    if wrong:
        raise BackupError("the restored tables do not match the backup's manifest (table: expected, restored): " + json.dumps(wrong))
    return {"restored": True, "tables": len(restored), "rows": sum(restored.values()), "migration": manifest["migration"], "seconds": round(time.monotonic() - started, 1)}


# ---- monitor -------------------------------------------------------------------------------------------------------------------------------

def monitor(url: str) -> dict:
    with psycopg.connect(_plain(url), connect_timeout=30) as connection:
        cursor = connection.cursor()
        cursor.execute("select pg_database_size(current_database())")
        size = cursor.fetchone()[0]
        cursor.execute("select count(*) from pg_stat_activity")
        connections = cursor.fetchone()[0]
        cursor.execute("select count(*) from public.session")
        sessions = cursor.fetchone()[0]
    fraction = size / FREE_LIMIT_BYTES
    return {"database_mb": round(size / 1024 / 1024, 1), "limit_mb": 500, "used_percent": round(fraction * 100, 1), "connections": connections, "sessions": sessions, "warn": fraction >= WARN_AT}


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("dump"); p.add_argument("--url", default=os.environ.get("BACKUP_DATABASE_URL", "")); p.add_argument("--out", type=Path, required=True); p.add_argument("--previous", type=Path)
    p = sub.add_parser("drill"); p.add_argument("--file", type=Path, required=True)
    p = sub.add_parser("restore"); p.add_argument("--file", type=Path, required=True); p.add_argument("--url", required=True)
    p = sub.add_parser("monitor"); p.add_argument("--url", default=os.environ.get("BACKUP_DATABASE_URL", ""))
    args = parser.parse_args(argv)
    try:
        if args.command == "dump":
            if not args.url.startswith(("postgresql://", "postgres://", "postgresql+psycopg://")):
                raise BackupError("the database address is missing or does not start with postgresql:// (give --url or set BACKUP_DATABASE_URL)")
            print(json.dumps(dump(args.url, args.out, key_from_environment(), args.previous)))
        elif args.command == "drill":
            print(json.dumps(drill(args.file, key_from_environment(), args.file.parent)))
        elif args.command == "restore":
            restore(args.file, args.url, key_from_environment(), args.file.parent)
            print(json.dumps({"restored": True}))
        else:
            result = monitor(args.url)
            print(json.dumps(result))
            if result["warn"]:
                print(f"::error::The database uses {result['used_percent']}% of the free 500 MB. Plan the move to a paid plan or clean up (docs: server/RUNBOOK.md, Backup and restore).")
                return 1
    except BackupError as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
