"""Numeric profiles of the lab and vital-sign datasets (p1-dataset-analysis-s02): the shape of real values before any generator is written.

    uv run --project tools tools/data/profile_numeric.py            # write tools/data/derived/profiles/{labs,vitals}.json and docs/research/datasets/profiles/numeric.md
    uv run --project tools tools/data/profile_numeric.py --check    # the committed profiles agree with the earlier aggregates and cover every analyte of content/lab_bands.csv (or state the gap)

Reads the git-ignored download cache (tools/data/cache: MIMIC-IV demo, MIMIC-IV-ED demo; needs the network the first time, see extract_mimic.py) and writes aggregates only: no patient row leaves this script.
Per lab analyte: n, percentiles, missing share, skew, the share the dataset's own flag calls abnormal, the share outside the walkthrough's normal band and the share within 10 percent of the band's width of an edge (borderline).
Correlations of linked analytes use values taken at the same time for the same patient; fewer than 50 pairs is marked weak (OQ-02).
The demo data are 100 hospital patients (not healthy adults): the profile describes what real values look like; it is not a reference range and nothing here is extrapolated.
"""
from __future__ import annotations

import csv
import gzip
import io
import json
import math
import sys
import zipfile
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import extract_mimic as em  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "derived" / "profiles"
REPORT = ROOT / "docs" / "research" / "datasets" / "profiles" / "numeric.md"
BANDS = ROOT / "content" / "lab_bands.csv"
WEAK_BELOW = 50
BORDERLINE_FRACTION = 0.10

# analytes in the dataset that the bands name in another unit or that the bands do not have: profiled in the dataset's own unit, never converted (no factor is printed in the walkthrough)
NATIVE = {
    "urea_nitrogen": ("Urea Nitrogen", "Blood", "mg/dL"), "haematocrit": ("Hematocrit", "Blood", "%"), "ggt": ("Gamma Glutamyltransferase", "Blood", "U/L"),
    "lipase": ("Lipase", "Blood", "U/L"), "hba1c": ("Hemoglobin A1c", "Blood", "%"), "neutrophils": ("Neutrophils", "Blood", "%"),
}
# pairs that move together in disease; matched on patient and time
LINKED = [("wbc", "crp"), ("urea_nitrogen", "creatinine"), ("haemoglobin", "haematocrit"), ("alt", "bilirubin"), ("alp", "bilirubin"), ("sodium", "glucose"), ("lactate", "creatinine")]


# ---------------------------------------------------------------- statistics (small, exact, tested)

def percentile(xs: list[float], p: float) -> float:
    return em.pct(xs, p)


def skewness(xs: list[float]) -> float:
    n = len(xs)
    if n < 3:
        return 0.0
    m = sum(xs) / n
    s2 = sum((x - m) ** 2 for x in xs) / n
    if s2 == 0:
        return 0.0
    return (sum((x - m) ** 3 for x in xs) / n) / (s2 ** 1.5)


def ranks(xs: list[float]) -> list[float]:
    order = sorted(range(len(xs)), key=lambda i: xs[i])
    out = [0.0] * len(xs)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and xs[order[j + 1]] == xs[order[i]]:
            j += 1
        for k in range(i, j + 1):
            out[order[k]] = (i + j) / 2 + 1
        i = j + 1
    return out


def pearson(a: list[float], b: list[float]) -> float | None:
    n = len(a)
    if n < 3:
        return None
    ma, mb = sum(a) / n, sum(b) / n
    sa = math.sqrt(sum((x - ma) ** 2 for x in a))
    sb = math.sqrt(sum((y - mb) ** 2 for y in b))
    if sa == 0 or sb == 0:
        return None
    return sum((x - ma) * (y - mb) for x, y in zip(a, b)) / (sa * sb)


