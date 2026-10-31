"""Builds content/records/records.json: the patient record the folder and the EMR show (p0-patient-records-emr-s01).

    uv run --project tools tools/content/build_records.py            # write
    uv run --project tools tools/content/build_records.py --check    # fail if the file is out of date

What is in it:
- the EMR's tabs, and document types: the kinds of document, whether each is current-episode paperwork (in the folder and the EMR) or part of the hospital record (the EMR only),
  and the tab it shows under (a new one badges its tab until opened, p0-patient-records-emr-s05);
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

# The EMR's tabs (p0-patient-records-emr README, "EMR screens"); a new document puts a badge on its tab until it is opened (p0-patient-records-emr-s05).
TABS = [
    ("profile", "Patient profile"),
    ("current_episode", "Current episode"),
    ("labs", "Lab results"),
    ("imaging", "Imaging"),
    ("reports", "Reports"),
    ("history", "Medical history"),
    ("medications", "Medications"),
]

TYPES = [
    # id, label, episode (current: folder and EMR; record: EMR only), EMR tab
    ("referral", "Referral letter", "current", "current_episode"),
    ("clerking", "Clerking note", "current", "current_episode"),
    ("obs_chart", "Observation chart", "current", "current_episode"),
    ("consent", "Consent form", "current", "current_episode"),
    ("ward_checklist", "Ward checklist", "current", "current_episode"),
    ("lab_report", "Laboratory report", "current", "labs"),
    ("imaging_report", "Imaging report", "current", "imaging"),
    ("bedside_result", "Bedside test result", "current", "labs"),
    ("clinic_letter", "Clinic letter or summary", "record", "reports"),
    ("lab_result", "Previous laboratory result", "record", "labs"),
    ("previous_imaging", "Previous imaging report", "record", "imaging"),
    ("anaesthetic_history", "Previous anaesthetic record", "record", "history"),
    ("discharge_summary", "Discharge summary", "record", "reports"),
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
    tabs = {t[0] for t in TABS}
    for t in TYPES:
        if t[3] not in tabs:
            raise SystemExit(f"document type {t[0]}: unknown tab {t[3]}")
    records: dict[str, list[dict]] = {}
    for r in rows("emr_case_records.csv"):
        kind = RECORD_TYPE.get(r["doc_type"], r["doc_type"])
        if kind not in known:
            raise SystemExit(f"emr_case_records.csv {r['id']}: unknown document type {r['doc_type']}")
        records.setdefault(r["case_id"], []).append({
            "id": r["id"], "type": kind, "title": r["title"], "date_offset": r["date_offset"], "body": r["d_body"],
            "synthetic": r["synthetic"] == "yes", "ref": r["ref"],
        })
    distractors = [{"id": r["patient_id"], "name": r["d_name"], "dob": r["d_dob"], "mrn": r["d_mrn"], "location": r["d_location"], "ref": r["ref"]} for r in rows("emr_distractors.csv")]
    doc = {
        "tabs": [{"id": i, "label": label} for i, label in TABS],
        "document_types": [{"id": i, "label": label, "episode": ep, "tab": tab} for i, label, ep, tab in TYPES],
        "current": [{"id": i, "type": t, "title": title, "created": c} for i, t, title, c in CURRENT],
        "result_types": RESULT_TYPES,
        "records": {k: records[k] for k in sorted(records)},
        "distractors": distractors,
        "refs": sorted({r["ref"] for v in records.values() for r in v} | {d["ref"] for d in distractors}),
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def registered() -> set[str]:
    with open(ROOT / "docs" / "research" / "source_register.csv", encoding="utf-8", newline="") as f:
        return {r["id"] for r in csv.DictReader(f)}


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        missing = sorted(set(json.loads(text)["refs"]) - registered())
        if missing:
            print(f"FAIL  {OUT.relative_to(ROOT).as_posix()} cites register rows that do not exist: {', '.join(missing)}")
            return 1
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
