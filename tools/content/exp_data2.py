"""EXP-DATA-2 (p0-medical-research-data README): generated lab values always stay inside the case's band.

    uv run --project tools tools/content/exp_data2.py            # 10,000 seeds
    uv run --project tools tools/content/exp_data2.py 2000        # fewer seeds

The rule the engine will use to make a result look real: take the authored value and add a small measurement jitter (analytical variation, 2% of the value), but never let the jitter move
the value into another band of content/lab_bands.csv. The experiment applies that rule to every numeric laboratory result of the 53 case files with 10,000 seeds and counts how often the
band changed (it must be 0) and how often the rule had to hold a value back at a band edge. The jitter uses Python's seeded random.Random, so the run is reproducible.
"""
from __future__ import annotations

import csv
import json
import random
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ANALYTE = {"WBC": "wbc", "CRP": "crp", "Creatinine": "creatinine", "Lactate": "lactate", "Potassium": "potassium", "Hb": "haemoglobin", "Haemoglobin": "haemoglobin", "HbA1c": "hba1c",
           "Bilirubin": "bilirubin", "INR": "inr", "ALP": "alp", "GGT": "ggt", "ALT": "alt", "Urea": "urea", "Sodium": "sodium", "Albumin": "albumin", "Lipase": "lipase",
           "Neutrophils": "neutrophils", "eGFR": "egfr", "Capillary ketones": "ketones", "Ketones": "ketones"}
CV = 0.02


def num(text: str):
    m = re.fullmatch(r"\s*(-?\d+(?:\.\d+)?)\s*", text or "")
    return float(m.group(1)) if m else None


def bands() -> list[dict]:
    with open(ROOT / "content" / "lab_bands.csv", encoding="utf-8", newline="") as f:
        return [b for b in csv.DictReader(f) if not b["basis"] and not b["v_state"]]


_PARSED: dict = {}


def zones(all_bands: list[dict], analyte: str, value: float, sex: str) -> frozenset[str]:
    key = (id(all_bands), analyte, sex)
    if key not in _PARSED:
        _PARSED[key] = [(num(b["v_low"]), num(b["v_high"]), b["bound"], b["zone"]) for b in all_bands if b["analyte"] == analyte and not (b["sex"] and sex and b["sex"] != sex)]
    out = set()
    for low, high, bound, zone in _PARSED[key]:
        if ((low is None) or (value > low if bound == "above" else value >= low)) and ((high is None) or (value < high if bound == "below" else value <= high)):
            out.add(zone)
    return frozenset(out)


def jitter(value: float, band_of, rng: random.Random) -> tuple[float, bool]:
    """The generator rule: value plus up to 2% jitter, held back (returned unchanged) if the jittered value would be in a different band."""
    moved = round(value * (1 + CV * rng.uniform(-1, 1)), 3)
    if band_of(moved) != band_of(value):
        return value, True
    return moved, False


def cases() -> list[dict]:
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "case_files").glob("*.json"))]


def run(seeds: int = 10000) -> dict:
    all_bands = bands()
    items = []
    for c in cases():
        for r in c["results"]:
            a, v = ANALYTE.get(r["test"].split(" (")[0]), num(r["value"])
            if a and v is not None and zones(all_bands, a, v, c["identity"]["sex"]):
                items.append((c["id"], r["id"], a, v, c["identity"]["sex"]))
    violations, held, total = [], 0, 0
    for seed in range(seeds):
        rng = random.Random(seed)
        for cid, rid, a, v, sex in items:
            new, was_held = jitter(v, lambda x, a=a, sex=sex: zones(all_bands, a, x, sex), rng)
            total += 1
            held += was_held
            if zones(all_bands, a, new, sex) != zones(all_bands, a, v, sex):
                violations.append((seed, cid, rid, v, new))
    return {"results": len(items), "seeds": seeds, "generated": total, "held_back_at_a_band_edge": held, "violations": violations}


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    out = run(int(argv[0]) if argv else 10000)
    print(f"{out['results']} laboratory results x {out['seeds']} seeds = {out['generated']} generated values; held back at a band edge: {out['held_back_at_a_band_edge']}; band changes: {len(out['violations'])}")
    for v in out["violations"][:10]:
        print("VIOLATION", v)
    return 1 if out["violations"] else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
