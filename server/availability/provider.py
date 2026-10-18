"""What an availability answer is and what a provider of answers must do (p2-content-extensions-s04).

A medicine group is `available`, `unavailable` or `unknown` at a place and time, according to ONE named source. Three rules are in the types, not in the callers:
  * registration is not stock: a source of kind `registration_register` can never answer `available` or `unavailable` (it lists what may be sold, not what is on a shelf);
  * synthetic answers are marked `synthetic` and `live` is False, so no display can pass them off as real;
  * an answer carries the source, the UTC time it was observed and how long it may be used; past that it is `unknown` (freshness is checked by the service, not trusted from the file).
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
from typing import Protocol

STATUSES = ("available", "unavailable", "unknown")
KINDS = ("synthetic", "pharmacist_supplied", "licensed_feed", "registration_register")


@dataclass(frozen=True)
class Availability:
    medicine_id: str
    status: str                 # available | unavailable | unknown
    source_id: str              # id of the source in permitted_sources.json, or "synthetic-demo", or "none"
    source_kind: str            # one of KINDS, or "none"
    observed_at: datetime | None  # UTC; None when nothing was observed
    max_age_days: int           # how long the observation may be used
    note: str = ""

    @property
    def synthetic(self) -> bool:
        return self.source_kind == "synthetic"

    @property
    def live(self) -> bool:
        """True only for an observation from a permitted real source; synthetic and unknown are never live."""
        return self.source_kind in ("pharmacist_supplied", "licensed_feed") and self.status != "unknown"

    def __post_init__(self):
        if self.status not in STATUSES:
            raise ValueError(f"status {self.status!r} is not one of {STATUSES}")
        if self.source_kind == "registration_register" and self.status != "unknown":
            raise ValueError("a registration register cannot say a medicine is in or out of stock")
        if self.source_kind == "synthetic" and self.live:
            raise ValueError("synthetic answers are never live")


def unknown(medicine_id: str, why: str, max_age_days: int = 0) -> Availability:
    return Availability(medicine_id, "unknown", "none", "none", None, max_age_days, why)


class Provider(Protocol):
    """A source of availability answers. `lookup` returns None when it has no answer for the medicine; it never raises for a missing medicine and never invents one."""
    source_id: str

    def lookup(self, medicine_id: str) -> Availability | None: ...
