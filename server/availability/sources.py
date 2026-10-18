"""The two providers: a synthetic one for demos and tests, and a file one for data from a PERMITTED source (p2-content-extensions-s04).

SyntheticProvider reads content/availability/synthetic_demo.json. Its answers say source_kind `synthetic`; the service never serves them unless the caller asks for demo mode.

FileProvider reads a CSV (medicine_id,status,observed_on,source_id) supplied by a source listed in content/availability/permitted_sources.json. A row is refused, with the reason kept, when its
source is not listed, the source's `allowed_uses` lacks `teaching_display`, its kind is a registration register and the status claims stock, its date is not an ISO date or lies in the future, or
its status is not a known one. Nothing is fetched from the network here; getting the file from the source is a separate act the source's terms must allow.
"""
from __future__ import annotations

import csv
import json
from datetime import datetime, timezone
from pathlib import Path

from .provider import KINDS, STATUSES, Availability

ROOT = Path(__file__).resolve().parents[2]
DIR = ROOT / "content" / "availability"


def load_config(path: Path = DIR / "config.json") -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def load_permitted(path: Path = DIR / "permitted_sources.json") -> dict[str, dict]:
    return {s["id"]: s for s in json.loads(path.read_text(encoding="utf-8"))["sources"]}


def _day(text: str) -> datetime:
    return datetime.strptime(text, "%Y-%m-%d").replace(tzinfo=timezone.utc)


class SyntheticProvider:
    source_id = "synthetic-demo"

    def __init__(self, path: Path = DIR / "synthetic_demo.json"):
        doc = json.loads(path.read_text(encoding="utf-8"))
        if doc.get("synthetic") is not True:
            raise ValueError("the synthetic file must say synthetic: true")
        self.max_age_days = int(doc["max_age_days"])
        self._rows = {r["medicine_id"]: r for r in doc["rows"]}

    def lookup(self, medicine_id: str) -> Availability | None:
        r = self._rows.get(medicine_id)
        if r is None:
            return None
        return Availability(medicine_id, r["status"], self.source_id, "synthetic", _day(r["observed_on"]), self.max_age_days, "SYNTHETIC demonstration value, not a real stock position")


class FileProvider:
    def __init__(self, csv_path: Path, permitted: dict[str, dict], source_id: str, max_age_days: int, now: datetime | None = None):
        self.source_id = source_id
        self.rejected: list[tuple[int, str]] = []
        self._rows: dict[str, Availability] = {}
        now = now or datetime.now(timezone.utc)
        src = permitted.get(source_id)
        if src is None:
            self.rejected.append((0, f"source {source_id!r} is not in permitted_sources.json"))
            return
        if src["kind"] not in KINDS or src["kind"] == "synthetic":
            self.rejected.append((0, f"source kind {src['kind']!r} is not a real source kind"))
            return
        if "teaching_display" not in src.get("allowed_uses", []):
            self.rejected.append((0, f"source {source_id!r} does not allow teaching display"))
            return
        with open(csv_path, encoding="utf-8", newline="") as f:
            for n, row in enumerate(csv.DictReader(f), start=2):
                why = self._refuse(row, src, now)
                if why:
                    self.rejected.append((n, why))
                    continue
                self._rows[row["medicine_id"]] = Availability(row["medicine_id"], row["status"], source_id, src["kind"], _day(row["observed_on"]), max_age_days, src.get("attribution", ""))

    @staticmethod
    def _refuse(row: dict, src: dict, now: datetime) -> str:
        if row.get("source_id") != src["id"]:
            return f"row names source {row.get('source_id')!r}, the file is for {src['id']!r}"
        if row.get("status") not in STATUSES:
            return f"status {row.get('status')!r} is not one of {STATUSES}"
        if src["kind"] == "registration_register" and row["status"] != "unknown":
            return "a registration register cannot give a stock status"
        try:
            when = _day(row.get("observed_on", ""))
        except ValueError:
            return f"observed_on {row.get('observed_on')!r} is not an ISO date"
        if when > now:
            return "observed_on lies in the future"
        return ""

    def lookup(self, medicine_id: str) -> Availability | None:
        return self._rows.get(medicine_id)