def correlation(a: list[float], b: list[float]) -> dict:
    """Pearson and Spearman with a 95 percent interval (Fisher z) on Spearman; weak below 50 pairs."""
    n = len(a)
    r, rho = pearson(a, b), pearson(ranks(a), ranks(b))
    out: dict = {"pairs": n, "pearson": None if r is None else round(r, 3), "spearman": None if rho is None else round(rho, 3), "weak": n < WEAK_BELOW}
    if rho is not None and n > 3 and abs(rho) < 1:
        z, se = math.atanh(rho), 1 / math.sqrt(n - 3)
        out["spearman_ci95"] = [round(math.tanh(z - 1.96 * se), 3), round(math.tanh(z + 1.96 * se), 3)]
    return out


def band_shares(xs: list[float], low: float | None, high: float | None) -> dict:
    """Share outside the normal band, and share inside it but within 10 percent of its width of an edge (the borderline zone)."""
    if not xs or (low is None and high is None):
        return {}
    outside = sum(1 for x in xs if (low is not None and x < low) or (high is not None and x > high))
    out = {"outside_normal_share": round(outside / len(xs), 3)}
    if low is not None and high is not None and high > low:
        edge = (high - low) * BORDERLINE_FRACTION
        near = sum(1 for x in xs if low <= x <= high and (x - low < edge or high - x < edge))
        out["borderline_share"] = round(near / len(xs), 3)
    return out


def summary(xs: list[float]) -> dict:
    if not xs:
        return {"n": 0}
    return {"n": len(xs), **{f"p{p}": round(percentile(xs, p), 2) for p in (1, 5, 25, 50, 75, 95, 99)}, "mean": round(sum(xs) / len(xs), 2), "skew": round(skewness(xs), 2)}


# ---------------------------------------------------------------- reading the demo

def normal_bands() -> dict[str, dict[str, tuple[float | None, float | None]]]:
    """analyte -> sex ('' for both) -> (low, high) of the walkthrough's normal zone."""
    out: dict[str, dict[str, tuple[float | None, float | None]]] = defaultdict(dict)
    for r in csv.DictReader(BANDS.open(encoding="utf-8", newline="")):
        if r["zone"] != "normal" or r["basis"]:
            continue
        low = float(r["v_low"]) if r["v_low"] else None
        high = float(r["v_high"]) if r["v_high"] else None
        out[r["analyte"]][r["sex"]] = (low, high)
    return out


def read_labs() -> dict:
    z = zipfile.ZipFile(em.fetch(em.DEMO_URL, "mimic_iv_demo.zip"))
    items = {r["itemid"]: r for r in em.read_gz(z, "d_labitems.csv.gz")}
    sex = {r["subject_id"]: r["gender"] for r in em.read_gz(z, "patients.csv.gz")}
    wanted: dict[str, tuple[str, float]] = {}
    for key, (label, fluid, factor, _) in em.LABS.items():
        for iid, r in items.items():
            if r["label"] == label and r["fluid"] == fluid:
                wanted[iid] = (key, factor)
    for key, (label, fluid, _) in NATIVE.items():
        for iid, r in items.items():
            if r["label"] == label and r["fluid"] == fluid and iid not in wanted:
                wanted[iid] = (key, 1.0)
    rows: dict[str, list[tuple[float, str, str, str]]] = defaultdict(list)   # analyte -> (value, sex, patient|time key, mimic flag)
    seen: dict[str, int] = defaultdict(int)
    missing: dict[str, int] = defaultdict(int)
    for r in em.read_gz(z, "labevents.csv.gz"):
        hit = wanted.get(r["itemid"])
        if not hit:
            continue
        seen[hit[0]] += 1
        if not r["valuenum"]:
            missing[hit[0]] += 1
            continue
        rows[hit[0]].append((float(r["valuenum"]) * hit[1], sex.get(r["subject_id"], ""), r["subject_id"] + "|" + r["charttime"], r["flag"]))
    return {"rows": rows, "seen": seen, "missing": missing}


