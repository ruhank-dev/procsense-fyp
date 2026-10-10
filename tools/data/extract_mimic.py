"""Aggregates from the MIMIC-IV clinical database demo and the MIMIC-IV-ED demo (PhysioNet, open access, ODbL 1.0) into tools/data/derived/.

    uv run --project tools tools/data/extract_mimic.py

Used ONLY to check that the walkthrough's bands and our generated values are plausible for adult hospital patients. No patient row is kept; only percentiles
leave the script. The aggregates are for reference and tests, they are not shipped in the app (ODbL: attribution required; share-alike applies to derived databases).
Units are converted only with the factors the walkthrough itself prints (bilirubin 1 mg/dL = 17.1 umol/L; glucose 1 mmol/L = 18 mg/dL; creatinine 1 mg/dL = 88.4 umol/L;
haemoglobin and albumin 1 g/dL = 10 g/L). Needs the network the first time (downloads about 16 MB into tools/data/cache, which is git-ignored).
"""
from __future__ import annotations

import csv
import gzip
import io
import json
import sys
import urllib.request
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
CACHE = HERE / "cache"
OUT = HERE / "derived"
DEMO_URL = "https://physionet.org/static/published-projects/mimic-iv-demo/mimic-iv-clinical-database-demo-2.2.zip"
ED_URL = "https://physionet.org/static/published-projects/mimic-iv-ed-demo/mimic-iv-ed-demo-2.2.zip"

# our analyte -> (MIMIC label, fluid, factor to our unit, our unit)
LABS = {
    "haemoglobin": ("Hemoglobin", "Blood", 10.0, "g/L"), "wbc": ("White Blood Cells", "Blood", 1.0, "x10^9/L"), "platelets": ("Platelet Count", "Blood", 1.0, "x10^9/L"),
    "sodium": ("Sodium", "Blood", 1.0, "mmol/L"), "potassium": ("Potassium", "Blood", 1.0, "mmol/L"), "creatinine": ("Creatinine", "Blood", 88.4, "umol/L"),
    "glucose": ("Glucose", "Blood", 1 / 18, "mmol/L"), "bilirubin": ("Bilirubin, Total", "Blood", 17.1, "umol/L"), "alt": ("Alanine Aminotransferase (ALT)", "Blood", 1.0, "U/L"),
    "alp": ("Alkaline Phosphatase", "Blood", 1.0, "U/L"), "albumin": ("Albumin", "Blood", 10.0, "g/L"), "lactate": ("Lactate", "Blood", 1.0, "mmol/L"),
    "inr": ("INR(PT)", "Blood", 1.0, "ratio"), "crp": ("C-Reactive Protein", "Blood", 1.0, "mg/L"),
}


def fetch(url: str, name: str) -> Path:
    CACHE.mkdir(exist_ok=True)
    path = CACHE / name
    if path.exists():
        try:
            zipfile.ZipFile(path).testzip()
            return path
        except zipfile.BadZipFile:
            path.unlink()
    have = 0
    for _ in range(6):   # the server sometimes closes early: resume until the archive is complete
        req = urllib.request.Request(url, headers={"User-Agent": "procsense-fyp-data-extract", "Range": f"bytes={path.stat().st_size if path.exists() else 0}-"})
        try:
            with urllib.request.urlopen(req, timeout=120) as r, open(path, "ab") as f:
                f.write(r.read())
        except Exception:   # noqa: BLE001
            pass
        try:
            zipfile.ZipFile(path).testzip()
            return path
        except zipfile.BadZipFile:
            continue
    raise RuntimeError(f"could not download a complete {name}")


def pct(xs: list[float], p: float) -> float:
    xs = sorted(xs)
    k = (len(xs) - 1) * p / 100
    f, c = int(k), min(int(k) + 1, len(xs) - 1)
    return xs[f] + (xs[c] - xs[f]) * (k - f)


def summary(xs: list[float], digits: int = 2) -> dict:
    return {"n": len(xs), **{f"p{p}": round(pct(xs, p), digits) for p in (5, 25, 50, 75, 95)}}


def read_gz(z: zipfile.ZipFile, name: str):
    base = next(n for n in z.namelist() if n.endswith(name))
    with z.open(base) as raw, gzip.open(raw, "rt", encoding="utf-8", newline="") as f:
        yield from csv.DictReader(f)


def labs() -> dict:
    z = zipfile.ZipFile(fetch(DEMO_URL, "mimic_iv_demo.zip"))
    items = {r["itemid"]: r for r in read_gz(z, "d_labitems.csv.gz")}
    wanted: dict[str, tuple[str, float]] = {}
    for key, (label, fluid, factor, _) in LABS.items():
        for iid, r in items.items():
            if r["label"] == label and r["fluid"] == fluid:
                wanted[iid] = (key, factor)
    values: dict[str, list[float]] = {k: [] for k in LABS}
    for r in read_gz(z, "labevents.csv.gz"):
        hit = wanted.get(r["itemid"])
        if hit and r["valuenum"]:
            values[hit[0]].append(float(r["valuenum"]) * hit[1])
    return {k: {"unit": LABS[k][3], "mimic_label": LABS[k][0], **summary(v)} for k, v in values.items() if v}


def ed_triage() -> dict:
    z = zipfile.ZipFile(fetch(ED_URL, "mimic_iv_ed_demo.zip"))
    name = next(n for n in z.namelist() if n.endswith("triage.csv") or n.endswith("triage.csv.gz"))
    opener = (lambda: gzip.open(z.open(name), "rt", encoding="utf-8", newline="")) if name.endswith(".gz") else (lambda: io.TextIOWrapper(z.open(name), encoding="utf-8", newline=""))
    rows = list(csv.DictReader(opener()))
    abdominal = [r for r in rows if "abd" in r.get("chiefcomplaint", "").lower()]
    out = {"triage_rows": len(rows), "abdominal_pain_rows": len(abdominal), "variables": {}}
    cols = {"temperature": "temperature_f", "heartrate": "heart_rate", "resprate": "respiratory_rate", "o2sat": "spo2", "sbp": "systolic_bp", "dbp": "diastolic_bp"}
    for col, key in cols.items():
        vals = []
        for r in abdominal:
            try:
                v = float(r[col])
            except (KeyError, ValueError):
                continue
            vals.append((v - 32) * 5 / 9 if col == "temperature" else v)
        if vals:
            out["variables"][key if col != "temperature" else "temperature_c"] = summary(vals, 1)
    return out


def main() -> int:
    OUT.mkdir(exist_ok=True)
    lab = {"dataset": "MIMIC-IV Clinical Database Demo", "version": "2.2", "licence": "ODbL 1.0", "doi": "10.13026/dp1f-ex47", "patients": 100,
           "note": "hospital patients, not healthy adults: used only to check plausibility of bands and generated values", "labs": labs()}
    (OUT / "mimic_lab_percentiles.json").write_text(json.dumps(lab, indent=1) + "\n", encoding="utf-8")
    ed = {"dataset": "MIMIC-IV-ED Demo", "version": "2.2", "licence": "ODbL 1.0", "doi": "10.13026/jzz5-vs76", "selection": "chief complaint contains 'abd'", **ed_triage()}
    (OUT / "mimic_ed_triage.json").write_text(json.dumps(ed, indent=1) + "\n", encoding="utf-8")
    print(json.dumps({"labs": {k: v["n"] for k, v in lab["labs"].items()}, "ed": {"rows": ed["triage_rows"], "abdominal": ed["abdominal_pain_rows"]}}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
