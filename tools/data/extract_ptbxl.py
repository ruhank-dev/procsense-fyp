"""Extracts lead-II beat templates from PTB-XL (PhysioNet, open access, CC BY 4.0; Wagner et al., Scientific Data 2020) into tools/data/derived/ptbxl_ecg_templates.json.

    uv run --project tools tools/data/extract_ptbxl.py [--per-class 25]

For three rhythms (normal sinus, sinus tachycardia, atrial fibrillation) it takes human-validated 10-second recordings at 100 Hz, finds the R peaks in lead II,
averages the beats aligned on the R peak and keeps ONE short template per rhythm (75 points = 0.75 s) plus the heart-rate and beat-to-beat variation of the class.
Only that average leaves the script: no recording is kept. Atrial fibrillation also keeps the spread of beat intervals (irregularly irregular).
Needs the network (downloads the metadata file and about 3 x 25 small recordings) and takes a minute or two. CI never runs it; it checks the committed result.
"""
from __future__ import annotations

import argparse
import ast
import csv
import io
import json
import statistics
import struct
import sys
import time
import urllib.request
from pathlib import Path

BASE = "https://physionet.org/files/ptb-xl/1.0.3/"
OUT = Path(__file__).resolve().parent / "derived" / "ptbxl_ecg_templates.json"
CLASSES = {"sinus_rhythm": {"scp": "NORM", "rhythm": "SR"}, "sinus_tachycardia": {"scp": None, "rhythm": "STACH"}, "atrial_fibrillation": {"scp": None, "rhythm": "AFIB"}}
FS = 100
BEFORE, AFTER = 25, 50   # samples around the R peak (0.25 s before, 0.5 s after)


CACHE = Path(__file__).resolve().parent / "cache" / "ptbxl"   # git-ignored: a rerun does not download again


def get(path: str, binary: bool = False, tries: int = 4):
    cached = CACHE / path
    if cached.exists():
        data = cached.read_bytes()
        return data if binary else data.decode("utf-8")
    for n in range(tries):
        try:
            req = urllib.request.Request(BASE + path, headers={"User-Agent": "procsense-fyp-data-extract"})
            with urllib.request.urlopen(req, timeout=120) as r:
                data = r.read()
            cached.parent.mkdir(parents=True, exist_ok=True)
            cached.write_bytes(data)
            return data if binary else data.decode("utf-8")
        except Exception:   # noqa: BLE001
            if n == tries - 1:
                raise
            time.sleep(2 * (n + 1))


def read_lead_ii(base: str) -> list[float]:
    """WFDB format 16: interleaved little-endian int16 for 12 leads; lead II is channel 1."""
    header = get(base + ".hea").splitlines()
    n_sig = int(header[0].split()[1])
    gains, baselines = [], []
    for line in header[1:1 + n_sig]:
        parts = line.split()
        gain = float(parts[2].split("(")[0].split("/")[0])
        gains.append(gain)
        baselines.append(int(parts[4]) if len(parts) > 4 else 0)
    raw = get(base + ".dat", binary=True)
    n = len(raw) // (2 * n_sig)
    ints = struct.unpack("<" + "h" * (n * n_sig), raw[: n * n_sig * 2])
    return [(ints[i * n_sig + 1] - baselines[1]) / gains[1] for i in range(n)]


def r_peaks(x: list[float]) -> list[int]:
    """R peaks: local maxima of the squared first difference-smoothed signal above a threshold, at least 0.3 s apart."""
    d = [x[i + 1] - x[i] for i in range(len(x) - 1)]
    sq = [v * v for v in d]
    sm = [sum(sq[max(0, i - 2):i + 3]) / 5 for i in range(len(sq))]
    thr = 0.4 * max(sm)
    peaks, last = [], -999
    for i in range(2, len(sm) - 2):
        if sm[i] > thr and sm[i] >= max(sm[i - 2:i + 3]) and i - last > 0.3 * FS:
            lo, hi = max(0, i - 4), min(len(x), i + 6)
            top = max(range(lo, hi), key=lambda j: x[j])   # the R apex sits next to the steepest slope
            peaks.append(top)
            last = i
    return peaks


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--per-class", type=int, default=25)
    per_class = ap.parse_args().per_class
    rows = list(csv.DictReader(io.StringIO(get("ptbxl_database.csv"))))
    result = {"dataset": "PTB-XL", "version": "1.0.3", "licence": "CC BY 4.0", "citation": "Wagner P, et al. PTB-XL, a large publicly available electrocardiography dataset. Sci Data 2020.",
              "doi": "10.13026/kfzx-aw45", "sampling_hz": FS, "lead": "II", "samples_before_r": BEFORE, "samples_after_r": AFTER, "per_class_records": per_class, "classes": {}}
    for name, spec in CLASSES.items():
        picked = []
        for r in rows:
            codes = ast.literal_eval(r["scp_codes"])
            if r["validated_by_human"] != "True" or "PACE" in codes:
                continue
            rhythm_codes = {c for c in codes if c in ("SR", "STACH", "SBRAD", "AFIB", "AFLT", "SVTAC", "PSVT", "SARRH", "BIGU", "TRIGU", "PACE")}
            # rhythm statements carry likelihood 0 in PTB-XL (they are labels, not probabilities), so presence is what counts; the record must show that rhythm only
            if spec["scp"] and codes.get(spec["scp"], 0) >= 100 and rhythm_codes == {"SR"}:
                picked.append(r)
            elif not spec["scp"] and rhythm_codes == {spec["rhythm"]}:
                picked.append(r)
        picked.sort(key=lambda r: int(r["ecg_id"]))
        step = max(1, len(picked) // per_class)
        picked = picked[::step][:per_class]
        beats, rates, cvs = [], [], []
        for r in picked:
            x = read_lead_ii(r["filename_lr"])
            peaks = r_peaks(x)
            peaks = [p for p in peaks if p - BEFORE >= 0 and p + AFTER < len(x)]
            if len(peaks) < 4:
                continue
            rr = [(peaks[i + 1] - peaks[i]) / FS for i in range(len(peaks) - 1)]
            rates.append(60 / statistics.fmean(rr))
            cvs.append(statistics.pstdev(rr) / statistics.fmean(rr))
            baseline = statistics.median(x)
            amp = max(x[p] for p in peaks) - baseline or 1.0
            for p in peaks:
                beats.append([(x[p - BEFORE + k] - baseline) / amp for k in range(BEFORE + AFTER)])
            print(f"{name}: record {r['ecg_id']} ({len(peaks)} beats)", file=sys.stderr)
        template = [round(statistics.fmean(b[k] for b in beats), 4) for k in range(BEFORE + AFTER)]
        result["classes"][name] = {"records_used": len(rates), "beats_averaged": len(beats), "heart_rate_median": round(statistics.median(rates), 1),
                                   "heart_rate_p5": round(sorted(rates)[int(0.05 * (len(rates) - 1))], 1), "heart_rate_p95": round(sorted(rates)[int(0.95 * (len(rates) - 1))], 1),
                                   "rr_variation_cv_median": round(statistics.median(cvs), 3), "template_r_normalised": template}
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(result, indent=1) + "\n", encoding="utf-8")
    print(json.dumps({k: {x: v[x] for x in ("records_used", "beats_averaged", "heart_rate_median", "rr_variation_cv_median")} for k, v in result["classes"].items()}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
