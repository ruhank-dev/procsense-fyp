"""Builds content/imaging/imaging.json: what each imaging test can show, per finding code, per case, with the approved report texts (p0-clinical-logic-s09).

    uv run --project tools tools/content/build_imaging.py            # write
    uv run --project tools tools/content/build_imaging.py --check    # fail if the file is out of date

Sources (all built and checked in earlier steps): the finding tables content/ultrasound_findings.csv, xsec_findings.csv, cxr_findings.csv and ecg_findings.csv; the approved report texts in
content/imaging_reports.json, xsec_reports.json, cxr_reports.json and ecg_reports.json (only APPROVED_FOR_TRAINING ones are used, so an unapproved picture is never shown); and
content/case_imaging.csv, which says which finding each case shows for each study.

What this script adds is structure and one judgement: each finding code gets a category the trainee can be asked to interpret (normal, positive, complicated, other_diagnosis, non_diagnostic).
'non_diagnostic' is the walkthrough's 'appendix not visualised': equivocal, never negative (Part 6.4). The category of each code is the project's reading of the table's plain wording (SR-0082).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "imaging" / "imaging.json"

CATEGORIES = ["normal", "positive", "complicated", "other_diagnosis", "non_diagnostic"]
CATEGORY = {
    # normal: nothing abnormal at the place that was looked at
    **{c: "normal" for c in ("US-GB-00", "US-AP-05", "US-PV-00", "US-RN-00", "CT-AP-00", "CT-GB-00", "CT-AB-00", "MR-AP-00", "MRCP-00", "CXR-01", "ECG-01")},
    # positive: a definite finding that is not complicated (the expected diagnosis, or an abnormal pattern on a film or strip)
    **{c: "positive" for c in ("US-GB-01", "US-GB-02", "US-GB-04", "US-AP-01", "CT-AP-01", "MR-AP-01", "MRCP-01", "CXR-02", "CXR-03", "CXR-04", "CXR-05", "CXR-06", "CXR-07",
                               "ECG-02", "ECG-03", "ECG-04", "ECG-05", "ECG-06", "ECG-08", "ECG-09", "ECG-10")},
    # complicated: a severe or complicated finding that changes the urgency
    **{c: "complicated" for c in ("US-GB-05", "US-AP-02", "CT-AP-02", "CT-GB-01", "CT-AB-02", "CXR-08", "ECG-07")},
    # other_diagnosis: a different problem from the one suspected (the walkthrough's 'negative or other')
    **{c: "other_diagnosis" for c in ("US-GB-03", "US-GB-06", "US-AP-03", "US-PV-01", "US-PV-02", "US-RN-01", "CT-AP-03", "CT-AP-04", "CT-AB-01")},
    # non_diagnostic: not seen, borderline or uncertain: equivocal, NOT negative
    **{c: "non_diagnostic" for c in ("US-AP-04", "US-GB-07", "US-GB-08")},
}
# investigation test id -> the studies in content/case_imaging.csv that belong to it
TESTS = {"ultrasound": ["ultrasound_gallbladder", "ultrasound_appendix", "ultrasound_pelvis", "ultrasound_renal"], "ct": ["ct_appendix_level"], "mri": ["mri_appendix_level"],
         "mrcp": ["mrcp"], "chest_xray": ["chest_xray"], "ecg": ["ecg"]}
# a film or strip with no abnormality when the case states none (only where that cannot contradict a story: a patient with a chest or heart story has one stated)
CLEAN = {"chest_xray": "CXR-01", "ecg": "ECG-01"}


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def build() -> str:
    findings = []
    for name, plain_key in (("ultrasound_findings", "v_plain"), ("xsec_findings", "d_plain"), ("cxr_findings", "d_plain"), ("ecg_findings", "d_plain")):
        for r in rows(name):
            plain = (r.get(plain_key) or "").strip() or (r.get("v_finding") or "").strip() or "No abnormality at this level"
            findings.append({"code": r["code"], "plain": plain, "category": CATEGORY.get(r["code"]), "ref": r["ref"]})
    missing = [f["code"] for f in findings if f["category"] is None]
    if missing:
        raise SystemExit(f"no category chosen for the finding codes {missing}")
    extra = sorted(set(CATEGORY) - {f["code"] for f in findings})
    if extra:
        raise SystemExit(f"categories given for codes that no finding table has: {extra}")

    reports = []
    for name in ("imaging_reports", "xsec_reports", "cxr_reports", "ecg_reports"):
        for r in json.loads((CONTENT / f"{name}.json").read_text(encoding="utf-8"))["reports"]:
            if r["status"] != "APPROVED_FOR_TRAINING":
                continue
            reports.append({"id": r["id"], "code": r["finding_code"], "image_id": r["image_id"], "text": r["text"], "measurements": {k: str(v) for k, v in sorted(r["measurements"].items())}, "synthetic": bool(r["synthetic"])})
    have = {r["code"] for r in reports}
    codes = {f["code"] for f in findings}
    studies = {s for ss in TESTS.values() for s in ss}
    case_studies: dict[str, dict[str, str]] = {}
    for r in rows("case_imaging"):
        if r["test"] not in studies:
            continue                      # bedside pictures that are text only (pregnancy test, dipstick, tube) are not imaging
        if r["d_finding_code"] not in codes:
            raise SystemExit(f"case_imaging.csv: {r['id']} uses the code {r['d_finding_code']}, which no finding table has")
        if r["d_finding_code"] not in have:
            raise SystemExit(f"case_imaging.csv: {r['id']} uses the code {r['d_finding_code']}, which has no approved report")
        case_studies.setdefault(r["case_id"], {})[r["test"]] = r["d_finding_code"]
    for code in CLEAN.values():
        if code not in have:
            raise SystemExit(f"the clean code {code} has no approved report")
    doc = {
        "categories": CATEGORIES,
        "tests": TESTS,
        "clean_fallback": CLEAN,
        "findings": sorted(findings, key=lambda f: f["code"]),
        "reports": sorted(reports, key=lambda r: r["id"]),
        "case_studies": {k: dict(sorted(v.items())) for k, v in sorted(case_studies.items())},
        "refs": ["SR-0082", "SR-0083"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/imaging/imaging.json is out of date; run tools/content/build_imaging.py")
            return 1
        print("content/imaging/imaging.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
