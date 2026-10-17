"""Waveform profiles: which ECG rhythms the cases need, how many real recordings PTB-XL holds for each, and how those recordings look (p1-dataset-analysis-s03).

    uv run --project tools tools/data/profile_waveforms.py            # write tools/data/derived/profiles/waveforms.json and docs/research/datasets/profiles/waveforms.md
    uv run --project tools tools/data/profile_waveforms.py --check    # the committed profile covers every ECG finding of the imaging library with a source or a stated plan

Needs the PTB-XL metadata file and some small 100 Hz records (about 40 per class, cached in the git-ignored tools/data/cache/ptbxl; the first run downloads them, a few minutes).
Only aggregates are written. Measured per class from lead II: heart rate, beat-to-beat variation, QRS width (from the steepest slope, at 100 Hz one sample is 10 ms, so it is coarse), and the noise
(the part of the signal a 5-point average removes, as a share of the R height). PR and QT are NOT measured: a delineator on a 100 Hz recording is not reliable, so they stay a stated gap (PTB-XL+ or 500 Hz records
would be the way; neither is used yet). VitalDB monitoring behaviour is in `vitaldb_noise.json` and is quoted, not recomputed.
PTB-XL labels are statements by a cardiologist for the whole 10 s recording, not beat-level labels.
"""
from __future__ import annotations

import ast
import csv
import io
import json
import statistics
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_ptbxl as xp  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
OUT = HERE / "derived" / "profiles" / "waveforms.json"
REPORT = ROOT / "docs" / "research" / "datasets" / "profiles" / "waveforms.md"
LIBRARY = ROOT / "content" / "imaging_library.csv"
PER_CLASS = 40
RHYTHM_CODES = ("SR", "STACH", "SBRAD", "AFIB", "AFLT", "SVTAC", "PSVT", "SARRH", "BIGU", "TRIGU", "PACE")

# ECG library code -> how PTB-XL labels it. "plan" is what is done where PTB-XL has no label.
CLASSES = {
    "ECG-01": {"name": "normal sinus rhythm", "select": lambda c, r: c.get("NORM", 0) >= 100 and r == {"SR"}},
    "ECG-02": {"name": "sinus tachycardia", "select": lambda c, r: r == {"STACH"}},
    "ECG-03": {"name": "atrial fibrillation", "select": lambda c, r: r == {"AFIB"}},
    "ECG-04": {"name": "sinus bradycardia", "select": lambda c, r: r == {"SBRAD"}},
    "ECG-05": {"name": "complete heart block", "select": lambda c, r: "3AVB" in c},
    "ECG-06": {"name": "supraventricular tachycardia", "select": lambda c, r: bool(r & {"SVTAC", "PSVT"})},
    "ECG-07": {"name": "inferior myocardial infarction (ST elevation pattern)", "select": lambda c, r: c.get("IMI", 0) >= 100},
    "ECG-08": {"name": "hyperkalaemia (peaked T waves)", "select": None,
               "plan": "PTB-XL has no hyperkalaemia statement. The 4 library pictures are synthetic; a live generator makes the pattern from parameters (T-wave height and QRS width as a function of potassium), and only after the cardiology reference for those parameters is read (OQ-05)."},
    "ECG-09": {"name": "hypokalaemia (flat T waves with U waves)", "select": None,
               "plan": "PTB-XL has no hypokalaemia statement (LOWT, low T amplitude, is the nearest). The 4 library pictures are synthetic; same parametric plan as hyperkalaemia (OQ-05)."},
    "ECG-10": {"name": "atrial fibrillation with a fast ventricular rate", "select": lambda c, r: r == {"AFIB"}, "min_rate": 110.0, "sample": 200},
}


def metadata() -> list[dict]:
    return list(csv.DictReader(io.StringIO(xp.get("ptbxl_database.csv"))))


