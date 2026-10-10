"""The imaging library: for every test that returns a picture, the clean (no problem) result and every problem result, each with its stored pictures (ADR-0004).

    uv run --project tools tools/content/imaging_library.py            # rewrite content/imaging_library.csv
    uv run --project tools tools/content/imaging_library.py --check    # fail if it is out of date or the library has a hole (CI)

How the engine uses it (it never edits a picture):
1. Which finding does this case give for this test?  `case_imaging.csv` lists the problem result a case is built around; a test the case does not list returns the test's CLEAN code.
   A scenario event ("the scan shows an abscess") may change the finding code; that is the only way a result changes.
2. Pick a picture with that finding code from the library, using the session seed (the same seed gives the same picture on replay) and avoiding the picture just shown.
   Pictures are reused across cases and sessions; the report that belongs to the picture comes with it.
3. Only pictures at the status the mode allows are eligible (DT-055: practice accepts APPROVED_FOR_TRAINING or above, assessment needs APPROVED_FOR_ASSESSMENT).
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "imaging_library.csv"
# test id -> (set of pictures, clean finding code)
TESTS = {
    "ultrasound_gallbladder": ("ultrasound", "US-GB-00"), "ultrasound_appendix": ("ultrasound", "US-AP-05"), "ecg": ("ecg", "ECG-01"), "chest_xray": ("cxr", "CXR-01"),
    "pregnancy_test": ("bedside", "PREG-00"), "urine_dipstick": ("bedside", "DIP-00"), "blood_sample_appearance": ("bedside", "TUBE-00"),
    "ultrasound_pelvis": ("ultrasound", "US-PV-00"), "ultrasound_kidney": ("ultrasound", "US-RN-00"), "ct_abdomen_midlevel": ("xsec", "CT-AB-00"),
    "ct_appendix_level": ("xsec", "CT-AP-00"), "ct_gallbladder_level": ("xsec", "CT-GB-00"), "mri_appendix_level": ("xsec", "MR-AP-00"), "mrcp": ("xsec", "MRCP-00"),
}
PREFIX = {"pregnancy_test": "PREG-", "urine_dipstick": "DIP-", "blood_sample_appearance": "TUBE-", "ultrasound_pelvis": "US-PV-", "ultrasound_kidney": "US-RN-", "ct_abdomen_midlevel": "CT-AB-", "ultrasound_gallbladder": "US-GB-", "ultrasound_appendix": "US-AP-", "ecg": "ECG-", "chest_xray": "CXR-", "ct_appendix_level": "CT-AP-", "ct_gallbladder_level": "CT-GB-",
          "mri_appendix_level": "MR-AP-", "mrcp": "MRCP-"}
SETS = {"ultrasound": ("content/images/index.json", "content/ultrasound_findings.csv"), "ecg": ("content/ecg/index.json", "content/ecg_findings.csv"),
        "cxr": ("content/cxr/index.json", "content/cxr_findings.csv"), "xsec": ("content/xsec/index.json", "content/xsec_findings.csv"),
        "bedside": (None, "content/bedside_results.csv")}      # results only: no pictures
HEADER = ["test", "finding_code", "role", "finding", "pictures", "real", "synthetic", "picture_ids", "lowest_status", "ref"]
ORDER = ["DRAFT", "REVIEWED", "APPROVED_FOR_TRAINING", "APPROVED_FOR_ASSESSMENT"]


def findings(kind: str) -> dict[str, tuple[str, str]]:
    with open(ROOT / SETS[kind][1], encoding="utf-8", newline="") as f:
        return {r["code"]: ((r.get("v_finding") or r.get("d_finding") or ""), r["ref"]) for r in csv.DictReader(f)}


def result_text(code: str) -> str:
    """The written result of a text-only test (pregnancy test, dipstick, blood sample appearance)."""
    with open(ROOT / SETS["bedside"][1], encoding="utf-8", newline="") as f:
        for r in csv.DictReader(f):
            if r["code"] == code:
                return r["d_text"]
    raise LookupError(code)


TEXT_KINDS = {"bedside"}     # tests that return a written result only (owner decision, 10 October 2026)


def pictures(kind: str) -> list[dict]:
    if kind in TEXT_KINDS:
        return []
    return json.loads((ROOT / SETS[kind][0]).read_text(encoding="utf-8"))["images"]


def library() -> list[dict]:
    rows = []
    for test, (kind, clean) in TESTS.items():
        names, pics = findings(kind), pictures(kind)
        for code in sorted(c for c in names if c.startswith(PREFIX[test])):
            mine = [p for p in pics if p["finding_code"] == code]
            rows.append({"test": test, "finding_code": code, "role": "clean" if code == clean else "problem", "finding": names[code][0] or "(finding wording not in the book)", "pictures": len(mine),
                         "real": sum(not p["synthetic"] for p in mine), "synthetic": sum(p["synthetic"] for p in mine), "picture_ids": " ".join(p["id"] for p in mine),
                         "lowest_status": min((p["status"] for p in mine), key=ORDER.index) if mine else ("TEXT" if kind in TEXT_KINDS else ""), "ref": names[code][1]})
    return rows


def case_rows() -> list[dict]:
    with open(ROOT / "content" / "case_imaging.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


MIN_PICTURES = 4   # every result keeps at least four pictures, so a repeated order or replay does not show the same one


def whatif_rows() -> list[dict]:
    with open(ROOT / "content" / "whatif_imaging.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def problems(rows: list[dict] | None = None) -> list[str]:
    rows = rows or library()
    out = []
    for test, (_, clean) in TESTS.items():
        mine = [r for r in rows if r["test"] == test]
        if not any(r["role"] == "clean" for r in mine):
            out.append(f"{test}: the clean result {clean} has no row")
        if not any(r["role"] == "problem" for r in mine):
            out.append(f"{test}: no problem result")
    for r in rows:
        if r["lowest_status"] == "TEXT":
            if not result_text(r["finding_code"]).strip():
                out.append(f"{r['finding_code']} has no written result")
        elif r["pictures"] < MIN_PICTURES:
            out.append(f"{r['finding_code']} has {r['pictures']} picture(s); at least {MIN_PICTURES} are kept so results are not repeated")
    with open(ROOT / "content" / "case_seeds.csv", encoding="utf-8", newline="") as f:
        cases = {r["id"] for r in csv.DictReader(f)}
    by = {(r["test"], r["finding_code"]): r for r in rows}
    seen = set()
    for c in case_rows():
        key = (c["case_id"], c["test"])
        if c["case_id"] not in cases:
            out.append(f"case_imaging: unknown case {c['case_id']}")
        if key in seen:
            out.append(f"case_imaging: {key} listed twice")
        seen.add(key)
        r = by.get((c["test"], c["d_finding_code"]))
        if r is None:
            out.append(f"case_imaging: {c['case_id']} / {c['test']}: code {c['d_finding_code']} does not belong to that test")
        elif r["role"] != "problem":
            out.append(f"case_imaging: {c['case_id']} / {c['test']}: lists the clean code (tests not listed already return it)")
    with open(ROOT / "content" / "whatif_library.csv", encoding="utf-8", newline="") as f:
        whatifs = {r["id"] for r in csv.DictReader(f)}
    referenced = {c["d_finding_code"] for c in case_rows()}
    for w in whatif_rows():
        if w["whatif_id"] not in whatifs:
            out.append(f"whatif_imaging: unknown what-if {w['whatif_id']}")
        r = by.get((w["test"], w["d_finding_code"]))
        if r is None:
            out.append(f"whatif_imaging: {w['whatif_id']} / {w['test']}: code {w['d_finding_code']} does not belong to that test")
        referenced.add(w["d_finding_code"])
    clean_codes = {code for _, code in TESTS.values()}
    for r in rows:      # every result must be reachable: a case or a what-if produces it, or it is a test's clean default
        if r["finding_code"] not in referenced and r["finding_code"] not in clean_codes:
            out.append(f"{r['finding_code']} is not produced by any case or what-if (add a row to case_imaging or whatif_imaging)")
    return out


def result_code(case_id: str, test: str, cases: list[dict] | None = None, event_override: str | None = None) -> str:
    """The finding code a case gives for a test: a scenario event's override, else the case's listed problem, else the test's clean code."""
    if event_override:
        return event_override
    for c in cases if cases is not None else case_rows():
        if c["case_id"] == case_id and c["test"] == test:
            return c["d_finding_code"]
    return TESTS[test][1]


