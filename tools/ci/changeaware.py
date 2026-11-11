"""Change-aware testing core: what changed, which checks that affects, and a content-hash result cache.

Used by select_checks.py (decides), run.py (executes) and the GitHub workflow (job selection). Guide: docs/qa/change_aware_testing.md.
Safe by construction: an unmapped path, a shared-infrastructure change or any error selects EVERYTHING; the cache only reuses a PASS whose
inputs (file contents, check definition, tool versions) are byte-identical and not older than the check's max_age_hours.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
import sys
import time
from functools import lru_cache
from pathlib import Path

try:
    import yaml
except ImportError:  # pragma: no cover
    sys.exit("PyYAML is missing: run this with `uv run --project tools python tools/ci/select_checks.py` (tools/pyproject.toml lists pyyaml).")

ROOT = Path(__file__).resolve().parents[2]
RULES_FILE = Path(__file__).with_name("rules.yml")
RESULT_SCHEMA = 1   # bump to invalidate every cached result (e.g. after changing how keys are built)


# --------------------------------------------------------------------------- rules and globs
def _flatten(items):
    for i in items or []:
        if isinstance(i, list):
            yield from _flatten(i)
        else:
            yield i


def load_rules(path: Path = RULES_FILE) -> dict:
    data = yaml.safe_load(path.read_text(encoding="utf-8"))
    data["full_triggers"] = list(_flatten(data.get("full_triggers")))
    data["ignore"] = list(_flatten(data.get("ignore")))
    for name, chk in data["checks"].items():
        chk["name"] = name
        chk["inputs"] = list(_flatten(chk.get("inputs")))
        chk.setdefault("kind", "local")
        chk.setdefault("job", name)
    return data


@lru_cache(maxsize=4096)
def glob_re(pattern: str) -> re.Pattern:
    out, i = "", 0
    while i < len(pattern):
        c = pattern[i]
        if pattern.startswith("**/", i):
            out += "(?:.*/)?"; i += 3; continue
        if pattern.startswith("**", i):
            out += ".*"; i += 2; continue
        out += "[^/]*" if c == "*" else "[^/]" if c == "?" else re.escape(c)
        i += 1
    return re.compile("^" + out + "$")


def norm(path: str) -> str:
    p = path.replace("\\", "/")
    while p.startswith("./"):
        p = p[2:]
    return p[:-5] if p.endswith(".meta") else p   # a Unity .meta file counts as the asset it describes


def matches(path: str, patterns) -> str | None:
    p = norm(path)
    for pat in patterns:
        if glob_re(pat).match(p):
            return pat
    return None


# --------------------------------------------------------------------------- git: what changed, and a content hash for every file
def git(*args: str, check: bool = True) -> str:
    r = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {r.stderr.strip()[:300]}")
    return r.stdout


def _lines(out: str) -> list[str]:
    return [l for l in out.split("\0") if l] if "\0" in out else [l for l in out.splitlines() if l]


def worktree_dirty() -> set[str]:
    """Files whose content differs from HEAD: modified, staged, deleted, or untracked (not ignored)."""
    dirty = set(_lines(git("diff", "--name-only", "--no-renames", "-z", "HEAD")))
    dirty |= set(_lines(git("ls-files", "-o", "--exclude-standard", "-z")))
    return dirty


def changed_files(base: str | None = None, head: str | None = None, staged: bool = False) -> list[str]:
    if staged:
        return sorted(_lines(git("diff", "--cached", "--name-only", "--no-renames", "-z")))
    if base:
        files = set(_lines(git("diff", "--name-only", "--no-renames", "-z", f"{base}...{head or 'HEAD'}")))
        if not head:
            files |= worktree_dirty()
        return sorted(files)
    return sorted(worktree_dirty())


def file_hashes() -> dict[str, str]:
    """path -> content id for every tracked or untracked file: git's blob id when the file is clean, a sha256 of the bytes when it is not."""
    ids: dict[str, str] = {}
    for rec in _lines(git("ls-files", "-s", "-z")):
        meta, _, path = rec.partition("\t")
        ids[path] = meta.split()[1]
    for path in worktree_dirty():
        f = ROOT / path
        ids[path] = "sha256:" + hashlib.sha256(f.read_bytes()).hexdigest() if f.is_file() else "deleted"
    return {p: h for p, h in ids.items() if h != "deleted"}


