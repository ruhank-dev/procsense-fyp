"""Builds the generator parameter packs and the evaluation sets (p1-dataset-analysis-s05).

    uv run --project tools tools/generation/build_packs.py            # write content/generation/packs/*.json and tests/generation/*
    uv run --project tools tools/generation/build_packs.py --check    # fail if any of them is out of date, or a pack row has no source, or a pack holds a row of raw data (CI)

Offline preparation, separate from live generation: the packs are made from the committed profiles (tools/data/derived/profiles, aggregates only) and the walkthrough's bands (content/lab_bands.csv); a live session
only reads them (tools/generation/packs.py). The files are byte-identical on every run: sorted keys, no times. The pack manifest lists each file's SHA-256 and a version; content/manifest.json covers the files too.
Evaluation sets (tests/generation): gold scenarios (the 53 case files, summarised), cross-modal mutation vectors (the 12 contradiction types of tools/composer/mutate.py) and retrieval queries (a problem label asked
for, with the cases that hold it). Their data card is tests/generation/README.md.
"""
from __future__ import annotations

import csv
import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PROFILES = ROOT / "tools" / "data" / "derived" / "profiles"
PACKS = ROOT / "content" / "generation" / "packs"
EVAL = ROOT / "tests" / "generation"
VERSION = 1
OPEN_END_FACTOR = 1.25
# the dataset analytes the packs take from the lab profile, named as the bands name them (glucose uses the peri-operative diabetic zones)
BAND_NAME = {"glucose": "glucose_perioperative_diabetic"}
SOURCES = {"labs": "DS-0003 (MIMIC-IV demo 2.2, ODbL 1.0)", "vitals_ed": "DS-0004 (MIMIC-IV-ED demo 2.2, ODbL 1.0)", "vitals_monitor": "DS-0001 (VitalDB, CC BY 4.0 via the PhysioNet release; see DT-183)", "ecg": "DS-0002 (PTB-XL 1.0.3, CC BY 4.0)"}


def jdump(obj) -> str:
    return json.dumps(obj, indent=1, sort_keys=True, ensure_ascii=False) + "\n"


def num(s: str) -> float | None:
    return float(s) if s != "" else None


def bands() -> dict[str, dict]:
    out: dict[str, dict] = {}
    for r in csv.DictReader((ROOT / "content" / "lab_bands.csv").read_text(encoding="utf-8").splitlines()):
        a = out.setdefault(r["analyte"], {"unit": r["unit"], "zones": [], "relative_zones": []})
        row = {"zone": r["zone"], "sex": r["sex"], "low": num(r["v_low"]), "high": num(r["v_high"]), "bound": r["bound"], "ref": r["ref"], "id": r["id"]}
        (a["relative_zones"] if r["basis"] else a["zones"]).append({**row, "basis": r["basis"]} if r["basis"] else row)
    return out


def quantiles(p: dict) -> dict:
    return {k: p[k] for k in ("p1", "p5", "p25", "p50", "p75", "p95", "p99")}


def labs_pack() -> tuple[dict, dict]:
    prof = json.loads((PROFILES / "labs.json").read_text(encoding="utf-8"))
    b = bands()
    analytes = {}
    for name in sorted(b):       # an analyte profiled only in a unit with no band (haematocrit, urea nitrogen) stays in the correlation pack and has no row here
        src_name = next((k for k, v in BAND_NAME.items() if v == name), name)
        p = prof["labs"].get(src_name)
        row: dict = {"unit": (b.get(name) or {}).get("unit") or (p or {}).get("unit", ""), "zones": (b.get(name) or {}).get("zones", []), "relative_zones": (b.get(name) or {}).get("relative_zones", []),
                     "quantiles": None, "n": 0, "dataset_unit": None, "profile": None}
        if p and p["unit_converted_with_a_printed_factor"]:
            row.update(quantiles=quantiles(p), n=p["n"], dataset_unit=p["unit"], skew=p["skew"], missing_share=p["missing_share"], profile="tools/data/derived/profiles/labs.json", source=SOURCES["labs"])
        elif p:
            row.update(profile_note=f"profiled in the dataset's own unit {p['unit']}; no conversion factor is printed in the walkthrough, so the quantiles are not used (the analyte is drawn from its band only)", source=SOURCES["labs"])
        else:
            row["profile_note"] = "not in the demo dataset or no band unit match: drawn from its band only"
        analytes[name.replace("glucose_perioperative_diabetic", "glucose")] = row
    labs = {"version": VERSION, "kind": "labs", "open_end_rule": {"factor_of_start": OPEN_END_FACTOR, "text": "an open-ended zone is closed at the larger of the dataset's p99 and 1.25 times the zone's start"},
            "weak_below_pairs": prof["weak_below_pairs"], "analytes": analytes, "note": "Aggregates only; the zones are the walkthrough's (content/lab_bands.csv, each row's SR id); the quantiles come from the profile, every one with its n."}
    corr = {"version": VERSION, "kind": "correlations", "source": SOURCES["labs"], "pairs": [c for c in prof["correlations"] if c.get("pairs")]}
    return labs, corr