def pick(code: str, seed: int, last: str | None = None, minimum: str = "APPROVED_FOR_TRAINING") -> str:
    """A picture id for the code: eligible by status, chosen by the seed, never the one just shown when another exists."""
    eligible = [i for r in library() if r["finding_code"] == code for i in r["picture_ids"].split()]
    by_id = {p["id"]: p for kind in SETS for p in pictures(kind)}
    eligible = [i for i in eligible if ORDER.index(by_id[i]["status"]) >= ORDER.index(minimum)]
    if not eligible:
        raise LookupError(f"no picture for {code} at {minimum} or above")
    choices = [i for i in eligible if i != last] or eligible
    return choices[seed % len(choices)]


def csv_text(rows: list[dict]) -> str:
    buf = io.StringIO()
    w = csv.DictWriter(buf, HEADER, lineterminator="\n")
    w.writeheader()
    w.writerows(rows)
    return buf.getvalue()


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    rows = library()
    probs = problems(rows)
    text = csv_text(rows)
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            probs.append("content/imaging_library.csv is out of date: run `uv run --project tools tools/content/imaging_library.py`")
        for p in probs:
            print("  - " + p)
        return 1 if probs else 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"{len(rows)} results for {len(TESTS)} tests ({sum(r['pictures'] for r in rows)} pictures); " + ("no problems" if not probs else f"{len(probs)} problem(s)"))
    for p in probs:
        print("  - " + p)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