# --------------------------------------------------------------------------- selection
def select(rules: dict, files: list[str], *, force_all: bool = False, all_reason: str = "") -> dict:
    """Pure function: the changed files in, the verdict per check out. No git, no disk."""
    checks = rules["checks"]
    verdict = {n: {"run": False, "why": "no input changed", "inputs": c["inputs"], "kind": c["kind"], "job": c["job"],
                   "heavy": bool(c.get("heavy")), "cached": False} for n, c in checks.items()}
    full, reason = force_all, all_reason or ("--all" if force_all else "")
    unmapped = []
    for f in files:
        if matches(f, rules["ignore"]) and not matches(f, rules["full_triggers"]):
            continue
        if (hit := matches(f, rules["full_triggers"])):
            full, reason = True, reason or f"shared infrastructure changed: {norm(f)} (matches {hit})"
            continue
        hit_any = False
        for n, c in checks.items():
            if (pat := matches(f, c["inputs"])):
                hit_any = True
                if not verdict[n]["run"]:
                    verdict[n].update(run=True, why=f"{norm(f)} matches {pat}")
        if not hit_any:
            unmapped.append(norm(f))
    if unmapped and not full:
        full, reason = True, f"unmapped path (no rule covers it, so everything runs): {unmapped[0]}" + (f" and {len(unmapped) - 1} more" if len(unmapped) > 1 else "")
    if full:
        for n in verdict:
            verdict[n].update(run=True, why=reason)
    jobs = {"engine": False, "server": False, "repo_checks": False, "dependencies": False, "unity_compile": False, "unity_tests": False, "android_build": False}
    for n, v in verdict.items():
        if v["run"]:
            jobs[v["job"]] = True
    return {"full": full, "reason": reason or ("nothing to check" if not any(v["run"] for v in verdict.values()) else "per-check input match"),
            "changed": [norm(f) for f in files], "jobs": jobs, "checks": verdict}


# --------------------------------------------------------------------------- cache
def cache_dir() -> Path:
    env = os.environ.get("PROCSENSE_CI_CACHE")
    if env:
        return Path(env)
    if os.name == "nt" and Path("D:/").exists():
        return Path("D:/ProcSenseWork/ci_cache")   # big/volatile files go to the drive with the most free space (owner's rule)
    return Path(__file__).with_name("cache")        # gitignored


@lru_cache(maxsize=None)
def tool_version(tool: str) -> str:
    try:
        if tool == "python":
            return sys.version.split()[0]
        if tool == "unity":
            m = re.search(r"m_EditorVersion: (\S+)", (ROOT / "unity/ProjectSettings/ProjectVersion.txt").read_text())
            return "unity-" + (m.group(1) if m else "?")
        cmd = {"dotnet": ["dotnet", "--version"], "uv": ["uv", "--version"], "docker": ["docker", "--version"]}[tool]
        return subprocess.run(cmd, capture_output=True, text=True, timeout=30, shell=os.name == "nt").stdout.strip() or "unknown"
    except Exception:
        return "unavailable"


def cache_key(check: dict, hashes: dict[str, str]) -> str:
    h = hashlib.sha256()
    definition = {k: check.get(k) for k in ("cmds", "pwsh", "unity", "fail_if_output", "salt", "params")}
    h.update(json.dumps([RESULT_SCHEMA, check["name"], definition, {t: tool_version(t) for t in check.get("tools", [])}], sort_keys=True).encode())
    for path in sorted(p for p in hashes if matches(p, check["inputs"])):
        h.update(b"\0" + path.encode() + b"\0" + hashes[path].encode())
    return h.hexdigest()


def _entry_path(name: str, key: str) -> Path:
    return cache_dir() / "results" / name / f"{key}.json"


def cache_lookup(check: dict, key: str, now: float | None = None) -> dict | None:
    """A previous PASS with exactly this key, still within max_age_hours. Failures are never reused."""
    p = _entry_path(check["name"], key)
    try:
        e = json.loads(p.read_text())
    except (OSError, ValueError):
        return None
    if not e.get("passed") or e.get("key") != key:
        return None
    age_h = ((now or time.time()) - e.get("finished", 0)) / 3600
    if check.get("max_age_hours") and age_h > check["max_age_hours"]:
        return None
    return e


def cache_store(check: dict, key: str, passed: bool, seconds: float, note: str = "") -> None:
    p = _entry_path(check["name"], key)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps({"check": check["name"], "key": key, "passed": passed, "seconds": round(seconds, 1), "finished": time.time(), "note": note[-400:]}))


def mark_cached(verdict: dict, rules: dict, hashes: dict[str, str]) -> None:
    for n, v in verdict["checks"].items():
        v["cached"] = bool(cache_lookup(rules["checks"][n], cache_key(rules["checks"][n], hashes)))
