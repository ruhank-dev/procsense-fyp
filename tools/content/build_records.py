"""Builds content/records/records.json: the patient record the folder and the EMR show (p0-patient-records-emr-s01).

    uv run --project tools tools/content/build_records.py            # write
    uv run --project tools tools/content/build_records.py --check    # fail if the file is out of date

What is in it:
- document types: the kinds of document, and whether each is current-episode paperwork (in the folder and the EMR) or part of the hospital record (the EMR only);
- the current-episode documents a session starts with (the clerking note, the observation chart) and the ones an event creates (consent signed, ward checklist signed);
- the document type a result becomes, by kind of test;
- each case's hospital record, from content/emr_case_records.csv (written from the case files, restating them and adding no clinical fact; every row has its register id);
- the distractor patients on the ward list, from content/emr_distractors.csv (authored identities with no clinical history, SR-3050 to SR-3055), so that finding the right patient is a real task.
The document types and the event that creates each current document are design values of the project team (no clinical value).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "records" / "records.json"

TYPES = [
    # id, label, episode (current: folder and EMR; record: EMR only)
    ("referral", "Referral letter", "current"),
    ("clerking", "Clerking note", "current"),
    ("obs_chart", "Observation chart", "current"),
    ("consent", "Consent form", "current"),
    ("ward_checklist", "Ward checklist", "current"),
    ("lab_report", "Laboratory report", "current"),
    ("imaging_report", "Imaging report", "current"),
    ("bedside_result", "Bedside test result", "current"),
    ("clinic_letter", "Clinic letter or summary", "record"),
    ("lab_result", "Previous laboratory result", "record"),
    ("previous_imaging", "Previous imaging report", "record"),
    ("anaesthetic_history", "Previous anaesthetic record", "record"),
    ("discharge_summary", "Discharge summary", "record"),
]

CURRENT = [
    # id, type, title, created by: "start" or a flag that, once set, creates it
    ("clerking", "clerking", "Clerking note", "start"),
    ("obs_chart", "obs_chart", "Observation chart", "start"),
    ("consent", "consent", "Consent form", "consent.signed"),
    ("ward_checklist", "ward_checklist", "Ward checklist", "ward.checklist_signed"),
]

RESULT_TYPES = {"blood": "lab_report", "bedside": "bedside_result", "imaging": "imaging_report"}

# the case records name previous imaging "imaging_report"; in the record they are previous imaging (EMR only), a new scan's report is current paperwork
RECORD_TYPE = {"imaging_report": "previous_imaging"}


def rows(name: str) -> list[dict]:
    with open(CONTENT / name, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def build() -> str:
    known = {t[0] for t in TYPES}
    records: dict[str, list[dict]] = {}
    for r in rows("emr_case_records.csv"):
        kind = RECORD_TYPE.get(r["doc_type"], r["doc_type"])
        if kind not in known:
            raise SystemExit(f"emr_case_records.csv {r['id']}: unknown document type {r['doc_type']}")
        records.setdefault(r["case_id"], []).append({
            "id": r["id"], "type": kind, "title": r["title"], "date_offset": r["date_offset"], "body": r["d_body"],
            "synthetic": r["synthetic"] == "yes", "ref": r["ref"],
        })
    distractors = [{"id": r["patient_id"], "name": r["d_name"], "dob": r["d_dob"], "mrn": r["d_mrn"], "ref": r["ref"]} for r in rows("emr_distractors.csv")]
    doc = {
        "document_types": [{"id": i, "label": label, "episode": ep} for i, label, ep in TYPES],
        "current": [{"id": i, "type": t, "title": title, "created": c} for i, t, title, c in CURRENT],
        "result_types": RESULT_TYPES,
        "records": {k: records[k] for k in sorted(records)},
        "distractors": distractors,
        "refs": sorted({r["ref"] for v in records.values() for r in v} | {d["ref"] for d in distractors}),
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/records/records.json is out of date; run tools/content/build_records.py")
            return 1
        print("content/records/records.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
