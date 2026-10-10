"""NEWS2 (RCP 2017) scoring from content/news2_scoring.csv, for checking the case files and tables (the engine has its own implementation in Core).

    score(hr=..., sbp=..., rr=..., spo2=..., temp=..., acvpu="A", oxygen="room_air", scale=1) -> total

Cells are inclusive ranges from the verified table (checked against the RCP chart in ISS-0007); a value that falls in no cell raises, so a gap in the table is never scored as 0.
"""
from __future__ import annotations

import csv
from functools import lru_cache
from pathlib import Path

CONTENT = Path(__file__).resolve().parents[2] / "content"


@lru_cache(maxsize=1)
def cells() -> list[dict]:
    with open(CONTENT / "news2_scoring.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def _in(v: float, c: dict) -> bool:
    lo = float(c["v_low"]) if c["v_low"] != "" else float("-inf")
    hi = float(c["v_high"]) if c["v_high"] != "" else float("inf")
    return lo <= v <= hi


def points(parameter: str, value: float, scale: int = 1, extra: str | None = None) -> int:
    hits = [c for c in cells() if c["scale"] == str(scale) and c["parameter"] == parameter and _in(value, c) and (extra is None or extra in c["id"])]
    if len(hits) != 1:
        raise ValueError(f"NEWS2 {parameter}={value}: {len(hits)} matching cells")
    return int(hits[0]["o_points"])


def score(hr: float, sbp: float, rr: float, spo2: float, temp: float, acvpu: str = "A", oxygen: str = "room_air", scale: int = 1) -> int:
    """Scale 1 unless the clinician has set scale 2 (target 88-92% in hypercapnic failure)."""
    total = points("respiratory_rate", rr) + points("systolic_bp", sbp) + points("heart_rate", hr) + points("temperature_c", temp)
    if scale == 1:
        total += points("spo2", spo2, 1)
    else:
        total += points("spo2", spo2, 2, "on_air" if oxygen == "room_air" else "on_oxygen") if spo2 >= 93 else points("spo2", spo2, 2)
    total += 2 if oxygen != "room_air" else 0
    total += 0 if acvpu == "A" else 3
    return total


def single_red(hr, sbp, rr, spo2, temp, acvpu="A") -> bool:
    """True if any single parameter scores 3 (triggers an urgent ward-doctor review whatever the total)."""
    parts = [points("respiratory_rate", rr), points("systolic_bp", sbp), points("heart_rate", hr), points("temperature_c", temp), points("spo2", spo2, 1), 0 if acvpu == "A" else 3]
    return 3 in parts
