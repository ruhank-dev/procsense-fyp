"""Session history for generation: what the next objectives and the novelty check read (p1-live-scenario-generation-s12, stage LG-20).

    entry = entry_from_report(trainee, session_id, package, report, ended_at)     # one finished generated or library session
    store = FileHistory(directory); store.append(entry)
    records = session_records(store.entries(trainee))                              # for objectives.derive
    levels = novelty_history(store.entries(trainee))                               # for novelty.check
    recent = recent_fingerprints(store.entries(trainee))                           # for objectives exclusions

An entry holds only what the generator needs and nothing that names a person: a pseudonym, the session id, the package id and its SHA-256, the story's spine and fingerprint, the fingerprint levels, the tier and the credits
and penalties per behaviour (the "competencies" of the report). Times are UTC. The same file serves a trainee on one machine or the backend's table later (the fields are the columns). A session played from a library case
has no package and no fingerprint levels; it still counts toward the ratings. Nothing here runs in a session: it is read when a request arrives and written when a session ends.
"""
from __future__ import annotations

import json
import re
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path

from . import objectives as ob

TRAINEE = re.compile(r"^[A-Za-z0-9_-]{3,64}$")


class HistoryError(ValueError):
    pass


@dataclass
class HistoryEntry:
    trainee: str
    session_id: str
    case: str                                   # the case id played (a generated case id for a generated session)
    story: str                                  # the case the story belongs to: the spine for a generated case, the case itself otherwise
    tier: str
    behaviours: dict[str, list[int]]            # behaviour -> [credits, penalties]
    ended_at: str                               # UTC, ISO 8601 with Z
    package_id: str = ""
    package_sha256: str = ""
    fingerprint: str = ""
    levels: dict[str, list[str]] = field(default_factory=dict)

    def as_dict(self) -> dict:
        return asdict(self)


def now_utc() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def entry_from_report(trainee: str, session_id: str, report: dict, ended_at: str | None = None, package: dict | None = None, package_file_sha256: str | None = None, tier_of=None) -> HistoryEntry:
    """`report` is the engine's debrief facts (content/schema/debrief_facts.schema.json). For a generated session `package` is the package that was played; it must be the one the report names."""
    if not TRAINEE.match(trainee):
        raise HistoryError("the trainee is a pseudonym of 3 to 64 letters, digits, - or _")
    behaviours = {c["behaviour"]: [c["credits"], c["penalties"]] for c in report["competencies"] if c["items"] > 0}
    gen = report.get("generated")
    if gen:
        if package is None:
            raise HistoryError("a generated session needs its package")
        if gen["package_sha256"] and package_file_sha256 and gen["package_sha256"] != package_file_sha256:
            raise HistoryError("the report was made from another package than the one given")
        if package["case"]["id"] != report["case"]:
            raise HistoryError("the package is not the case the report is about")
        return HistoryEntry(trainee, session_id, report["case"], gen["spine"], gen["tier"], behaviours, ended_at or now_utc(), package["id"], gen["package_sha256"] or (package_file_sha256 or ""),
                            package["fingerprint"], package.get("fingerprint_levels", {}))
    tier = tier_of(report["case"]) if tier_of else "P0"
    return HistoryEntry(trainee, session_id, report["case"], report["case"], tier, behaviours, ended_at or now_utc())


class FileHistory:
    """One append-only JSON Lines file per trainee. A damaged line is reported, not skipped silently."""

    def __init__(self, directory: Path):
        self.dir = Path(directory)

    def path(self, trainee: str) -> Path:
        if not TRAINEE.match(trainee):
            raise HistoryError("not a pseudonym")
        return self.dir / f"{trainee}.jsonl"

    def append(self, e: HistoryEntry) -> None:
        self.dir.mkdir(parents=True, exist_ok=True)
        with self.path(e.trainee).open("a", encoding="utf-8", newline="\n") as f:
            f.write(json.dumps(e.as_dict(), ensure_ascii=False, sort_keys=True) + "\n")

    def entries(self, trainee: str) -> list[HistoryEntry]:
        p = self.path(trainee)
        if not p.exists():
            return []
        out = []
        for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
            if not line.strip():
                continue
            try:
                out.append(HistoryEntry(**json.loads(line)))
            except (json.JSONDecodeError, TypeError) as e:
                raise HistoryError(f"{p.name} line {n}: {e}") from e
        return out

    def forget(self, trainee: str) -> bool:
        """Remove a trainee's history (the privacy request); True if there was any."""
        p = self.path(trainee)
        if p.exists():
            p.unlink()
            return True
        return False


def session_records(entries: list[HistoryEntry]) -> list[ob.SessionRecord]:
    """For objectives.derive. The case is the story's case, so a generated session excludes its spine like a library session excludes itself."""
    return [ob.SessionRecord(e.story, e.tier, {b: (v[0], v[1]) for b, v in e.behaviours.items()}) for e in entries]


def novelty_history(entries: list[HistoryEntry]) -> list[dict]:
    return [e.levels for e in entries if e.levels]


def recent_fingerprints(entries: list[HistoryEntry], n: int = 20) -> list[str]:
    return [e.fingerprint for e in entries if e.fingerprint][-n:]
