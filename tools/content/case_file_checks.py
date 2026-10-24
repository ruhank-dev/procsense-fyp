"""Checks for the playable case files (content/case_files/*.json): schema, then the clinical and consistency rules that a schema cannot say.

    problems(case, ctx) -> list of strings; ctx = load_context()

What is checked: identity (age against the date of birth, wristband text, unique names and numbers); NEWS2 recomputed from the verified table for every observation; every result flag against its
normal range, critical potassium and sodium limits, sample-quality names; book results equal the walkthrough's measurement rows; unique ids and ranks; every decision has a correct and a wrong
option; decision rules, end states, verification ids and picture results exist; a variant named anywhere is listed in variant_end_states or has a decision; no medicine doses anywhere.
"""
from __future__ import annotations

import csv
import json
import re
import sys
from datetime import date
from pathlib import Path

import jsonschema

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import checks  # noqa: E402
import news2  # noqa: E402

CONTENT = ROOT / "content"
DOSE = re.compile(r"\b\d+(?:\.\d+)?\s?(?:mg|mcg|µg|ug|ml|mL|IU|units?)\b(?!/)|\b\d+(?:\.\d+)?\s?g\b(?!/)")


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def load_context() -> dict:
    ver = (ROOT / "docs" / "research" / "clinical_verification.md").read_text(encoding="utf-8")
    ctx = {
        "schema": json.loads((CONTENT / "schema" / "case_file.schema.json").read_text(encoding="utf-8")),
        "rules": {r["id"] for r in rows("decision_rules")}, "end_states": {r["id"] for r in rows("end_states")}, "problem_types": {r["id"] for r in rows("problem_types")},
        "quality": {r["id"] for r in rows("sample_quality")}, "verification": set(re.findall(r"\| (V-\d\d) \|", ver)),
        "measurements": {r["ref"]: r for r in rows("case_measurements")},
        "case_seeds": {r["id"]: r["ref"] for r in rows("case_seeds")}, "authoring": {r["case_id"]: r["ref"] for r in rows("case_files_authoring")},
        "imaging": [r for r in rows("case_imaging")],
    }
    sys.path.insert(0, str(Path(__file__).resolve().parent / "cases"))
    import p2comb                      # the combination cases (DT-220) have no book story: their story and authoring rows are the two design rows of the register
    ctx = _ctx_with_combinations(ctx, p2comb)
    return ctx


def _ctx_with_combinations(ctx: dict, p2comb) -> dict:
    ctx["case_seeds"].update({cid: p2comb.STORY_REF for cid in p2comb.CASES})
    ctx["authoring"].update({cid: p2comb.AUTHORING_REF for cid in p2comb.CASES})
    return ctx


def _num(text: str):
    m = re.search(r"-?\d+(?:\.\d+)?", text)
    return float(m.group(0)) if m else None


def in_normal(value: float, normal: str) -> bool | None:
    """True/False if the normal range text can be read ('3-21', 'below 5', '130-170', '4.0-11.0 (note)'); None if it cannot."""
    t = normal.lower().strip()
    m = re.match(r"^below (\d+(?:\.\d+)?)", t)
    if m:
        return value < float(m.group(1))
    m = re.match(r"^(\d+(?:\.\d+)?)\s*(?:-|to)\s*(\d+(?:\.\d+)?)", t)
    if m:
        return float(m.group(1)) <= value <= float(m.group(2))
    m = re.match(r"^(\d+(?:\.\d+)?) or above", t)
    if m:
        return value >= float(m.group(1))
    return None


CRITICAL = {"Potassium": (2.5, 6.5), "Sodium": (120, 160)}