def lab_profiles(raw: dict, bands: dict) -> dict:
    out = {}
    for key, entries in sorted(raw["rows"].items()):
        xs = [e[0] for e in entries]
        unit = em.LABS[key][3] if key in em.LABS else NATIVE[key][2]
        p: dict = {"unit": unit, "unit_converted_with_a_printed_factor": key in em.LABS, **summary(xs)}
        total = raw["seen"][key]
        p["missing_share"] = round(raw["missing"][key] / total, 3) if total else None
        p["dataset_flag_abnormal_share"] = round(sum(1 for e in entries if e[3] == "abnormal") / len(entries), 3)
        band = bands.get("glucose_perioperative_diabetic" if key == "glucose" else key)
        if key in em.LABS and band:
            if "" in band:
                p.update(band_shares(xs, *band[""]))
            elif band:
                by_sex = {s: band_shares([e[0] for e in entries if e[1] == s], *band[s]) for s in band}
                p["by_sex"] = {s: {"n": sum(1 for e in entries if e[1] == s), **v} for s, v in sorted(by_sex.items())}
        else:
            p["band_note"] = "not compared with a band: " + ("the band is in another unit and the walkthrough prints no conversion factor" if key not in em.LABS else "the analyte has no band in lab_bands.csv")
        out[key] = p
    return out


def lab_correlations(raw: dict) -> list[dict]:
    by_key = {k: {e[2]: e[0] for e in v} for k, v in raw["rows"].items()}
    out = []
    for a, b in LINKED:
        if a not in by_key or b not in by_key:
            out.append({"a": a, "b": b, "pairs": 0, "note": "one of the two analytes is not in the demo"})
            continue
        common = sorted(set(by_key[a]) & set(by_key[b]))
        out.append({"a": a, "b": b, **correlation([by_key[a][k] for k in common], [by_key[b][k] for k in common])})
    return out


def read_triage() -> dict:
    z = zipfile.ZipFile(em.fetch(em.ED_URL, "mimic_iv_ed_demo.zip"))
    name = next(n for n in z.namelist() if n.endswith("triage.csv.gz"))
    rows = list(csv.DictReader(io.TextIOWrapper(gzip.open(z.open(name)), encoding="utf-8", newline="")))
    cols = {"temperature": "temperature_c", "heartrate": "heart_rate", "resprate": "respiratory_rate", "o2sat": "spo2", "sbp": "systolic_bp", "dbp": "diastolic_bp"}

    def values(selection: list[dict]) -> dict[str, list[float]]:
        out: dict[str, list[float]] = {}
        for col, key in cols.items():
            xs = []
            for r in selection:
                try:
                    v = float(r[col])
                except (KeyError, ValueError):
                    continue
                xs.append((v - 32) * 5 / 9 if col == "temperature" else v)
            out[key] = xs
        return out

    abdominal = [r for r in rows if "abd" in r.get("chiefcomplaint", "").lower()]
    return {"all": rows, "abdominal": abdominal, "cols": cols, "values": values}


def vital_profiles(triage: dict) -> dict:
    out: dict = {"triage_rows": len(triage["all"]), "abdominal_rows": len(triage["abdominal"]), "groups": {}}
    for label, rows in (("all_ed_triage", triage["all"]), ("abdominal_complaint", triage["abdominal"])):
        vals = triage["values"](rows)
        group = {"variables": {k: {**summary(v), "missing_share": round(1 - len(v) / len(rows), 3) if rows else None} for k, v in vals.items() if v}}
        pairs = []
        keys = [k for k in vals if vals[k]]
        for i, a in enumerate(keys):
            for b in keys[i + 1:]:
                ra = {j: x for j, x in enumerate(vals[a])}
                # pair by row: rebuild per row so a missing value drops only its own pair
                xs, ys = [], []
                for r in rows:
                    try:
                        va, vb = float(r[[c for c, k in triage["cols"].items() if k == a][0]]), float(r[[c for c, k in triage["cols"].items() if k == b][0]])
                    except (KeyError, ValueError):
                        continue
                    xs.append((va - 32) * 5 / 9 if a == "temperature_c" else va)
                    ys.append((vb - 32) * 5 / 9 if b == "temperature_c" else vb)
                del ra
                pairs.append({"a": a, "b": b, **correlation(xs, ys)})
        group["correlations"] = pairs
        out["groups"][label] = group
    vd = json.loads((Path(__file__).resolve().parent / "derived" / "vitaldb_noise.json").read_text(encoding="utf-8"))
    out["vitaldb_monitoring"] = {"source": "tools/data/derived/vitaldb_noise.json (profiled in extract_vitaldb.py)", "dataset": vd["dataset"], "selection": vd["selection"], "limitation": vd["limitation"],
                                 "variables": {k: {kk: vv for kk, vv in v.items() if kk in ("unit", "cases", "samples", "level_p5", "level_p50", "level_p95", "sigma_median", "phi_median")} for k, v in vd["variables"].items()}}
    return out


