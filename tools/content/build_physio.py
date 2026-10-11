"""Builds content/physio/model.json, couplings.json and noise.json: the LitePhysio engine's variables, couplings and noise (p0-physiology-events-behaviour-s01 and s02).

    uv run --project tools tools/content/build_physio.py            # write
    uv run --project tools tools/content/build_physio.py --check    # fail if the files are out of date

Sources: the guide's B-R13 (bounds HR 30-200, SBP 50-220, RR 4-45, SpO2 60-100, Temp 34-41; relaxation time HR 60 s, BP 90 s, SpO2 30 s) and the VitalDB aggregates in content/physio_noise.csv (sigma and phi per
variable, verified in the data step). Everything else here is a simulation design value (provisional), registered as SR-2840 (model), SR-2841 (couplings and flags) and SR-2842 (noise caps): none of it is clinical.
The two flag rules reuse verified numbers: oxygen 91% or less (SR-0094) and glucose below 4.0 (SR-0556).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "physio"

REF_MODEL = "SR-2840"


def var(id, unit, lo, hi, tau, source):
    return {"id": id, "unit": unit, "min": lo, "max": hi, "tau_seconds": tau, "source": source, "ref": REF_MODEL}


VARIABLES = [
    var("HR", "/min", 30, 200, 60, "guide"), var("SBP", "mmHg", 50, 220, 90, "guide"), var("DBP", "mmHg", 30, 130, 90, "design"), var("RR", "/min", 4, 45, 30, "guide bounds, design tau"),
    var("SpO2", "%", 60, 100, 30, "guide"), var("Temp", "C", 34, 41, 300, "guide bounds, design tau"), var("glucose", "mmol/L", 1.0, 30.0, 300, "design"),
    var("pain", "0-10", 0, 10, 60, "design"), var("nausea", "0-10", 0, 10, 120, "design"), var("sedation", "0-3", 0, 3, 120, "design"), var("volume_deficit", "mL", 0, 3000, 600, "design"),
]
# a coupling moves a target in proportion to how far a source variable is from its baseline (design gains, provisional)
COUPLINGS = [
    {"id": "volume_hr", "source": "volume_deficit", "target": "HR", "gain": 0.01, "note": "a fluid deficit makes the heart beat faster"},
    {"id": "volume_sbp", "source": "volume_deficit", "target": "SBP", "gain": -0.008, "note": "and lowers the blood pressure"},
    {"id": "pain_hr", "source": "pain", "target": "HR", "gain": 1.5, "note": "pain raises the heart rate"},
    {"id": "pain_sbp", "source": "pain", "target": "SBP", "gain": 2.0, "note": "the blood pressure"},
    {"id": "pain_rr", "source": "pain", "target": "RR", "gain": 0.5, "note": "and the breathing rate"},
]
# flags the behaviour model reads: a rule over the variables
FLAGS = [
    {"id": "confusion", "when": {"<=": [{"var": "SpO2"}, 91]}, "why": "low oxygen (91% or less, the emergency sign)", "ref": "SR-0094"},
    {"id": "confusion", "when": {"<": [{"var": "glucose"}, 4.0]}, "why": "low sugar (below 4.0 mmol/L)", "ref": "SR-0556"},
]
NOISE_FOR = {"HR": "heart_rate", "SBP": "nibp_systolic", "DBP": "nibp_diastolic", "RR": "respiratory_rate", "SpO2": "spo2", "Temp": "temperature_c"}


def build() -> dict[str, str]:
    with open(CONTENT / "physio_noise.csv", encoding="utf-8", newline="") as f:
        rows = {r["variable"]: r for r in csv.DictReader(f)}
    ids = {v["id"] for v in VARIABLES}
    bad = sorted({c["source"] for c in COUPLINGS} | {c["target"] for c in COUPLINGS})
    unknown = [b for b in bad if b not in ids]
    if unknown:
        raise SystemExit(f"couplings on variables the model does not have: {unknown}")
    noise = []
    for v, src in NOISE_FOR.items():
        r = rows[src]
        sigma, phi = float(r["d_sigma"]), float(r["d_phi"])
        band = (float(r["d_level_p95"]) - float(r["d_level_p5"])) / 2          # the usual spread of the level in the dataset
        noise.append({"variable": v, "sigma": sigma, "phi": phi, "uncaused_band": round(band, 3), "cap": round(min(3 * sigma, band / 2), 3), "ref": r["ref"], "cap_ref": "SR-2842"})
    model = {"variables": VARIABLES, "noise_enabled": False, "noise_step_seconds": 1, "refs": [REF_MODEL]}
    couplings = {"couplings": COUPLINGS, "flags": FLAGS, "refs": ["SR-2841"]}
    noise_doc = {"noise": noise, "refs": ["SR-2842"]}
    dump = lambda d: json.dumps(d, indent=1, ensure_ascii=False) + "\n"
    return {"model.json": dump(model), "couplings.json": dump(couplings), "noise.json": dump(noise_doc)}


def main(argv: list[str]) -> int:
    files = build()
    if "--check" in argv:
        stale = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8") != t]
        if stale:
            print(f"FAIL  content/physio/{', '.join(stale)} out of date; run tools/content/build_physio.py")
            return 1
        print("content/physio is up to date")
        return 0
    OUT.mkdir(parents=True, exist_ok=True)
    for n, t in files.items():
        (OUT / n).write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files in content/physio")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
