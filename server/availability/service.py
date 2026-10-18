"""Resolve a medicine's availability from providers, with freshness, and say what may be shown (p2-content-extensions-s04).

    answer = resolve("warfarin", providers, config, now)
    answer.status       # available | unavailable | unknown
    note(answer, config)  # the sentence a debrief may add, or "" when nothing may be said

Order and rules: live display is off unless content/availability/config.json says `live_enabled` AND a provider holds a fresh answer from a permitted source; a stale answer becomes unknown
(never silently served); synthetic answers are served only when the caller passes `demo=True`, and are then always labelled; with no answer the result is `unknown` from source `none`.
"""
from __future__ import annotations

from datetime import datetime, timedelta, timezone

from .provider import Availability, Provider, unknown


def fresh(a: Availability, now: datetime) -> bool:
    return a.observed_at is not None and now - a.observed_at <= timedelta(days=a.max_age_days)


def resolve(medicine_id: str, providers: list[Provider], config: dict, now: datetime | None = None, demo: bool = False) -> Availability:
    now = now or datetime.now(timezone.utc)
    live_ok = bool(config.get("live_enabled"))
    stale: Availability | None = None
    for p in providers:
        a = p.lookup(medicine_id)
        if a is None:
            continue
        if a.synthetic:
            if not demo:
                continue                                        # never offered as an answer outside demo mode
            return a if fresh(a, now) else unknown(medicine_id, "the synthetic demonstration value is out of date", a.max_age_days)
        if not live_ok:
            continue                                            # live display is switched off: a real-source row is ignored, not shown
        if fresh(a, now):
            return a
        stale = a
    if stale is not None:
        return unknown(medicine_id, f"the last observation from {stale.source_id} ({stale.observed_at:%Y-%m-%d}) is older than {stale.max_age_days} days", stale.max_age_days)
    return unknown(medicine_id, "no permitted source has been established" if not live_ok else "no source has an answer")


def note(a: Availability, config: dict) -> str:
    """The wording a debrief or explanation may add. Empty for unknown. Synthetic values are prefixed so they cannot read as real."""
    if a.status == "unknown":
        return ""
    when = a.observed_at.strftime("%Y-%m-%d") if a.observed_at else "unknown date"
    if a.synthetic:
        return f"[SYNTHETIC DEMONSTRATION, not real stock] {a.medicine_id} shown as {a.status}."
    if a.status == "unavailable":
        return f"Reported as not available locally by {a.source_id} on {when}; check what is stocked where you work."
    return ""