# ---------------------------------------------------------------- report

def report(labs: dict, corr: list[dict], vitals: dict, gaps: list[str]) -> str:
    L = ["# Numeric profiles: labs and vital signs", "",
         "Generated by `tools/data/profile_numeric.py` (p1-dataset-analysis-s02); do not edit. Aggregates only: no patient row is kept.", "",
         "**What this is.** How real values are spread in two small open datasets, so a generator can be designed against evidence. **What it is not.** A reference range (the walkthrough's bands stay the rule), a "
         "sample of healthy adults (the demo is 100 hospital patients), or anything extrapolated beyond its size. Every figure carries its n.", "",
         "Sources: MIMIC-IV Clinical Database Demo 2.2 and MIMIC-IV-ED Demo 2.2 (PhysioNet, ODbL 1.0, DS-0003, DS-0004); VitalDB (DS-0001, see `vitaldb_noise.json`).", "",
         "## Labs", "", "| Analyte | Unit | n | p1 | p5 | p50 | p95 | p99 | Skew | Missing | Flagged abnormal by the dataset | Outside the normal band | Borderline |", "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
    for k, p in labs.items():
        by = p.get("outside_normal_share", "—")
        if "by_sex" in p:
            by = "; ".join(f"{s}: {v.get('outside_normal_share', '—')} (n {v['n']})" for s, v in p["by_sex"].items())
        L.append(f"| {k} | {p['unit']}{'' if p['unit_converted_with_a_printed_factor'] else ' (native)'} | {p['n']} | {p['p1']} | {p['p5']} | {p['p50']} | {p['p95']} | {p['p99']} | {p['skew']} | {p['missing_share']} | {p['dataset_flag_abnormal_share']} | {by} | {p.get('borderline_share', '—')} |")
    L += ["", "Borderline = inside the normal band but within 10 percent of its width from an edge. \"Native\" units are the dataset's own (no conversion factor is printed in the walkthrough, so none is applied).", "",
          "## Linked analytes (same patient, same time)", "", "| Pair | Pairs | Pearson | Spearman | Spearman 95% CI | Weak? |", "|---|---:|---:|---:|---|---|"]
    for c in corr:
        L.append(f"| {c['a']} and {c['b']} | {c['pairs']} | {c.get('pearson', '—')} | {c.get('spearman', '—')} | {c.get('spearman_ci95', '—')} | {'weak (under 50 pairs)' if c.get('weak', True) else 'ok'} |")
    L += ["", "## Vital signs", "", f"MIMIC-IV-ED demo triage: {vitals['triage_rows']} rows, {vitals['abdominal_rows']} with an abdominal complaint (chief complaint contains \"abd\").", ""]
    for label, g in vitals["groups"].items():
        L += [f"### {label.replace('_', ' ')}", "", "| Variable | n | p5 | p25 | p50 | p75 | p95 | Missing |", "|---|---:|---:|---:|---:|---:|---:|---:|"]
        for k, v in g["variables"].items():
            L.append(f"| {k} | {v['n']} | {v['p5']} | {v['p25']} | {v['p50']} | {v['p75']} | {v['p95']} | {v['missing_share']} |")
        L += ["", "| Joint (pairs) | Pairs | Spearman | Weak? |", "|---|---:|---:|---|"]
        for c in g["correlations"]:
            L.append(f"| {c['a']} and {c['b']} | {c['pairs']} | {c.get('spearman', '—')} | {'weak' if c['weak'] else 'ok'} |")
        L.append("")
    L += ["### VitalDB (monitoring of anaesthetised patients)", "", f"Selection: {vitals['vitaldb_monitoring']['selection']}. Limitation: {vitals['vitaldb_monitoring']['limitation']}", "",
          "| Variable | Cases | p5 level | p50 level | p95 level | Noise sigma (median) | Noise phi (median) |", "|---|---:|---:|---:|---:|---:|---:|"]
    for k, v in vitals["vitaldb_monitoring"]["variables"].items():
        L.append(f"| {k} | {v.get('cases')} | {v.get('level_p5')} | {v.get('level_p50')} | {v.get('level_p95')} | {v.get('sigma_median')} | {v.get('phi_median')} |")
    L += ["", "## Gaps stated, not filled", ""] + [f"- {g}" for g in gaps] + [""]
    return "\n".join(L)


def gaps(labs: dict) -> list[str]:
    bands = normal_bands()
    out = []
    for analyte in sorted(bands):
        if analyte in labs or (analyte == "glucose_perioperative_diabetic" and "glucose" in labs):
            continue
        out.append(f"`{analyte}` is in lab_bands.csv but not in the demo (or not named in a unit the walkthrough converts): no profile.")
    out.append("The demo has 100 patients: percentiles beyond p5 and p95, and every correlation under 50 pairs, are weak.")
    out.append("Respiratory rate from VitalDB is the monitor value and the least reliable of the six; triage vitals are single spot values, not a time course.")
    return out


def build() -> tuple[dict, dict, str]:
    raw = read_labs()
    labs = lab_profiles(raw, normal_bands())
    corr = lab_correlations(raw)
    vitals = vital_profiles(read_triage())
    g = gaps(labs)
    lab_doc = {"dataset": "MIMIC-IV Clinical Database Demo", "version": "2.2", "licence": "ODbL 1.0", "patients": 100, "weak_below_pairs": WEAK_BELOW, "borderline_fraction": BORDERLINE_FRACTION,
               "labs": labs, "correlations": corr, "gaps": g}
    return lab_doc, vitals, report(labs, corr, vitals, g)


def check() -> int:
    problems = []
    try:
        labs = json.loads((OUT / "labs.json").read_text(encoding="utf-8"))
        old = json.loads((Path(__file__).resolve().parent / "derived" / "mimic_lab_percentiles.json").read_text(encoding="utf-8"))
    except FileNotFoundError as e:
        print("FAIL  " + str(e))
        return 1
    for k, o in old["labs"].items():
        p = labs["labs"].get(k)
        if not p:
            problems.append(f"{k}: in mimic_lab_percentiles.json but not profiled")
            continue
        for q in ("p5", "p95", "n"):
            if p[q] != o[q]:
                problems.append(f"{k}.{q}: profile {p[q]}, earlier aggregate {o[q]}")
    bands = normal_bands()
    stated = " ".join(labs["gaps"])
    for analyte in bands:
        key = "glucose" if analyte == "glucose_perioperative_diabetic" else analyte
        if key not in labs["labs"] and f"`{analyte}`" not in stated:
            problems.append(f"{analyte}: no profile and no stated gap")
    if not REPORT.exists():
        problems.append("docs/research/datasets/profiles/numeric.md is missing")
    for p in problems:
        print("FAIL  " + p)
    if not problems:
        print(f"PASS  {len(labs['labs'])} lab profiles reproduce the earlier p5, p95 and n; every analyte of lab_bands.csv has a profile or a stated gap")
    return 1 if problems else 0


def main(argv: list[str]) -> int:
    if "--check" in argv:
        return check()
    lab_doc, vitals, md = build()
    OUT.mkdir(parents=True, exist_ok=True)
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    (OUT / "labs.json").write_text(json.dumps(lab_doc, indent=1, sort_keys=True) + "\n", encoding="utf-8")
    (OUT / "vitals.json").write_text(json.dumps(vitals, indent=1, sort_keys=True) + "\n", encoding="utf-8")
    REPORT.write_text(md, encoding="utf-8", newline="\n")
    print(f"wrote {len(lab_doc['labs'])} lab profiles, {len(lab_doc['correlations'])} correlations, vitals and {REPORT.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