def vitals_pack() -> dict:
    v = json.loads((PROFILES / "vitals.json").read_text(encoding="utf-8"))
    groups = {}
    for name, g in v["groups"].items():
        groups[name] = {"variables": {k: {**quantiles(x), "n": x["n"], "missing_share": x["missing_share"]} for k, x in g["variables"].items()},
                        "correlations": [c for c in g["correlations"] if c.get("pairs")]}
    return {"version": VERSION, "kind": "vitals", "triage": {"source": SOURCES["vitals_ed"], "rows": v["triage_rows"], "abdominal_rows": v["abdominal_rows"], "groups": groups,
                                                        "note": "spot values at triage; the abdominal group is small (weak)"},
            "monitoring": {"source": SOURCES["vitals_monitor"], "selection": v["vitaldb_monitoring"]["selection"], "limitation": v["vitaldb_monitoring"]["limitation"], "variables": v["vitaldb_monitoring"]["variables"]}}


def ecg_pack() -> dict:
    w = json.loads((PROFILES / "waveforms.json").read_text(encoding="utf-8"))
    classes = {}
    for code, c in w["classes"].items():
        keep = {k: c[k] for k in ("name", "library_finding", "available_records", "measured_records", "rate_per_min", "rr_variation_cv", "qrs_width_ms_coarse", "noise_share_of_r_height", "thin", "caveat", "plan", "selection_note") if k in c}
        keep["use"] = "real_recording" if c["available_records"] else "parametric_after_reference_is_read"
        classes[code] = keep
    return {"version": VERSION, "kind": "ecg", "source": SOURCES["ecg"], "sampling_hz": w["sampling_hz"], "lead": w["lead"], "classes": classes, "not_measured": w["not_measured"],
            "rule": "a rate or noise outside the class's p5 to p95 is not generated; a class with use = parametric_after_reference_is_read is not generated until its cardiology reference is read (OQ-05)"}


def pack_files() -> dict[str, str]:
    labs, corr = labs_pack()
    joint = json.loads((PROFILES / "joint.json").read_text(encoding="utf-8"))          # made by tools/generation/fit_joint.py from the demo data (aggregates only)
    files = {"labs": jdump(labs), "correlations": jdump(corr), "vitals": jdump(vitals_pack()), "ecg": jdump(ecg_pack()), "joint": jdump(joint)}
    manifest = {"version": VERSION, "files": {f"{k}.json": hashlib.sha256(v.encode("utf-8")).hexdigest() for k, v in sorted(files.items())},
                "note": "Packs never hold raw rows: every number is an aggregate with its n. A live session reads them and never refits."}
    files["manifest"] = jdump(manifest)
    return files


# ---------------------------------------------------------------- evaluation sets

def case_files() -> list[dict]:
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "case_files").glob("*.json"))]


def gold_scenarios(cases: list[dict]) -> dict:
    out = []
    for c in cases:
        out.append({"id": c["id"], "tier": c["tier"], "scenario": c["scenario"], "age": c["identity"]["age"], "sex": c["identity"]["sex"], "expected_end_state": c["expected_end_state"],
                    "problems": [{"id": p["id"], "type": p["type"], "label": p["label"]} for p in c["problems"]],
                    "abnormal_results": sorted({r["test"].split(" (")[0] for r in c["results"] if r["flag"] not in ("normal", "")})})
    return {"note": "The 53 authored cases summarised as reference scenarios for generated ones: a generated scenario is compared with these for structure and for novelty.", "scenarios": out}


def mutation_vectors() -> dict:
    sys.path.insert(0, str(ROOT / "tools"))
    sys.path.insert(0, str(ROOT / "tools" / "content"))
    from composer import mutate
    return {"note": "The cross-modal contradiction types: each breaks one rule of a valid case; Stage B must reject the broken copy with this code (tools/composer/mutate.py, 5 cases each in CI).",
            "types": [{"code": k, "mutator": fn.__name__, "what": (fn.__doc__ or "").strip() or fn.__name__.removeprefix("m_").replace("_", " ")} for k, fn in sorted(mutate.MUTATIONS.items())]}


