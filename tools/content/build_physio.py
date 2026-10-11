"""Builds content/physio/model.json, couplings.json, noise.json and effects.json: the LitePhysio engine's variables, couplings and noise (p0-physiology-events-behaviour-s01 and s02).

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
REF_EFFECTS = "SR-2860"
REF_PATTERNS = "SR-2861"
REF_DISPLAY = "SR-2862"


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


def eff(id, targets, onset, peak, plateau, offset, note):
    return {"id": id, "targets": targets, "profile": {"onset_seconds": onset, "peak_seconds": peak, "plateau_seconds": plateau, "offset_seconds": offset}, "source": "design", "note": note, "ref": REF_EFFECTS}


# What each treatment effect does to the patient (p0-physiology-events-behaviour-s05, DT-068). Magnitudes are simulation design values (provisional, SR-2860): only the direction is clinical; they are shown as plots
# in the review pack before a case depends on them. A target is the change the variable is pulled toward at full effect; the couplings then move heart rate and pressure with pain.
EFFECTS = [
    eff("EFF-ANALGESIA", {"pain": -3}, 300, 600, 3600, 3600, "Pain falls (and, through the pain couplings, so do heart rate and blood pressure a little)"),
    eff("EFF-ANTIEMETIC", {"nausea": -4}, 300, 600, 7200, 3600, "Nausea settles"),
    eff("EFF-FLUID-BOLUS", {"SBP": 10, "DBP": 5, "HR": -6}, 0, 600, 1800, 3600, "Circulating volume rises: blood pressure up, heart rate down"),
    eff("EFF-OXYGEN", {"SpO2": 4}, 30, 120, 7200, 300, "Oxygen saturation rises"),
    eff("EFF-ANTIBIOTIC", {}, 0, 1, 0, 0, "Acts on the deterioration clock (DeteriorationSubsystem), not on a vital sign"),
    eff("EFF-ADRENALINE", {"SBP": 25, "HR": 20, "SpO2": 4, "RR": -4}, 30, 120, 600, 900, "Allergic reaction reverses: pressure and oxygen recover, breathing eases"),
    eff("EFF-GLUCOSE-RISE", {"glucose": 4}, 60, 600, 1800, 3600, "Low blood sugar corrects"),
    eff("EFF-POTASSIUM-LOWER", {}, 0, 1, 0, 0, "Potassium is a result value, not a vital sign: the model carries no variable for it yet, so a repeat result is not generated (DT-074)"),
    eff("EFF-OPIOID-REVERSAL", {"RR": 6, "SpO2": 3, "sedation": -2}, 60, 180, 1800, 1800, "Slow breathing and drowsiness from an opioid reverse"),
    eff("EFF-ANAPHYLAXIS", {"SBP": -40, "DBP": -25, "HR": 35, "RR": 8, "SpO2": -8}, 0, 120, 3600, 1800, "An allergen given: the walkthrough 14.4 pattern (pressure falls, pulse and breathing rise, oxygen falls), improving after adrenaline"),
]
# The nine vital-sign patterns of walkthrough 4.11, as bundles that push a patient from the "all normal" row to the example numbers. A number the book does not give is not invented; sleepiness is a design value.
# The pain couplings already add heart rate, pressure and breathing rate for the pain score, so that share is taken out of the bundle (a pattern and a coupling must never count the same change twice).
NORMAL = {"HR": 76, "SBP": 124, "DBP": 78, "RR": 14, "SpO2": 98, "Temp": 36.7}
PATTERNS = [
    ("PAT-NORMAL", "All normal, calm", {}, "Stable: continue"),
    ("PAT-PAIN", "Pain pattern", {"HR": 104, "SBP": 146, "DBP": 88, "RR": 20, "Temp": 37.0, "pain": 8}, "Pain is driving the numbers: pain relief, then re-measure"),
    ("PAT-INFECTION", "Infection pattern", {"HR": 108, "Temp": 38.6, "RR": 20}, "Infection or inflammation: find the source, bloods, antibiotics per plan (blood pressure stays normal)"),
    ("PAT-DEHYDRATION", "Dehydration pattern", {"HR": 112, "SBP": 104, "DBP": 66}, "Not enough fluid: drip and fluids, kidney blood tests, re-measure (dry mouth, little urine and vomiting are history and examination, not vital signs)"),
    ("PAT-SEPSIS", "Sepsis pattern", {"HR": 128, "SBP": 92, "DBP": 58, "RR": 26, "Temp": 39.2}, "Sepsis with poor circulation: sepsis bundle, emergency call (lactate 3.8 is a result, not a vital sign)"),
    ("PAT-BLEEDING", "Bleeding pattern", {"HR": 122, "SBP": 96, "DBP": 60}, "Blood loss (for example ectopic pregnancy): emergency, two drips, blood ready, senior and gynaecology (pale, cold and falling Hb are examination and results)"),
    ("PAT-ALLERGY", "Allergy pattern", {"SBP": 84, "DBP": 50}, "Anaphylaxis: stop the drug, adrenaline, call for help (rash, swollen lips and wheeze are examination findings; only the pressure is numeric in the book)"),
    ("PAT-OPIOID", "Too-much-painkiller pattern", {"RR": 8, "SpO2": 89, "sedation": 3}, "Opioid overdose: stimulate, oxygen, call for help, antidote by a clinician (very sleepy is the design value sedation 3 of 3; small pupils are examination)"),
    ("PAT-AF", "Rhythm pattern", {"HR": 128, "SBP": 132, "DBP": 84}, "Atrial fibrillation: ECG, senior or medical review (the irregular pulse is shown by the pulse examination and ECG, not by a vital-sign variable)"),
]


def pattern_effect(id, label, example, move):
    pain = example.get("pain", 0)
    targets = {}
    for k, v in example.items():
        targets[k] = v - NORMAL[k] if k in NORMAL else v
    for c in COUPLINGS:
        if c["source"] == "pain" and pain and c["target"] in targets:
            targets[c["target"]] = targets[c["target"]] - c["gain"] * pain
    targets = {k: (round(v, 3) if isinstance(v, float) else v) for k, v in targets.items()}
    return eff(id, targets, 0, 300, 86400, 600, label + ": " + move)


EFFECTS += [pattern_effect(*p) for p in PATTERNS]

# Drivers: a variable that keeps moving on its own until a treatment stops it (design rates, provisional, SR-2861)
DRIVERS = [
    {"id": "DRV-DEHYDRATION", "variable": "volume_deficit", "rate_per_hour": 150, "stopped_by": ["EFF-FLUID-BOLUS"], "relief_per_dose": 500,
     "note": "Fluid is lost steadily (vomiting, poor intake) until a bolus is given; the deficit then falls by one bolus", "ref": REF_PATTERNS},
]
NOISE_FOR = {"HR": "heart_rate", "SBP": "nibp_systolic", "DBP": "nibp_diastolic", "RR": "respiratory_rate", "SpO2": "spo2", "Temp": "temperature_c"}


# Display zones (walkthrough Part 17) are what the monitor colours; NEWS2 scores with its own bands (WT-I04: they differ on purpose, so the two tables are never merged).
# Only the parameters that are vital-sign variables are here; a band that depends on context (breathing 22 or more "with infection") or has no number (pain "uncontrolled") is left out and says so.
DISPLAY_PARAMETERS = {"heart_rate": 0, "systolic_bp_low": 0, "respiratory_rate": 0, "spo2": 0, "temperature_c": 1, "pain_score": 0}
ZONE_ORDER = ["critical", "emergency", "worry", "watch", "normal"]


def display_zones() -> dict:
    with open(CONTENT / "vitals_bands.csv", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    params = {}
    for name, digits in DISPLAY_PARAMETERS.items():
        out = []
        for r in rows:
            if r["parameter"] != name or r["v_condition"] or r["v_state"]:
                continue
            lo = float(r["v_low"]) if r["v_low"] else None
            hi = float(r["v_high"]) if r["v_high"] else None
            if lo is None and hi is None:
                continue
            if name == "pain_score" and r["zone"] == "emergency":
                continue
            lo = None if lo is None else (int(lo) if digits == 0 else lo)
            hi = None if hi is None else (int(hi) if digits == 0 else hi)
            out.append({"zone": r["zone"], "low": lo, "high": hi, "high_exclusive": r["bound"] == "below" and hi is not None, "ref": r["ref"]})
        out.sort(key=lambda x: (ZONE_ORDER.index(x["zone"]), x["low"] if x["low"] is not None else -1e9))
        params[name] = {"digits": digits, "rows": out}
    artefacts = {"spo2_probe": {"problems": ["cold_fingers", "nail_polish", "movement"], "reading_drop": 6, "quality": "poor_pleth", "ref": REF_DISPLAY,
                                "note": "The book says finger probes read falsely low with cold fingers, nail polish or movement; the size of the drop (6 points) is a design value, and re-siting the probe clears it"}}
    return {"parameters": params, "artefacts": artefacts, "left_out": ["breathing 22 or more with infection (context)", "pain uncontrolled (no number)", "capillary refill, urine, shock index, alertness (not vital-sign variables here)"], "refs": [REF_DISPLAY]}


def pregnancy() -> dict:
    return {"hr_rise_min": 10, "hr_rise_max": 20, "hr_rise_used": 15, "news2_used": False, "maternity_score": "placeholder: the hospital chooses the maternity early-warning score",
            "wbc": {"number": None, "note": "the book says pregnancy raises the white cell count normally but gives no figure, so none is applied"},
            "note": "Walkthrough S2-D: pregnancy raises the heart rate by about 10 to 20 a minute (design midpoint 15 for a generated patient); blood pressure is often lower in mid-pregnancy (no figure given). Authored case files already include pregnancy in their values.",
            "ref": REF_DISPLAY}


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
    listed = {e for t in json.loads((CONTENT / "treatments.json").read_text(encoding="utf-8"))["treatments"] for e in t["effects"]}
    missing = sorted(listed - {e["id"] for e in EFFECTS})
    if missing:
        raise SystemExit(f"treatments name effects with no row in EFFECTS: {missing}")
    for e in EFFECTS:
        unknown = [k for k in e["targets"] if k not in ids]
        if unknown:
            raise SystemExit(f"{e['id']} targets variables the model does not have: {unknown}")
    effects_doc = {"effects": EFFECTS, "drivers": DRIVERS, "refs": [REF_EFFECTS, REF_PATTERNS]}
    dump = lambda d: json.dumps(d, indent=1, ensure_ascii=False) + "\n"
    return {"model.json": dump(model), "couplings.json": dump(couplings), "noise.json": dump(noise_doc), "effects.json": dump(effects_doc), "display_zones.json": dump(display_zones()), "pregnancy.json": dump(pregnancy())}


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
