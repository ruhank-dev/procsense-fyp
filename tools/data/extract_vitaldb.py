"""Extracts vital-sign noise parameters from VitalDB (open access, CC BY 4.0; Lee et al., Scientific Data 2022) into tools/data/derived/vitaldb_noise.json.

    uv run --project tools tools/data/extract_vitaldb.py [--cases 60]

What is taken: only aggregates. For each vital sign: how much it wanders around its own slow level (sigma of the residual after a rolling median) and how
strongly one reading follows the previous one (lag-1 autocorrelation, the AR(1) phi), plus percentiles of each patient's typical level. No patient row is kept
or shipped. Cases are chosen deterministically (planned operation, ASA 1-2, age 18-70, general surgery, every k-th by case id), so a rerun gives the same sample.
Limitation: VitalDB is intra-operative (anaesthetised) monitoring, so the values describe stable monitored patients, not an awake patient on a ward bay.
The script needs the network and takes a few minutes; CI never runs it (it checks the committed result instead).
"""
from __future__ import annotations

import argparse
import csv
import gzip
import io
import json
import statistics
import sys
import time
import urllib.request
from pathlib import Path

API = "https://api.vitaldb.net"
OUT = Path(__file__).resolve().parent / "derived" / "vitaldb_noise.json"
TRACKS = {  # our name -> (VitalDB track, unit, plausible range)
    "heart_rate": ("Solar8000/HR", "/min", (30, 200)),
    "spo2": ("Solar8000/PLETH_SPO2", "%", (70, 100)),
    "nibp_systolic": ("Solar8000/NIBP_SBP", "mmHg", (50, 260)),
    "nibp_diastolic": ("Solar8000/NIBP_DBP", "mmHg", (20, 160)),
    "respiratory_rate": ("Solar8000/RR", "/min", (4, 40)),
    "temperature_c": ("Solar8000/BT", "C", (33, 41)),
}
WINDOW = 31   # samples in the rolling median (about one minute at 2 s per sample)


def get(path: str, tries: int = 4) -> str:
    for n in range(tries):
        try:
            req = urllib.request.Request(f"{API}/{path}", headers={"Accept-Encoding": "gzip", "User-Agent": "procsense-fyp-data-extract"})
            with urllib.request.urlopen(req, timeout=60) as r:
                raw = r.read()
            return (gzip.decompress(raw) if raw[:2] == b"\x1f\x8b" else raw).decode("utf-8-sig")
        except Exception as e:   # noqa: BLE001 - network hiccups are retried, then reported
            if n == tries - 1:
                raise
            time.sleep(2 * (n + 1))
    raise AssertionError


def rolling_median(xs: list[float], w: int) -> list[float]:
    half = w // 2
    return [statistics.median(xs[max(0, i - half):i + half + 1]) for i in range(len(xs))]


def distinct_readings(values: list[float]) -> list[float]:
    """An intermittent measurement (the BP cuff) is held between readings: keep one value per new reading (each time the held value changes)."""
    out = [values[0]]
    for v in values[1:]:
        if v != out[-1]:
            out.append(v)
    return out


def residual_stats(values: list[float], window: int = WINDOW) -> tuple[float, float] | None:
    """(sigma, phi) of the part of the signal that moves around its slow level."""
    if len(values) < 3 * window:
        return None
    level = rolling_median(values, window)
    res = [v - l for v, l in zip(values, level)]
    sigma = statistics.pstdev(res)
    if sigma == 0:
        return sigma, 0.0
    mean = statistics.fmean(res)
    num = sum((res[i] - mean) * (res[i + 1] - mean) for i in range(len(res) - 1))
    den = sum((r - mean) ** 2 for r in res)
    return sigma, num / den


def pct(xs: list[float], p: float) -> float:
    xs = sorted(xs)
    k = (len(xs) - 1) * p / 100
    f, c = int(k), min(int(k) + 1, len(xs) - 1)
    return xs[f] + (xs[c] - xs[f]) * (k - f)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cases", type=int, default=60)
    n_cases = ap.parse_args().cases
    cases = list(csv.DictReader(io.StringIO(get("cases"))))
    eligible = [c for c in cases if c["department"] == "General surgery" and c["asa"] in ("1", "2") and c["emop"] == "0" and c["age"] and 18 <= float(c["age"]) <= 70]
    eligible.sort(key=lambda c: int(c["caseid"]))
    step = max(1, len(eligible) // n_cases)
    chosen = eligible[::step][:n_cases]
    tracks: dict[tuple[str, str], str] = {}
    for row in csv.DictReader(io.StringIO(get("trks"))):
        tracks[(row["caseid"], row["tname"])] = row["tid"]

    per_var: dict[str, dict] = {k: {"levels": [], "sigmas": [], "phis": [], "cases": 0, "samples": 0} for k in TRACKS}
    for i, case in enumerate(chosen, 1):
        for name, (tname, _, (lo, hi)) in TRACKS.items():
            tid = tracks.get((case["caseid"], tname))
            if not tid:
                continue
            rows = list(csv.reader(io.StringIO(get(tid))))[1:]
            values = [float(r[1]) for r in rows if len(r) > 1 and r[1] != "" and lo <= float(r[1]) <= hi]
            if name.startswith("nibp"):
                values = distinct_readings(values)   # one value per cuff reading; a 7-reading window instead of one minute
                stats = residual_stats(values, 7)
            else:
                stats = residual_stats(values)
            if stats is None:
                continue
            v = per_var[name]
            v["levels"].append(statistics.median(values))
            v["sigmas"].append(stats[0])
            v["phis"].append(stats[1])
            v["cases"] += 1
            v["samples"] += len(values)
        print(f"case {i}/{len(chosen)} ({case['caseid']})", file=sys.stderr)

    result = {
        "dataset": "VitalDB", "version": "API as read on 2026-10-10", "licence": "CC BY 4.0", "citation": "Lee HC, Park Y, Yoon SB, et al. VitalDB, a high-fidelity multi-parameter vital signs database in surgical patients. Sci Data 2022.",
        "selection": "planned (emop 0), ASA 1-2, age 18-70, General surgery, every k-th case by id", "cases_selected": len(chosen), "eligible_cases": len(eligible),
        "window_samples": WINDOW, "nibp_note": "blood pressure is an intermittent cuff reading: one value per reading, window of 7 readings",
        "limitation": "intra-operative monitoring of anaesthetised patients; respiratory rate is the monitor/ventilator value and is the least reliable of the six",
        "variables": {k: {"track": TRACKS[k][0], "unit": TRACKS[k][1], "cases": v["cases"], "samples": v["samples"],
                          "level_p5": round(pct(v["levels"], 5), 2) if v["levels"] else None, "level_p50": round(pct(v["levels"], 50), 2) if v["levels"] else None,
                          "level_p95": round(pct(v["levels"], 95), 2) if v["levels"] else None,
                          "sigma_median": round(statistics.median(v["sigmas"]), 3) if v["sigmas"] else None,
                          "phi_median": round(statistics.median(v["phis"]), 3) if v["phis"] else None} for k, v in per_var.items()},
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, indent=1) + "\n", encoding="utf-8")
    print(json.dumps(result["variables"], indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