def pick(rows: list[dict], select, n: int) -> tuple[int, list[dict]]:
    """How many human-validated, pacemaker-free records carry the label, and n of them spread evenly by id."""
    found = []
    for r in rows:
        codes = ast.literal_eval(r["scp_codes"])
        if r["validated_by_human"] != "True" or "PACE" in codes:
            continue
        rhythm = {c for c in codes if c in RHYTHM_CODES}
        if select(codes, rhythm):
            found.append(r)
    found.sort(key=lambda r: int(r["ecg_id"]))
    step = max(1, len(found) // n)
    return len(found), found[::step][:n]


def qrs_width_ms(x: list[float], peak: int) -> float | None:
    """Width of the steep part around an R peak: samples whose slope is at least 30 percent of the steepest slope near the peak, joined into one run, in milliseconds (100 Hz: 10 ms a sample)."""
    lo, hi = max(1, peak - 12), min(len(x) - 1, peak + 12)
    slope = [abs(x[i] - x[i - 1]) for i in range(lo, hi)]
    if not slope or max(slope) == 0:
        return None
    thr = 0.3 * max(slope)
    idx = [lo + i for i, s in enumerate(slope) if s >= thr]
    return (idx[-1] - idx[0] + 1) * (1000 / xp.FS)


def noise_share(x: list[float], peaks: list[int]) -> float | None:
    """Standard deviation of (signal minus its 5-point average) as a share of the median R height above the baseline."""
    if len(x) < 10 or not peaks:
        return None
    sm = [sum(x[max(0, i - 2):i + 3]) / len(x[max(0, i - 2):i + 3]) for i in range(len(x))]
    resid = [a - b for a, b in zip(x, sm)]
    base = statistics.median(x)
    height = statistics.median(x[p] - base for p in peaks)
    return statistics.pstdev(resid) / height if height > 0 else None


def measure(rec: dict) -> dict | None:
    x = xp.read_lead_ii(rec["filename_lr"])
    peaks = [p for p in xp.r_peaks(x) if 12 <= p < len(x) - 12]
    if len(peaks) < 4:
        return None
    rr = [(peaks[i + 1] - peaks[i]) / xp.FS for i in range(len(peaks) - 1)]
    widths = [w for w in (qrs_width_ms(x, p) for p in peaks) if w is not None]
    return {"rate": 60 / statistics.fmean(rr), "rr_cv": statistics.pstdev(rr) / statistics.fmean(rr), "qrs_ms": statistics.median(widths) if widths else None, "noise": noise_share(x, peaks)}


def spread(xs: list[float], digits: int = 1) -> dict:
    xs = sorted(v for v in xs if v is not None)
    if not xs:
        return {"n": 0}
    def q(p):
        k = (len(xs) - 1) * p / 100
        f, c = int(k), min(int(k) + 1, len(xs) - 1)
        return round(xs[f] + (xs[c] - xs[f]) * (k - f), digits)
    return {"n": len(xs), "p5": q(5), "p50": q(50), "p95": q(95)}


def library_findings() -> dict[str, str]:
    return {r["finding_code"]: r["finding"] for r in csv.DictReader(LIBRARY.open(encoding="utf-8", newline="")) if r["test"] == "ecg"}


def build() -> dict:
    rows = metadata()
    findings = library_findings()
    out = {"dataset": "PTB-XL", "version": "1.0.3", "licence": "CC BY 4.0", "records_per_class": PER_CLASS, "sampling_hz": xp.FS, "lead": "II", "classes": {},
           "not_measured": ["PR interval", "QT interval", "ST level", "T-wave height"], "why_not_measured": "a delineator on a 100 Hz recording is not reliable; PTB-XL+ or the 500 Hz records would be needed (not used yet)"}
    for code, spec in CLASSES.items():
        entry: dict = {"name": spec["name"], "library_finding": findings.get(code, ""), "ptbxl_label_available": spec["select"] is not None}
        if spec["select"] is None:
            entry.update(available_records=0, plan=spec["plan"])
            out["classes"][code] = entry
            continue
        available, picked = pick(rows, spec["select"], spec.get("sample", PER_CLASS))
        entry["available_records"] = available
        ms = [m for m in (measure(r) for r in picked) if m]
        if spec.get("min_rate"):
            ms = [m for m in ms if m["rate"] >= spec["min_rate"]]
            entry["selection_note"] = f"atrial fibrillation records with a measured rate of at least {spec['min_rate']:.0f} per minute"
        entry["measured_records"] = len(ms)
        if ms:
            entry["rate_per_min"] = spread([m["rate"] for m in ms])
            entry["rr_variation_cv"] = spread([m["rr_cv"] for m in ms], 3)
            entry["qrs_width_ms_coarse"] = spread([m["qrs_ms"] for m in ms])
            entry["noise_share_of_r_height"] = spread([m["noise"] for m in ms], 3)
        else:
            entry["plan"] = "no record passed the measurement: " + ("none in the 100 Hz sample" if available else "no labelled record")
        if available and available < 100:
            entry["thin"] = f"only {available} labelled records: weak"
        elif ms and len(ms) < 30:
            entry["thin"] = f"only {len(ms)} records measured: weak"
        if code in ("ECG-03", "ECG-10"):
            entry["caveat"] = "a rate below 40 per minute here is a peak finder that missed beats in an irregular rhythm, not a real rate; read the rate columns of this class as approximate"
        out["classes"][code] = entry
    vd = json.loads((HERE / "derived" / "vitaldb_noise.json").read_text(encoding="utf-8"))
    out["monitor_behaviour"] = {"source": "tools/data/derived/vitaldb_noise.json", "variables": {k: {x: v.get(x) for x in ("level_p5", "level_p50", "level_p95", "sigma_median", "phi_median")} for k, v in vd["variables"].items()},
                                "episode_slopes": "not measured: VitalDB episodes (hypotension, desaturation) are the physiology step's drivers (content/physio); a profile of real episode slopes needs the full waveform downloads, not the 40 cases kept as aggregates"}
    return out


def report(p: dict) -> str:
    L = ["# Waveform profiles: ECG and monitor signals", "",
         "Generated by `tools/data/profile_waveforms.py` (p1-dataset-analysis-s03); do not edit. Aggregates only.", "",
         "PTB-XL 1.0.3 (CC BY 4.0, DS-0002): 100 Hz, lead II, up to 40 human-validated records per class. Labels are statements for the whole recording. QRS width is coarse (one sample is 10 ms). "
         f"Not measured: {', '.join(p['not_measured'])} ({p['why_not_measured']}).", "",
         "## Coverage of the ECG library", "", "| Library | Finding | In PTB-XL | Records measured | Rate /min (p5 / p50 / p95) | Beat variation (CV p50) | QRS ms (p50) | Noise share (p50) | Note |", "|---|---|---:|---:|---|---|---|---|---|"]
    for code, c in p["classes"].items():
        if c["available_records"]:
            rate = c.get("rate_per_min", {})
            L.append(f"| {code} | {c['library_finding'] or c['name']} | {c['available_records']} | {c['measured_records']} | {rate.get('p5', '—')} / {rate.get('p50', '—')} / {rate.get('p95', '—')} | {c.get('rr_variation_cv', {}).get('p50', '—')} | {c.get('qrs_width_ms_coarse', {}).get('p50', '—')} | {c.get('noise_share_of_r_height', {}).get('p50', '—')} | {c.get('thin', c.get('selection_note', ''))}{' ' + c['caveat'] if 'caveat' in c else ''} |")
        else:
            L.append(f"| {code} | {c['library_finding'] or c['name']} | 0 | 0 | — | — | — | — | {c['plan']} |")
    L += ["", "## Monitor behaviour (VitalDB, from the earlier aggregates)", "", "| Variable | Level p5 | p50 | p95 | Noise sigma (median) | Noise phi (median) |", "|---|---:|---:|---:|---:|---:|"]
    for k, v in p["monitor_behaviour"]["variables"].items():
        L.append(f"| {k} | {v['level_p5']} | {v['level_p50']} | {v['level_p95']} | {v['sigma_median']} | {v['phi_median']} |")
    L += ["", p["monitor_behaviour"]["episode_slopes"], "",
          "## Design note for the ECG generator (p1-live-scenario-generation-s06)", "",
          "Rhythms with real recordings (normal, tachycardia, atrial fibrillation, bradycardia, heart block, SVT, inferior MI) are chosen from PTB-XL by label and rate and shown through the approved picture library. "
          "A rate or noise outside what the table shows for the class is not generated. Hyperkalaemia and hypokalaemia have no PTB-XL label: the generator uses the parametric pattern only after the cardiology reference "
          "for the parameters is read (OQ-05); NeuroKit2 `ecg_simulate` can match a class's rate and noise but does not model electrolyte changes, so it is not enough for those two.", ""]
    return "\n".join(L)


def check() -> int:
    problems = []
    if not OUT.exists() or not REPORT.exists():
        print("FAIL  the waveform profile or its report is missing: run profile_waveforms.py")
        return 1
    p = json.loads(OUT.read_text(encoding="utf-8"))
    for code in library_findings():
        c = p["classes"].get(code)
        if c is None:
            problems.append(f"{code}: in the ECG library but not in the profile")
        elif not c["available_records"] and not c.get("plan"):
            problems.append(f"{code}: no source records and no plan")
        elif c["available_records"] and not c.get("measured_records"):
            problems.append(f"{code}: records exist but none was measured")
    for p_ in problems:
        print("FAIL  " + p_)
    if not problems:
        print(f"PASS  {len(p['classes'])} ECG findings each have measured PTB-XL records or a stated plan")
    return 1 if problems else 0


def main(argv: list[str]) -> int:
    if "--check" in argv:
        return check()
    p = build()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(p, indent=1, sort_keys=True) + "\n", encoding="utf-8")
    REPORT.write_text(report(p), encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT).as_posix()} and {REPORT.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