def retrieval_queries(cases: list[dict]) -> dict:
    """A problem label asked for as an author would, with the cases that hold it. Only cases a session may use (approved) are expected: a DRAFT case is never retrieved."""
    usable = [c for c in cases if c["status"] in ("APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT")]
    by_label: dict[str, list[str]] = {}
    for c in usable:
        for p in c["problems"]:
            by_label.setdefault(p["label"], []).append(c["id"])
    queries = [{"query": label, "expected_cases": sorted(set(ids))} for label, ids in sorted(by_label.items())]
    # the same problem asked for in other words: the first sentence of why it matters, and what the debrief says to remember (the words differ from the label)
    for c in usable:
        for p in c["problems"]:
            first = p["why"].split(".")[0].strip()
            if len(first.split()) >= 6:
                queries.append({"query": first, "expected_cases": [c["id"]]})
        remember = c["debrief"].get("remember", "").split(".")[0].strip()
        if len(remember.split()) >= 6:
            queries.append({"query": remember, "expected_cases": [c["id"]]})
    return {"note": "Each query is a problem label, the first sentence of why it matters, or what the debrief says to remember, as an author would ask for it; the approved cases that hold it are the answer (a scenario family is a tag filter, not a search). Recall at 20 is measured against these (p1-live-scenario-generation-s02).", "queries": queries}


CARD = """# Evaluation sets for live scenario generation

Generated by `tools/generation/build_packs.py` (p1-dataset-analysis-s05); do not edit.

| File | What it is | Made from | Use |
|---|---|---|---|
| `gold_scenarios.json` | The 53 authored cases summarised (age, sex, problems, abnormal results, expected end state) | `content/case_files` | Structure and novelty reference for generated scenarios |
| `mutations.json` | The 12 cross-modal contradiction types | `tools/composer/mutate.py` | Stage B and the cross-modal checks must reject each broken copy |
| `retrieval_queries.json` | Problem labels and scenario families with the cases that hold them | `content/case_files` | Recall at k of the retrieval index |

**Data card.** Synthetic: every case is invented (labelled synthetic practice material); no real patient data. Licence: the project's own content. Known limits: queries are derived from the cases themselves, so
they measure whether retrieval finds what is there, not whether it understands a paraphrase; a human-written paraphrase set is added when the labelling programme (p1-ai-infrastructure-s04) has a labeller.
"""


def eval_files() -> dict[str, str]:
    cases = case_files()
    return {"gold_scenarios.json": jdump(gold_scenarios(cases)), "mutations.json": jdump(mutation_vectors()), "retrieval_queries.json": jdump(retrieval_queries(cases)), "README.md": CARD}


def problems() -> list[str]:
    out = []
    for name, text in pack_files().items():
        path = PACKS / f"{name}.json"
        if not path.exists() or path.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            out.append(f"{path.relative_to(ROOT).as_posix()} is out of date: run `uv run --project tools tools/generation/build_packs.py`")
    for name, text in eval_files().items():
        path = EVAL / name
        if not path.exists() or path.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            out.append(f"{path.relative_to(ROOT).as_posix()} is out of date: run `uv run --project tools tools/generation/build_packs.py`")
    # no raw rows: a pack value must be an aggregate (a quantile table with its n), never a list of numbers
    labs = json.loads(pack_files()["labs"])
    for a, row in labs["analytes"].items():
        q = row["quantiles"]
        if q is not None and (set(q) != {"p1", "p5", "p25", "p50", "p75", "p95", "p99"} or row["n"] < 1):
            out.append(f"labs pack: {a} is not a quantile table with an n")
        if q is None and not row.get("profile_note"):
            out.append(f"labs pack: {a} has no quantiles and no note saying why")
        if not row["zones"] and not row["relative_zones"] and q is None:
            out.append(f"labs pack: {a} has neither zones nor quantiles")
    def walk(x, path):
        if isinstance(x, list) and len(x) > 60 and all(isinstance(i, (int, float)) for i in x):
            out.append(f"{path}: a list of {len(x)} numbers looks like raw data")
        elif isinstance(x, dict):
            for k, v in x.items():
                walk(v, f"{path}.{k}")
        elif isinstance(x, list):
            for i, v in enumerate(x):
                walk(v, f"{path}[{i}]")
    for name, text in pack_files().items():
        walk(json.loads(text), name)
    return out


def main(argv: list[str]) -> int:
    if "--check" in argv:
        p = problems()
        for x in p:
            print("FAIL  " + x)
        if not p:
            files = pack_files()
            print(f"PASS  {len(files) - 1} packs and {len(eval_files())} evaluation files are current, hold no raw rows, and every analyte has zones or a stated reason")
        return 1 if p else 0
    PACKS.mkdir(parents=True, exist_ok=True)
    EVAL.mkdir(parents=True, exist_ok=True)
    for name, text in pack_files().items():
        (PACKS / f"{name}.json").write_text(text, encoding="utf-8", newline="\n")
    for name, text in eval_files().items():
        (EVAL / name).write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {len(pack_files())} pack files to {PACKS.relative_to(ROOT).as_posix()} and {len(eval_files())} evaluation files to {EVAL.relative_to(ROOT).as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
