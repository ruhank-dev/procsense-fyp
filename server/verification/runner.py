"""Running the engine's `verify` command on one uploaded session (p1-backend-s09).

The engine is a separate program (the command-line simulator). A session is written to a temporary file with a name the service chose, the engine replays it with the content of the version the session was played with,
and the service reads the one-line JSON verdict. The engine runs with a time limit, a CPU limit where the system allows one, a cap on how much it may print, and an environment with every secret removed
(it never sees the database address or any key). A crash, a timeout or output that is not a verdict is a RunnerError: the session stays pending and is tried again, it is not judged.
"""
from __future__ import annotations

import json
import os
import re
import signal
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

VERSION_NAME = re.compile(r"^[0-9a-f]{12}(-[a-z]+)?$")   # what the engine writes as a content version: 12 characters of the content hash, optionally the mode
MAX_OUTPUT_BYTES = 8 * 1024 * 1024
SECRET_WORDS = ("KEY", "SECRET", "PASSWORD", "PASSWD", "TOKEN", "DATABASE", "CREDENTIAL", "SUPABASE")


class RunnerError(Exception):
    """The engine could not give a verdict (crash, timeout, unreadable output). The session is not judged."""


@dataclass(frozen=True)
class EngineVerdict:
    ok: bool
    reason: str
    detail: str
    data: dict          # the whole verdict, including `facts` when ok
    report_html: bytes | None


def exit_text(code: int) -> str:
    """How an engine exit reads in the log: a negative code means the system stopped it with a signal, and the signal's name says why (for example SIGXFSZ: it wrote a file larger than the limit)."""
    if code < 0:
        try:
            return f"killed by signal {signal.Signals(-code).name} ({-code})"
        except ValueError:
            return f"killed by signal {-code}"
    return f"exit {code}"


def clean_environment(environ: dict[str, str] | None = None) -> dict[str, str]:
    """The environment the engine gets: everything except anything that looks like a secret."""
    environ = os.environ if environ is None else environ
    return {k: v for k, v in environ.items() if not any(word in k.upper() for word in SECRET_WORDS)}


class EngineRunner:
    def __init__(self, command: list[str], content_root: str, timeout_seconds: int = 60):
        self._command = command
        self._root = Path(content_root)
        self._timeout = timeout_seconds

    def content_dir_for(self, content_version: str) -> Path | None:
        """The folder of content for a version, or None when this service cannot verify that version (it is then unverifiable, not rejected)."""
        if not VERSION_NAME.match(content_version):
            return None
        path = self._root / content_version
        return path if path.is_dir() and path.resolve().parent == self._root.resolve() else None

    def verify(self, header_line: str, log_text: str, content_dir: Path, package: bytes | None = None) -> EngineVerdict:
        with tempfile.TemporaryDirectory(prefix="verify-") as tmp:
            tmp_path = Path(tmp)
            session_file, html_file = tmp_path / "session.jsonl", tmp_path / "report.html"
            session_file.write_text(header_line + "\n" + log_text, encoding="utf-8", newline="")
            args = [*self._command, "verify", "--log", str(session_file), "--html", str(html_file), "--content", str(content_dir)]
            if package is not None:
                package_file = tmp_path / "package.json"
                package_file.write_bytes(package)
                args += ["--package", str(package_file)]
            try:
                done = subprocess.run(args, capture_output=True, timeout=self._timeout, cwd=tmp, env=clean_environment(), preexec_fn=_limits(self._timeout) if os.name == "posix" else None)
            except subprocess.TimeoutExpired:
                raise RunnerError("timeout") from None
            except OSError as e:
                raise RunnerError(f"could not start the engine: {type(e).__name__}") from None
            if len(done.stdout) > MAX_OUTPUT_BYTES:
                raise RunnerError("output too large")
            data = _read_verdict(done.stdout)
            if data is None or done.returncode not in (0, 1) or bool(data.get("ok")) != (done.returncode == 0):
                raise RunnerError(f"no verdict ({exit_text(done.returncode)})")
            html = html_file.read_bytes() if html_file.exists() else None
            return EngineVerdict(bool(data["ok"]), str(data.get("reason", "")), str(data.get("detail", "")), data, html)


def _read_verdict(stdout: bytes) -> dict | None:
    for line in stdout.decode("utf-8", errors="replace").splitlines():
        if line.startswith("{"):
            try:
                value = json.loads(line)
            except ValueError:
                return None
            return value if isinstance(value, dict) and "ok" in value else None
    return None


def _limits(timeout_seconds: int):
    def apply() -> None:   # runs in the child before the engine starts (Linux only)
        import resource
        resource.setrlimit(resource.RLIMIT_CPU, (timeout_seconds + 10, timeout_seconds + 10))
        resource.setrlimit(resource.RLIMIT_FSIZE, (64 * 1024 * 1024, 64 * 1024 * 1024))
    return apply