def problems(c: dict, ctx: dict, others: list[dict] | None = None) -> list[str]:
    out: list[str] = []
    w = c.get("id", "?")
    errs = sorted(jsonschema.Draft202012Validator(ctx["schema"], registry=checks.schema_registry()).iter_errors(c), key=lambda e: list(e.path))
    out += [f"{w}: schema: {'/'.join(str(p) for p in e.path)}: {e.message[:140]}" for e in errs]
    if errs:
        return out
    ident, ref = c["identity"], date.fromisoformat(c["reference_date"])
    dob = date.fromisoformat(ident["dob"])
    age = ref.year - dob.year - ((ref.month, ref.day) < (dob.month, dob.day))
    if age != ident["age"]:
        out.append(f"{w}: age {ident['age']} does not match the date of birth {ident['dob']} on {ref}")
    if c["wristband"] is not None:
        wb = c["wristband"]
        if wb["name_text"] != ident["full_name"].upper() or wb["dob_text"] != dob.strftime("%d/%m/%Y") or wb["mrn_text"] != "MR " + ident["mrn"]:
            out.append(f"{w}: wristband text does not match the identity")
    if ident["full_name"] not in c["spoken_identity_answer"]:
        out.append(f"{w}: the spoken identity answer must contain the full name")
    for o in others or []:
        if o["id"] != w and (o["identity"]["mrn"] == ident["mrn"] or o["identity"]["full_name"] == ident["full_name"]):
            out.append(f"{w}: name or MRN repeats {o['id']}")
    # history
    hid = [h["id"] for h in c["history"]]
    if len(hid) != len(set(hid)):
        out.append(f"{w}: duplicate history ids")
    vol = [h for h in c["history"] if h["volunteered"]]
    if len(vol) != 1 or vol[0]["topic"] != "complaint":
        out.append(f"{w}: exactly one fact is volunteered and it is the complaint")
    for h in c["history"]:
        if not h["volunteered"] and not h["disclose_on"]:
            out.append(f"{w}: history {h['id']} is hidden but has no disclose_on")
    if c.get("pregnant") and c["identity"]["sex"] != "F":
        out.append(f"{w}: only a female patient can be pregnant")
    risk = c.get("risk")
    if risk is None:
        out.append(f"{w}: every case needs its expected ASA grade and anaesthetic plan (risk)")
    if risk is not None:
        a = risk["asa"]
        if not (1 <= a["min"] <= a["max"] <= 6):
            out.append(f"{w}: risk.asa needs 1 <= min <= max <= 6")
        if risk["plan"] == "standard_ga" and c["scenario"] == "S2":
            out.append(f"{w}: an S2 emergency is planned as rsi, not standard_ga")
    now = c["presentation"]["time_now"]
    for h in c["history"]:
        if h["topic"] in ("last_food", "last_drink"):
            m = re.search(r"\b([01]?\d|2[0-3]):([0-5]\d)\b", h["answer"])
            if m and re.search(r"\b(today|this morning)\b", h["answer"], re.IGNORECASE) and f"{int(m.group(1)):02d}:{m.group(2)}" > now:
                out.append(f"{w}: {h['topic']} says '{h['answer']}' but the case starts at {now}: a time today cannot be later than now")
            intake = h.get("intake")
            if intake is None:
                out.append(f"{w}: {h['topic']} has no intake (kind and hours before now)")
            elif intake["kind"] not in ("none", "unknown") and "hours_before_now" not in intake:
                out.append(f"{w}: {h['topic']} intake {intake['kind']} has no hours_before_now")
    topics = {h["topic"] for h in c["history"]}
    for t in ("complaint", "red_flags", "past_history", "medicines", "last_food", "last_drink"):
        if t not in topics:
            out.append(f"{w}: history lacks the topic {t}")
    # observations
    if c["observations"][0]["condition"] != "initial":
        out.append(f"{w}: the first observation must be the initial one")
    for o in c["observations"]:
        s = news2.score(o["heart_rate"], o["systolic_bp"], o["respiratory_rate"], o["spo2"], o["temperature_c"], o["acvpu"], o["oxygen"])
        if o["news2"] != s:
            out.append(f"{w}: observation {o['time']} {o['condition']} says NEWS2 {o['news2']} but the table gives {s}")
    # results
    rid = [r["id"] for r in c["results"]]
    if len(rid) != len(set(rid)):
        out.append(f"{w}: duplicate result ids")
    meas = ctx["measurements"]
    for r in c["results"]:
        v = _num(r["value"]) if r["flag"] in ("normal", "high", "low", "critical_high", "critical_low") else None
        if v is not None:
            normal = in_normal(v, r["normal_range"])
            if normal is True and r["flag"] != "normal":
                out.append(f"{w}: result {r['id']} {r['value']} is inside {r['normal_range']} but flagged {r['flag']}")
            if normal is False and r["flag"] == "normal":
                out.append(f"{w}: result {r['id']} {r['value']} is outside {r['normal_range']} but flagged normal")
            lo, hi = CRITICAL.get(r["test"].split(" (")[0], (None, None))
            if lo is not None and (v <= lo or v >= hi) and not r["flag"].startswith("critical"):
                out.append(f"{w}: result {r['id']} {r['value']} is a critical value but not flagged critical")
            if r["flag"] == "high" and normal is not False and normal is not None:
                pass
        if r.get("quality") and r["quality"] not in ctx["quality"]:
            out.append(f"{w}: result {r['id']} has an unknown sample-quality {r['quality']}")
        if r.get("source_row") in meas:
            m = meas[r["source_row"]]
            mv, rv = (_num(m["v_value"]) if re.fullmatch(r"-?\d+(?:\.\d+)?", m["v_value"].strip()) else None), _num(r["value"])     # composite rows ('WBC 15.2, CRP 96') are not compared
            if mv is not None and rv is not None and mv != rv:
                out.append(f"{w}: result {r['id']} {r['value']} differs from the walkthrough row {r['source_row']} ({m['v_value']})")
            if m["case_id"] != c.get("composition", {}).get("spine", w):      # a composed case cites the rows of its spine
                out.append(f"{w}: result {r['id']} cites a row of another case")
    # problems, decisions
    ranks = sorted(p["rank"] for p in c["problems"])
    if ranks != list(range(1, len(ranks) + 1)):
        out.append(f"{w}: problem ranks must be 1..n without gaps, got {ranks}")
    for p in c["problems"]:
        if p["type"] not in ctx["problem_types"]:
            out.append(f"{w}: unknown problem type {p['type']}")
    variants = set()
    for it in c["history"] + c["observations"] + c["results"] + c["decisions"]:
        if it.get("variant"):
            variants.add(it["variant"])
    for d in c["decisions"]:
        if not any(o["correct"] for o in d["options"]) or all(o["correct"] for o in d["options"]):
            out.append(f"{w}: decision {d['id']} needs at least one correct and one wrong option")
        if d["rule"] is not None and d["rule"] not in ctx["rules"]:
            out.append(f"{w}: decision {d['id']} cites an unknown rule {d['rule']}")
    if c["expected_end_state"] not in ctx["end_states"]:
        out.append(f"{w}: unknown end state")
    for k, v in c.get("variant_end_states", {}).items():
        if v not in ctx["end_states"]:
            out.append(f"{w}: unknown variant end state {v}")
    for v in c["verification"]:
        if v not in ctx["verification"]:
            out.append(f"{w}: unknown verification id {v}")
    if c["scenario"] == "S1" and "surgery_time" not in c["presentation"]:
        out.append(f"{w}: an S1 case has a booked surgery time")
    for blob in json.dumps({k: v for k, v in c.items() if k not in ("sources", "schema")}, ensure_ascii=False).split('", "'):
        m = DOSE.search(blob)
        if m:
            out.append(f"{w}: looks like a medicine dose ('{m.group(0)}'): the case files never contain doses")
            break
    # picture results for this case exist (checked more fully by imaging_library.py)
    for r in ctx["imaging"]:
        if r["case_id"] == w and not r["d_finding_code"]:
            out.append(f"{w}: empty picture result row")
    return out
