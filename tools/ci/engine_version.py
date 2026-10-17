"""Engine version rule (DT-174): CoreInfo.Version must change whenever a change can make an old log replay differently.

A golden run's log hash and state hash are the fingerprint of the engine's behaviour. `tests/golden/engine_version.lock`
holds the engine version together with one digest of every golden hash. If the golden hashes change while the version
stays the same, a headset built from an older engine would look like a tampered session to the server (HASH_MISMATCH
instead of ENGINE_VERSION_UNSUPPORTED), so the check fails and says what to do.

Usage (repo root):  uv run --project tools tools/ci/engine_version.py [check|update]
  check   fail when the goldens changed and CoreInfo.Version did not (default; also run by repo_checks.py)
  update  rewrite the lock after the version was raised on purpose
"""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GOLDEN = ROOT / "tests" / "golden"
GENERATED = ROOT / "tests" / "golden_generated"      # golden runs of generated cases count too: the engine plays them like any case
LOCK = GOLDEN / "engine_version.lock"
CORE_INFO = ROOT / "src" / "ProcSense.Core" / "CoreInfo.cs"


def core_version(text: str | None = None) -> str:
    m = re.search(r'Version\s*=\s*"([^"]+)"', text if text is not None else CORE_INFO.read_text(encoding="utf-8"))
    if not m:
        raise SystemExit(f"cannot find the version in {CORE_INFO}")
    return m.group(1)


def golden_digest(golden: Path = GOLDEN, extra: tuple[Path, ...] | None = None) -> str:
    """One digest over every golden's name, log hash and state hash (sorted by name). The generated cases' goldens are included when the default folder is used."""
    lines = []
    folders = [golden] + list(extra if extra is not None else ((GENERATED,) if golden == GOLDEN else ()))
    files = [p for folder in folders if folder.exists() for p in folder.glob("*.expected.json")]
    for p in sorted(files, key=lambda f: f.name):   # by name as text: Path order differs between Windows and Linux
        doc = json.loads(p.read_text(encoding="utf-8"))
        lines.append(f"{p.name}:{doc.get('log_hash', '')}:{doc.get('state_hash', '')}")
    return hashlib.sha256("\n".join(lines).encode("utf-8")).hexdigest()


def check(version: str, digest: str, lock: dict | None) -> list[str]:
    if lock is None:
        return [f"{LOCK.relative_to(ROOT).as_posix()} is missing: run `uv run --project tools tools/ci/engine_version.py update`"]
    if digest == lock.get("golden_digest") and version == lock.get("version"):
        return []
    if digest != lock.get("golden_digest") and version == lock.get("version"):
        return [f"the golden hashes changed but CoreInfo.Version is still {version}: raise it in src/ProcSense.Core/CoreInfo.cs "
                "(a log made by the older engine must be refused as ENGINE_VERSION_UNSUPPORTED, not look tampered), "
                "then run `uv run --project tools tools/ci/engine_version.py update`"]
    return [f"CoreInfo.Version is {version} but the lock says {lock.get('version')} (or the digest differs): "
            "run `uv run --project tools tools/ci/engine_version.py update` and commit the lock"]


def read_lock() -> dict | None:
    return json.loads(LOCK.read_text(encoding="utf-8")) if LOCK.exists() else None


def write_lock() -> None:
    LOCK.write_text(json.dumps({"version": core_version(), "golden_digest": golden_digest()}, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {LOCK.relative_to(ROOT).as_posix()} (engine {core_version()})")


def main(argv: list[str]) -> int:
    cmd = argv[0] if argv else "check"
    if cmd == "update":
        write_lock()
        return 0
    problems = check(core_version(), golden_digest(), read_lock())
    for p in problems:
        print("FAIL  " + p)
    if not problems:
        print(f"PASS  engine {core_version()} matches the golden hashes in the lock")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
